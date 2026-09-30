using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Network;
using AbsoluteRP.Social;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.RsUI.Pages;

// Feed-style social hub: category tab bar + scrollable list of post cards + composer modal. Groups/legacy content still reachable via the "Legacy" tab until those are migrated separately.
public sealed class SocialPage : IPage
{
    public string Id => "social";
    public string Title => "Social";
    public FontAwesomeIcon Icon => FontAwesomeIcon.Comments;

    // Which sub-view the page is showing right now. Detail and Compose replace the feed content in the same panel instead of stacking a modal on top of it.
    private enum SocialView { Feed, Detail, Compose }

    // UI state (per-page-instance) Legacy field kept so existing paged/fetched state elsewhere in this page keeps working - its value only matters for the feed-filter dropdown selection now, not for a visible tab bar.
    private int         _selectedTab              = 0;
    private int         _lastTabForPager          = -1;   // resets _feedPage when tab changes
    // Top-level section buttons - Feed / Bookmarks / Connections / Groups. Feed hosts the old feed with a category-filter dropdown that adds an "All" option; Bookmarks and Connections/Groups route to their respective sub-views.
    private enum TopSection { Feed = 0, Bookmarks = 1, Connections = 2, Groups = 3, Search = 4 }
    private TopSection  _topSection               = TopSection.Feed;
    // Category filter for the Feed section. Index 0 = "All", 1..N map to Categories[i-1]. Persisted across renders so the choice sticks.
    private int         _feedFilterIndex          = 0;
    // Search bar state used by the Connections section.
    private string      _searchQuery              = string.Empty;
    private int         _searchKindIndex          = 0;   // 0=Profiles, 1=Accounts, 2=Groups
    private bool        _searchSubmitted;
    // Page index for the feed pager. Reset to 0 on tab change / refresh so switching categories always drops the user back to the newest posts.
    private int         _feedPage                 = 0;
    private const int   FeedPageSize              = 20;
    private long        _feedVersionSeen          = -1;
    private long        _fetchedThisOpen          = 0;
    private long        _bookmarksFetchedThisOpen = 0;
    private bool        _notificationsPopupOpen;
    private SocialView  _view                     = SocialView.Feed;
    private SocialPost? _detailPost;                  // when _view == Detail
    private int         _lastCommentsFetchedFor    = 0;
    private string      _newCommentBody           = string.Empty;
    private int         _editingCommentId;
    // Tab-scoped "please open Compose next Draw" flag so header buttons switch the view without also processing card clicks the same frame.
    private bool        _requestOpenCompose;

    // Composer modal state.
    private bool          _composeOpen;
    private int           _composeEditId;      // 0 = new
    private SocialCategory _composeCategory   = SocialCategory.Event;
    private SocialScope    _composeScope      = SocialScope.All;
    private string         _composeScopeValue = string.Empty;
    // Region -> data center -> world cascade behind the scope pickers.
    private AbsoluteRP.Defines.FFXIVRegion     _composeRegion = AbsoluteRP.Defines.FFXIVRegion.NorthAmerica;
    private AbsoluteRP.Defines.FFXIVDataCenter _composeDC     = AbsoluteRP.Defines.FFXIVDataCenter.Aether;
    private string                             _composeWorld  = string.Empty;

    // Starts the cascade at the player's own world (or, when editing, at whatever the post was scoped to).
    private void SeedScopeCascade(SocialScope scope, string value)
    {
        var myWorld = Plugin.plugin?.playerworld ?? string.Empty;
        var myDC = AbsoluteRP.Defines.GameData.DataCenterOfWorld(myWorld) ?? AbsoluteRP.Defines.FFXIVDataCenter.Aether;
        _composeDC = myDC;
        _composeRegion = AbsoluteRP.Defines.GameData.GetRegionOfDataCenter(myDC);
        _composeWorld = string.IsNullOrEmpty(myWorld) ? (AbsoluteRP.Defines.GameData.GetWorldNamesByDataCenter(myDC).FirstOrDefault() ?? "") : myWorld;
        if (string.IsNullOrWhiteSpace(value)) return;
        value = PlaceLeaf(value);   // paths and bare names both seed from the leaf
        switch (scope)
        {
            case SocialScope.Region:
                {
                    var r = AbsoluteRP.Defines.GameData.RegionFromCode(value);
                    if (r.HasValue) SetComposeRegion(r.Value);
                    break;
                }
            case SocialScope.DC:
                {
                    var dc = AbsoluteRP.Defines.GameData.DataCenterFromName(value);
                    if (dc.HasValue) SetComposeDC(dc.Value);
                    break;
                }
            case SocialScope.World:
                {
                    var dc = AbsoluteRP.Defines.GameData.DataCenterOfWorld(value);
                    if (dc.HasValue) { SetComposeDC(dc.Value); _composeWorld = value.Trim(); }
                    break;
                }
        }
    }

    // Picking a region narrows the data centers to that region; the player's own DC / world are kept when they belong to the pick.
    private void SetComposeRegion(AbsoluteRP.Defines.FFXIVRegion region)
    {
        _composeRegion = region;
        var dcs = AbsoluteRP.Defines.GameData.GetDataCentersByRegion(region);
        if (!dcs.Contains(_composeDC)) SetComposeDC(dcs.FirstOrDefault());
    }

    private void SetComposeDC(AbsoluteRP.Defines.FFXIVDataCenter dc)
    {
        _composeDC = dc;
        _composeRegion = AbsoluteRP.Defines.GameData.GetRegionOfDataCenter(dc);
        var worlds = AbsoluteRP.Defines.GameData.GetWorldNamesByDataCenter(dc);
        var mine = Plugin.plugin?.playerworld ?? string.Empty;
        if (!worlds.Any(w => string.Equals(w, _composeWorld, StringComparison.OrdinalIgnoreCase)))
            _composeWorld = worlds.FirstOrDefault(w => string.Equals(w, mine, StringComparison.OrdinalIgnoreCase)) ?? worlds.FirstOrDefault() ?? string.Empty;
    }

    // Scope values are place PATHS so the feed can browse by prefix: "NA", "NA/Crystal", "NA/Crystal/Balmung". feed place filter (Anywhere / region / data center / world)
    private int _feedPlaceLevel;   // 0 anywhere, 1 region, 2 data center, 3 world
    private AbsoluteRP.Defines.FFXIVRegion     _feedRegion = AbsoluteRP.Defines.FFXIVRegion.NorthAmerica;
    private AbsoluteRP.Defines.FFXIVDataCenter _feedDC     = AbsoluteRP.Defines.FFXIVDataCenter.Aether;
    private string                             _feedWorld  = string.Empty;
    private bool _feedPlaceSeeded;

    private void SeedFeedPlace()
    {
        if (_feedPlaceSeeded) return;
        _feedPlaceSeeded = true;
        var myWorld = Plugin.plugin?.playerworld ?? string.Empty;
        var myDC = AbsoluteRP.Defines.GameData.DataCenterOfWorld(myWorld) ?? AbsoluteRP.Defines.FFXIVDataCenter.Aether;
        _feedDC = myDC;
        _feedRegion = AbsoluteRP.Defines.GameData.GetRegionOfDataCenter(myDC);
        _feedWorld = string.IsNullOrEmpty(myWorld) ? (AbsoluteRP.Defines.GameData.GetWorldNamesByDataCenter(myDC).FirstOrDefault() ?? "") : myWorld;
    }

    private void SetFeedRegion(AbsoluteRP.Defines.FFXIVRegion region)
    {
        _feedRegion = region;
        var dcs = AbsoluteRP.Defines.GameData.GetDataCentersByRegion(region);
        if (!dcs.Contains(_feedDC)) SetFeedDC(dcs.FirstOrDefault());
    }

    private void SetFeedDC(AbsoluteRP.Defines.FFXIVDataCenter dc)
    {
        _feedDC = dc;
        _feedRegion = AbsoluteRP.Defines.GameData.GetRegionOfDataCenter(dc);
        var worlds = AbsoluteRP.Defines.GameData.GetWorldNamesByDataCenter(dc);
        var mine = Plugin.plugin?.playerworld ?? string.Empty;
        if (!worlds.Any(w => string.Equals(w, _feedWorld, StringComparison.OrdinalIgnoreCase)))
            _feedWorld = worlds.FirstOrDefault(w => string.Equals(w, mine, StringComparison.OrdinalIgnoreCase)) ?? worlds.FirstOrDefault() ?? string.Empty;
    }

    private string FeedFilterPath() => _feedPlaceLevel switch
    {
        1 => AbsoluteRP.Defines.GameData.GetRegionCode(_feedRegion),
        2 => AbsoluteRP.Defines.GameData.GetRegionCode(_feedRegion) + "/" + AbsoluteRP.Defines.GameData.GetDataCenterName(_feedDC),
        3 => AbsoluteRP.Defines.GameData.GetRegionCode(_feedRegion) + "/" + AbsoluteRP.Defines.GameData.GetDataCenterName(_feedDC) + "/" + _feedWorld,
        _ => string.Empty,
    };

    // Draws the place pickers; returns true when the filter changed.
    private bool DrawFeedPlaceFilter()
    {
        SeedFeedPlace();
        var changed = false;
        ImGui.SameLine();
        var levelNames = new List<string> { "Anywhere", "Region", "Data center", "World" };
        var lv = _feedPlaceLevel;
        if (RsElements.Dropdown("feed_place_lv", ref lv, levelNames, width: 130f)) { _feedPlaceLevel = lv; changed = true; }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Only posts for this place (plus posts for everyone). Narrow it down region → data center → world.");
        if (_feedPlaceLevel >= 1)
        {
            ImGui.SameLine();
            var regions = AbsoluteRP.Defines.GameData.GetAllRegions();
            var regionNames = regions.Select(AbsoluteRP.Defines.GameData.GetRegionCode).ToList();
            int ri = Math.Max(0, regions.IndexOf(_feedRegion));
            if (RsElements.Dropdown("feed_place_r", ref ri, regionNames, width: 90f)) { SetFeedRegion(regions[Math.Clamp(ri, 0, regions.Count - 1)]); changed = true; }
        }
        if (_feedPlaceLevel >= 2)
        {
            ImGui.SameLine();
            var dcs = AbsoluteRP.Defines.GameData.GetDataCentersByRegion(_feedRegion);
            var dcNames = dcs.Select(AbsoluteRP.Defines.GameData.GetDataCenterName).ToList();
            int di = Math.Max(0, dcs.IndexOf(_feedDC));
            if (RsElements.Dropdown("feed_place_dc", ref di, dcNames, width: 130f)) { SetFeedDC(dcs[Math.Clamp(di, 0, dcs.Count - 1)]); changed = true; }
        }
        if (_feedPlaceLevel >= 3)
        {
            ImGui.SameLine();
            var worlds = AbsoluteRP.Defines.GameData.GetWorldNamesByDataCenter(_feedDC);
            int wi = Math.Max(0, worlds.FindIndex(w => string.Equals(w, _feedWorld, StringComparison.OrdinalIgnoreCase)));
            if (RsElements.Dropdown("feed_place_w", ref wi, worlds, width: 150f) && worlds.Count > 0) { _feedWorld = worlds[Math.Clamp(wi, 0, worlds.Count - 1)]; changed = true; }
        }
        return changed;
    }

    private string ComposeScopeValue() => _composeScope switch
    {
        SocialScope.Region => AbsoluteRP.Defines.GameData.GetRegionCode(_composeRegion),
        SocialScope.DC     => AbsoluteRP.Defines.GameData.GetRegionCode(_composeRegion) + "/" + AbsoluteRP.Defines.GameData.GetDataCenterName(_composeDC),
        SocialScope.World  => AbsoluteRP.Defines.GameData.GetRegionCode(_composeRegion) + "/" + AbsoluteRP.Defines.GameData.GetDataCenterName(_composeDC) + "/" + _composeWorld,
        _                  => string.Empty,
    };

    private static string PlaceLeaf(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var i = path.LastIndexOf('/');
        return i >= 0 ? path[(i + 1)..] : path;
    }
    private string         _composeTitle      = string.Empty;
    private string         _composeBody       = string.Empty;
    private bool           _composeHasExpiry;
    private int            _composeExpiryHours = 48;
    // Toolbar working state.
    private Vector3        _composeColor      = new Vector3(1f, 0.7f, 0.2f);
    private int            _composeSizePt     = 20;
    private string         _composeImageUrl   = string.Empty;
    private string         _composeVideoUrl   = string.Empty;
    // Upload-in-flight state so the toolbar can disable itself and show a status line. Reset after the SSocialMediaUploaded receiver fires.
    private bool           _uploading;
    private string         _uploadStatus       = string.Empty;

    // Categories shown in the tab bar, in fixed order.
    private static readonly (SocialCategory cat, string label, FontAwesomeIcon icon)[] Categories =
    {
        (SocialCategory.Event,       "Events",      FontAwesomeIcon.CalendarDay),
        (SocialCategory.Venue,       "Venues",      FontAwesomeIcon.MapMarkerAlt),
        (SocialCategory.Recruitment, "Recruitment", FontAwesomeIcon.UserPlus),
        (SocialCategory.LFRP,        "LFRP",        FontAwesomeIcon.CommentDots),
    };

    public void Draw()
    {
        // Tick the per-URL video-thumbnail cache once per frame so its deferred dispose + LRU eviction run before any DrawVideoBlock asks for a texture. Cheap when the cache is empty.
        AbsoluteRP.Social.SocialVideoThumbCache.BeginFrame();

        // Cross-window: the user profile popup queues a post here when its post list is clicked. Consume it once by opening the detail view - nulling the field so it doesn't re-fire.
        var pending = SocialFeed.PendingOpenPost;
        if (pending != null)
        {
            SocialFeed.PendingOpenPost = null;
            OpenDetail(pending);
        }

        // A bell click on the DTR entry gets picked up here so the notifications popup opens on top of whatever tab was showing.
        if (SocialDtrBell.OpenPopupRequested)
        {
            SocialDtrBell.OpenPopupRequested = false;
            _notificationsPopupOpen = true;
            _ = SocialFeed_DS.FetchNotifications(unseenOnly: false, limit: 100);
        }

        // Consume the deferred compose-open flag from a previous frame's header click. Doing it here means the click already resolved.
        if (_requestOpenCompose)
        {
            _requestOpenCompose = false;
            var currentCat = (_selectedTab >= 0 && _selectedTab < Categories.Length)
                             ? Categories[_selectedTab].cat : SocialCategory.Event;
            OpenComposer(currentCat, editId: 0);
            _view = SocialView.Compose;
        }

        // Someone's account profile replaces the panel content (it used to be a popup). Back returns to whatever was showing underneath.
        if (SocialUserProfilePopup.IsOpen && _view != SocialView.Compose)
        {
            SocialUserProfilePopup.DrawInline();
            DrawNotificationsPopup();
            SocialVideoPopup.Draw();
            return;
        }

        // Detail / Compose views replace the feed entirely - no tab bar shown so the panel reads as a dedicated page. A back button in the sub-view returns to Feed.
        if (_view == SocialView.Detail && _detailPost != null)
        {
            DrawDetailView();
            DrawReportUi();   // the reply flags live here, so the report modal must be drawn here too
            DrawNotificationsPopup();
            SocialVideoPopup.Draw(); SocialUserProfilePopup.Draw();
            return;
        }
        if (_view == SocialView.Compose)
        {
            DrawComposeView();
            DrawNotificationsPopup();
            SocialVideoPopup.Draw(); SocialUserProfilePopup.Draw();
            return;
        }

        // top section buttons: Feed / Bookmarks / Connections / Groups
        DrawTopSectionButtons();
        ImGui.Spacing();

        // Route Bookmarks / Connections / Groups. The Connections and Groups sections still live inside the legacy SocialWindow (same hosted content as before) - a follow-up will lift the specific sub-tabs out into first-class RsUI pages.
        if (_topSection == TopSection.Bookmarks)
        {
            DrawBookmarksTab(false);
            DrawNotificationsPopup();
            SocialVideoPopup.Draw(); SocialUserProfilePopup.Draw();
            return;
        }
        if (_topSection == TopSection.Connections)
        {
            DrawConnectionsSection();
            DrawNotificationsPopup();
            SocialVideoPopup.Draw(); SocialUserProfilePopup.Draw();
            return;
        }
        if (_topSection == TopSection.Search)
        {
            DrawProfileSearch();
            DrawNotificationsPopup();
            SocialVideoPopup.Draw(); SocialUserProfilePopup.Draw();
            return;
        }
        if (_topSection == TopSection.Groups)
        {
            DrawGroupsSection();
            DrawNotificationsPopup();
            SocialVideoPopup.Draw(); SocialUserProfilePopup.Draw();
            return;
        }

        // FEED section

        // Row 1: + New Post (primary, always visible) + Refresh flush right.
        if (RsElements.Button("+ New Post", RsElements.ButtonVariant.Primary))
            _requestOpenCompose = true;
        ImGui.SameLine();
        var refreshW = ImGui.CalcTextSize("Refresh").X + RsTheme.S(36f);
        var avail    = ImGui.GetContentRegionAvail().X;
        var offset   = avail - refreshW;
        if (offset > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        if (RsElements.Button("Refresh", RsElements.ButtonVariant.Ghost))
        {
            _feedPage = 0;
            RefreshFeedForFilter();
        }
        DrawReportUi();

        // Row 2: filter dropdown ("All" + each category).
        var filterLabels = new List<string> { "All" };
        for (int i = 0; i < Categories.Length; i++) filterLabels.Add(Categories[i].label);
        if (_feedFilterIndex < 0 || _feedFilterIndex >= filterLabels.Count) _feedFilterIndex = 0;
        var beforeFilter = _feedFilterIndex;

        ImGui.Spacing();
        if (RsElements.Dropdown("feed_filter", ref _feedFilterIndex, filterLabels, width: 180f))
            _feedPage = 0;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Kind of post: events, recruitment, looking for RP…");
        var placeChanged = DrawFeedPlaceFilter();
        if (placeChanged) _feedPage = 0;

        // Fetch on first paint OR whenever a filter changes.
        if (beforeFilter != _feedFilterIndex || placeChanged || _fetchedThisOpen == 0)
        {
            RefreshFeedForFilter();
            _fetchedThisOpen = SocialFeed.NowMs();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Gather the current filter's posts. "All" merges every category in reverse-chronological order so users see one unified stream.
        var posts = GetFilteredPosts();
        // Reset the pager whenever the filter changes so users always land on the newest posts of the new filter, not somewhere mid-history.
        if (_feedFilterIndex != _lastTabForPager)
        {
            _lastTabForPager = _feedFilterIndex;
            _feedPage        = 0;
        }

        var feedAvail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        if (ImGui.BeginChild("##social_feed_scroll", feedAvail, false, ImGuiWindowFlags.None))
        {
            if (posts.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                var lbl = _feedFilterIndex == 0 ? "posts" : Categories[_feedFilterIndex - 1].label.ToLower() + " posts";
                var where = _feedPlaceLevel == 0 ? "" : " for " + PlaceLeaf(FeedFilterPath());
                ImGui.TextWrapped($"No {lbl}{where} yet. Use \"+ New Post\" to start one.");
                ImGui.PopStyleColor();
            }
            else
            {
                var totalPages = (posts.Count + FeedPageSize - 1) / FeedPageSize;
                if (_feedPage >= totalPages) _feedPage = totalPages - 1;
                if (_feedPage < 0)           _feedPage = 0;
                var start = _feedPage * FeedPageSize;
                var end   = Math.Min(posts.Count, start + FeedPageSize);
                for (int i = start; i < end; i++) DrawPostCard(posts[i]);

                if (totalPages > 1)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                    DrawFeedPager(totalPages);
                }
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();

        // Consume the version tick so we don't spin animations forever.
        if (SocialFeed.Version != _feedVersionSeen) _feedVersionSeen = SocialFeed.Version;

        DrawNotificationsPopup();
        SocialVideoPopup.Draw(); SocialUserProfilePopup.Draw();
    }

    // Backwards-compat: legacy Social window inside a sub-tab so groups, connections, search, etc. stay reachable while they get migrated.
    private void DrawLegacy()
    {
        if (SocialWindow.Instance is null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
            ImGui.Text("Log in to load social.");
            ImGui.PopStyleColor();
            return;
        }
        SocialWindow.Instance.DrawContent();
    }

    // Connections section - search bar with a Profiles/Accounts/Groups filter dropdown at the top, then the connections list (or the profile-search UI when a query is entered under the Profiles kind). Skips DrawContent so the legacy side-nav strip never renders. connections list
    private int _connKind;               // 0 connected, 1 requests, 2 sent, 3 blocked
    private long _connAskedMs;

    private void DrawConnectionsList()
    {
        var conn = AbsoluteRP.Windows.Social.Views.Connections.All;
        var now = Environment.TickCount64;
        if (Plugin.character != null && now - _connAskedMs > 15000 && (conn.Count == 0 || now - AbsoluteRP.Windows.Social.Views.Connections.LastReceivedMs > 60000))
        {
            _connAskedMs = now;
            Connections_DS.RequestConnections(Plugin.character);
        }
        const int Pending = (int)UI.ConnectionStatus.pending, Accepted = (int)UI.ConnectionStatus.accepted, Blocked = (int)UI.ConnectionStatus.blocked;
        var connected = conn.Where(c => c.Status == Accepted).ToList();
        var incoming  = conn.Where(c => c.Status == Pending && c.IsReceiver).ToList();
        var sent      = conn.Where(c => c.Status == Pending && !c.IsReceiver).ToList();
        var blocked   = conn.Where(c => c.Status == Blocked).ToList();

        bool Tab(string label, int kind)
        {
            var sz = ImGui.CalcTextSize(label);
            var pad = new Vector2(RsTheme.S(14f), RsTheme.S(8f));
            var pos = ImGui.GetCursorScreenPos();
            var clicked = ImGui.InvisibleButton("##conn_tab_" + kind, sz + pad * 2f);
            var hov = ImGui.IsItemHovered();
            var active = _connKind == kind;
            var dl = ImGui.GetWindowDrawList();
            dl.AddText(pos + pad, ImGui.GetColorU32(active ? RsTheme.TextPrimary : hov ? RsTheme.TextSecondary : RsTheme.TextMuted), label);
            var y = pos.Y + sz.Y + pad.Y * 2f - RsTheme.S(2f);
            dl.AddRectFilled(new Vector2(pos.X, y), new Vector2(pos.X + sz.X + pad.X * 2f, y + RsTheme.S(2f)), active ? RsTheme.U.AccentPrimary : RsTheme.U.Border);
            return clicked;
        }
        if (Tab($"Connected ({connected.Count})", 0)) _connKind = 0;
        ImGui.SameLine(0f, 0f);
        if (Tab($"Requests ({incoming.Count})", 1)) _connKind = 1;
        ImGui.SameLine(0f, 0f);
        if (Tab($"Sent ({sent.Count})", 2)) _connKind = 2;
        ImGui.SameLine(0f, 0f);
        if (Tab($"Blocked ({blocked.Count})", 3)) _connKind = 3;
        ImGui.SameLine();
        var refreshW = ImGui.CalcTextSize("Refresh").X + RsTheme.S(36f);
        var offX = ImGui.GetContentRegionAvail().X - refreshW;
        if (offX > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offX);
        if (RsElements.Button("Refresh##conn_refresh", RsElements.ButtonVariant.Ghost) && Plugin.character != null)
        {
            _connAskedMs = now;
            Connections_DS.RequestConnections(Plugin.character);
        }
        ImGui.Spacing();

        var list = _connKind switch { 1 => incoming, 2 => sent, 3 => blocked, _ => connected };
        if (list.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped(_connKind switch
            {
                1 => "No connection requests waiting.",
                2 => "No requests sent. Ask to connect from a player's profile.",
                3 => "Nobody is blocked.",
                _ => "No connections yet. Ask to connect from a player's profile; connected players can see your private profiles.",
            });
            ImGui.PopStyleColor();
            return;
        }

        var me = Plugin.plugin?.Configuration?.account?.accountName ?? string.Empty;
        var myName = Plugin.plugin?.playername ?? string.Empty;
        var myWorld = Plugin.plugin?.playerworld ?? string.Empty;
        void Act(AbsoluteRP.Windows.Social.Views.Connections.Entry c, UI.ConnectionStatus status)
            => Profiles_DS.SendProfileAccessUpdate(Plugin.character, me, myName, myWorld, c.Name, c.World, (int)status);

        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (!RsElements.BeginPanel("conn_row_" + _connKind + "_" + i, null, fitContentsX: false, fitContentsY: true)) { RsElements.EndPanel(); continue; }
            try
            {
                // Account avatar (also for pending and blocked), then the username.
                var ap = c.UserId > 0 ? SocialAvatar.EnsureProfile(c.UserId) : null;
                var d = RsTheme.S(40f);
                SocialAvatar.DrawCircle(ap?.avatarUrl, d);
                ImGui.SameLine();
                ImGui.BeginGroup();
                var shown = !string.IsNullOrWhiteSpace(ap?.displayName) ? ap!.displayName : !string.IsNullOrWhiteSpace(c.Username) ? c.Username : c.Name;
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                ImGui.TextUnformatted(shown);
                ImGui.PopStyleColor();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted(_connKind switch { 1 => "wants to connect", 2 => "request sent", 3 => "blocked", _ => "connected" });
                ImGui.PopStyleColor();
                ImGui.EndGroup();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{c.Name} · {c.World}");

                ImGui.SameLine();
                var btnW = RsTheme.S(90f);
                int nBtn = _connKind == 1 ? 3 : _connKind == 0 ? 2 : 1;
                var off = ImGui.GetContentRegionAvail().X - (btnW + RsTheme.S(6f)) * nBtn;
                if (off > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + off);
                using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    switch (_connKind)
                    {
                        case 1:
                            if (RsElements.Button($"Accept##ca{i}", RsElements.ButtonVariant.Primary, new Vector2(btnW, 0f))) Act(c, UI.ConnectionStatus.accepted);
                            ImGui.SameLine();
                            if (RsElements.Button($"Decline##cd{i}", RsElements.ButtonVariant.Ghost, new Vector2(btnW, 0f))) Act(c, UI.ConnectionStatus.refused);
                            ImGui.SameLine();
                            if (RsElements.Button($"Block##cb{i}", RsElements.ButtonVariant.Danger, new Vector2(btnW, 0f))) Act(c, UI.ConnectionStatus.blocked);
                            break;
                        case 2:
                            if (RsElements.Button($"Cancel##cc{i}", RsElements.ButtonVariant.Ghost, new Vector2(btnW, 0f))) Act(c, UI.ConnectionStatus.refused);
                            break;
                        case 3:
                            if (RsElements.Button($"Unblock##cu{i}", RsElements.ButtonVariant.Primary, new Vector2(btnW, 0f))) Act(c, UI.ConnectionStatus.refused);
                            break;
                        default:
                            if (RsElements.Button($"Remove##cr{i}", RsElements.ButtonVariant.Ghost, new Vector2(btnW, 0f))) Act(c, UI.ConnectionStatus.refused);
                            ImGui.SameLine();
                            if (RsElements.Button($"Block##cb{i}", RsElements.ButtonVariant.Danger, new Vector2(btnW, 0f))) Act(c, UI.ConnectionStatus.blocked);
                            break;
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Hold Ctrl to enable");
            }
            finally { RsElements.EndPanel(); }
            ImGui.Spacing();
        }
    }

    private void DrawConnectionsSection()
    {
        if (SocialWindow.Instance == null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
            ImGui.Text("Log in to load social.");
            ImGui.PopStyleColor();
            return;
        }

        // Search row: profile search text input + search/clear buttons.
        _searchKindIndex = 0;
        // Fill the row with the text input; leave room for two buttons.
        var searchBtnW = ImGui.CalcTextSize("Search").X + RsTheme.S(28f);
        var clearBtnW  = ImGui.CalcTextSize("Clear").X  + RsTheme.S(28f);
        var trailing   = searchBtnW + clearBtnW + RsTheme.S(12f);
        var availRow   = ImGui.GetContentRegionAvail().X;
        var inputRaw   = MathF.Max(80f, (availRow - trailing) / MathF.Max(0.01f, RsTheme.S(1f)));
        if (RsElements.InputText("conn_search_text", ref _searchQuery, 100,
            placeholder: "Search profiles…", width: inputRaw))
        {
            // Live-search Profiles once the query is long enough - matches the legacy Search view's ergonomics.
            if (_searchKindIndex == 0 && _searchQuery.Trim().Length >= 2)
                SubmitSearch();
        }
        ImGui.SameLine();
        if (RsElements.Button("Search##conn_search", RsElements.ButtonVariant.Primary))
            SubmitSearch();
        ImGui.SameLine();
        if (RsElements.Button("Clear##conn_search_clear", RsElements.ButtonVariant.Ghost))
        {
            _searchQuery     = string.Empty;
            _searchSubmitted = false;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Scrollable body - either the connections list, or search results if the user submitted a query.
        var childAvail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        if (ImGui.BeginChild("##conn_body", childAvail, false, ImGuiWindowFlags.None))
        {
            if (_searchSubmitted && !string.IsNullOrWhiteSpace(_searchQuery))
            {
                switch (_searchKindIndex)
                {
                    case 0:  // Profiles - hand off to the existing Search view.
                        // Seed its query fields so its own input reflects what the user typed up top, then render.
                        try { AbsoluteRP.Windows.Social.Views.Search.profileSearchQuery = _searchQuery; } catch { }
                        AbsoluteRP.Windows.Social.Views.Search.LoadSearch();
                        break;
                    case 1:  // Accounts
                    case 2:  // Groups
                        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                        ImGui.TextWrapped(_searchKindIndex == 1
                            ? "Account search coming soon — for now, use Profiles to find people."
                            : "Group search coming soon — browse the Groups tab in the meantime.");
                        ImGui.PopStyleColor();
                        break;
                }
            }
            else
            {
                DrawConnectionsList();
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();

        AbsoluteRP.Windows.Social.Views.GroupsData.ProcessPendingDisposals();
    }

    // Groups section - direct entry into the existing groups list from the legacy SocialWindow, minus the side-nav column. profile search
    private int _psRegion, _psDc, _psWorld;          // _psWorld 0 = any world
    private int _psCategory, _psPageSizeIdx = 1, _psPage = 1;
    private string _psName = string.Empty;
    private static readonly int[] PsPageSizes = { 6, 12, 24, 48 };

    private void RunProfileSearch()
    {
        if (Plugin.character == null) return;
        var q = string.IsNullOrWhiteSpace(_psName) ? "ALL PROFILES" : _psName.Trim();
        AbsoluteRP.Windows.Social.Views.Search.profileSearchQuery = q;
        AbsoluteRP.Windows.Listings.SocialWindow.isSearchLoading = true;
        AbsoluteRP.Windows.Listings.SocialWindow.searchLoadedCount = 0;
        AbsoluteRP.Windows.Listings.SocialWindow.searchTotalCount = 0;
        AbsoluteRP.Windows.Listings.SocialWindow.listings.Clear();
        Listings_DS.RequestPersonals(Plugin.character, AbsoluteRP.Windows.Social.Views.Search.worldSearchQuery, _psPage,
            PsPageSizes[Math.Clamp(_psPageSizeIdx, 0, PsPageSizes.Length - 1)], q, _psCategory);
    }

    private void DrawProfileSearch()
    {
        var S = (Func<float, float>)RsTheme.S;

        // Filters card.
        if (RsElements.BeginPanel("ps_filters", "Find profiles", fitContentsX: false, fitContentsY: true))
        {
            try
            {
                var regions = AbsoluteRP.Defines.GameData.GetAllRegions();
                _psRegion = Math.Clamp(_psRegion, 0, Math.Max(0, regions.Count - 1));
                var dcs = regions.Count > 0 ? AbsoluteRP.Defines.GameData.GetDataCentersByRegion(regions[_psRegion]) : new List<AbsoluteRP.Defines.FFXIVDataCenter>();
                _psDc = Math.Clamp(_psDc, 0, Math.Max(0, dcs.Count - 1));
                var worlds = new List<string> { "Any world" };
                if (dcs.Count > 0) worlds.AddRange(AbsoluteRP.Defines.GameData.GetWorldNamesByDataCenter(dcs[_psDc]));
                _psWorld = Math.Clamp(_psWorld, 0, worlds.Count - 1);

                void Label(string s) { ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted); ImGui.TextUnformatted(s); ImGui.PopStyleColor(); }

                ImGui.BeginGroup(); Label("REGION");
                if (RsElements.Dropdown("ps_region", ref _psRegion, regions.ConvertAll(AbsoluteRP.Defines.GameData.GetRegionName), 150f)) { _psDc = 0; _psWorld = 0; }
                ImGui.EndGroup(); ImGui.SameLine(0f, S(12f));
                ImGui.BeginGroup(); Label("DATA CENTER");
                if (RsElements.Dropdown("ps_dc", ref _psDc, dcs.ConvertAll(AbsoluteRP.Defines.GameData.GetDataCenterName), 150f)) _psWorld = 0;
                ImGui.EndGroup(); ImGui.SameLine(0f, S(12f));
                ImGui.BeginGroup(); Label("WORLD");
                RsElements.Dropdown("ps_world", ref _psWorld, worlds, 150f);
                ImGui.EndGroup();
                AbsoluteRP.Windows.Social.Views.Search.worldSearchQuery = _psWorld == 0 ? string.Empty : worlds[_psWorld];

                ImGui.Spacing();
                ImGui.BeginGroup(); Label("PROFILE NAME");
                var submit = RsElements.InputText("ps_name", ref _psName, 100, placeholder: "Leave empty for every profile", width: 260f);
                ImGui.EndGroup(); ImGui.SameLine(0f, S(12f));
                ImGui.BeginGroup(); Label("CATEGORY");
                var cats = UI.ListingCategorySearchVals.Select(c => c.Item1).ToList();
                _psCategory = Math.Clamp(_psCategory, 0, cats.Count - 1);
                RsElements.Dropdown("ps_cat", ref _psCategory, cats, 150f);
                ImGui.EndGroup(); ImGui.SameLine(0f, S(12f));
                ImGui.BeginGroup(); Label("PER PAGE");
                RsElements.Dropdown("ps_pagesize", ref _psPageSizeIdx, PsPageSizes.Select(n => n.ToString()).ToList(), 80f);
                ImGui.EndGroup();

                ImGui.Spacing();
                if (RsElements.Button("Search##ps_go", RsElements.ButtonVariant.Primary) || (submit && ImGui.IsKeyPressed(ImGuiKey.Enter)))
                {
                    _psPage = 1;
                    RunProfileSearch();
                }
                ImGui.SameLine();
                if (RsElements.Button("Clear##ps_clear", RsElements.ButtonVariant.Ghost))
                {
                    _psName = string.Empty; _psCategory = 0; _psWorld = 0; _psPage = 1;
                    AbsoluteRP.Windows.Listings.SocialWindow.listings.Clear();
                }
            }
            finally { RsElements.EndPanel(); }
        }
        else RsElements.EndPanel();
        ImGui.Spacing();

        var listings = AbsoluteRP.Windows.Listings.SocialWindow.listings;
        if (AbsoluteRP.Windows.Listings.SocialWindow.isSearchLoading)
        {
            var total = AbsoluteRP.Windows.Listings.SocialWindow.searchTotalCount;
            var done = AbsoluteRP.Windows.Listings.SocialWindow.searchLoadedCount;
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(total > 0 ? $"Loading profiles {done} / {total}…" : "Searching…");
            ImGui.PopStyleColor();
            var w = ImGui.GetContentRegionAvail().X; var p0 = ImGui.GetCursorScreenPos(); var h = S(6f);
            var dl0 = ImGui.GetWindowDrawList();
            dl0.AddRectFilled(p0, p0 + new Vector2(w, h), RsTheme.U.BgTertiary, h * 0.5f);
            if (total > 0) dl0.AddRectFilled(p0, p0 + new Vector2(w * Math.Clamp(done / (float)total, 0f, 1f), h), RsTheme.U.AccentPrimary, h * 0.5f);
            ImGui.Dummy(new Vector2(w, h));
            return;
        }
        var results = listings.Where(l => l.type == AbsoluteRP.Windows.Social.Views.Search.type).ToList();
        if (results.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("No profiles to show. Pick a place and press Search; leave the name empty to list every public profile there.");
            ImGui.PopStyleColor();
            return;
        }

        // Pager.
        if (_psPage > 1 && RsElements.Button("← Previous##ps_prev", RsElements.ButtonVariant.Ghost)) { _psPage--; RunProfileSearch(); }
        if (_psPage > 1) ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted); ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted($"Page {_psPage}"); ImGui.PopStyleColor();
        ImGui.SameLine();
        if (RsElements.Button("Next →##ps_next", RsElements.ButtonVariant.Ghost)) { _psPage++; RunProfileSearch(); }
        ImGui.Spacing();

        // Result cards in a grid.
        var avail = ImGui.GetContentRegionAvail().X;
        var cardW = S(250f); var gap = S(12f);
        int cols = Math.Max(1, (int)((avail + gap) / (cardW + gap)));
        cardW = (avail - gap * (cols - 1)) / cols;
        var cardH = S(210f);
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        for (int i = 0; i < results.Count; i++)
        {
            var l = results[i];
            var min = origin + new Vector2((i % cols) * (cardW + gap), (i / cols) * (cardH + gap));
            var max = min + new Vector2(cardW, cardH);
            var col = l.color.W <= 0.01f ? RsTheme.AccentPrimary : new Vector4(l.color.X, l.color.Y, l.color.Z, 1f);
            var r = S(10f);
            dl.AddRectFilled(min, max, RsTheme.U.BgSecondary, r);
            dl.AddRect(min, max, RsTheme.U.Border, r, ImDrawFlags.None, RsTheme.BorderThickness);
            // A band of the profile's colour across the top.
            dl.AddRectFilledMultiColor(min + new Vector2(1f, 1f), new Vector2(max.X - 1f, min.Y + S(46f)),
                ImGui.GetColorU32(new Vector4(col.X, col.Y, col.Z, 0.35f)), ImGui.GetColorU32(new Vector4(col.X, col.Y, col.Z, 0.35f)),
                ImGui.GetColorU32(new Vector4(col.X, col.Y, col.Z, 0f)), ImGui.GetColorU32(new Vector4(col.X, col.Y, col.Z, 0f)));

            // Avatar, ringed in the profile colour.
            var ar = S(34f);
            var ac = new Vector2((min.X + max.X) * 0.5f, min.Y + S(18f) + ar);
            dl.AddCircleFilled(ac, ar + S(3f), ImGui.GetColorU32(col), 48);
            if (l.avatar != null && l.avatar.Handle != IntPtr.Zero)
                dl.AddImageRounded(l.avatar.Handle, ac - new Vector2(ar), ac + new Vector2(ar), Vector2.Zero, Vector2.One, 0xFFFFFFFF, ar);
            else dl.AddCircleFilled(ac, ar, RsTheme.U.BgTertiary, 48);

            // Name in the profile colour, then spoiler chips.
            var name = string.IsNullOrWhiteSpace(l.name) ? "(untitled)" : l.name;
            var inner = cardW - S(20f);
            while (name.Length > 3 && ImGui.CalcTextSize(name).X > inner) name = name[..^2] + "…";
            var ns = ImGui.CalcTextSize(name);
            var ny = ac.Y + ar + S(10f);
            dl.AddText(new Vector2(ac.X - ns.X * 0.5f, ny), ImGui.GetColorU32(col), name);
            var tags = new List<string>();
            if (l.ARR) tags.Add("ARR"); if (l.HW) tags.Add("HW"); if (l.SB) tags.Add("SB");
            if (l.SHB) tags.Add("ShB"); if (l.EW) tags.Add("EW"); if (l.DT) tags.Add("DT");
            var ty = ny + ns.Y + S(6f);
            if (tags.Count > 0)
            {
                var chipW = tags.Sum(x => ImGui.CalcTextSize(x).X + S(12f)) + (tags.Count - 1) * S(4f);
                var cx = ac.X - chipW * 0.5f;
                foreach (var tag in tags)
                {
                    var tw = ImGui.CalcTextSize(tag).X + S(12f);
                    var th = ImGui.GetTextLineHeight() + S(2f);
                    dl.AddRectFilled(new Vector2(cx, ty), new Vector2(cx + tw, ty + th), ImGui.GetColorU32(new Vector4(RsTheme.AccentWarning.X, RsTheme.AccentWarning.Y, RsTheme.AccentWarning.Z, 0.18f)), th * 0.5f);
                    dl.AddText(new Vector2(cx + S(6f), ty + S(1f)), ImGui.GetColorU32(RsTheme.AccentWarning), tag);
                    cx += tw + S(4f);
                }
            }
            else
            {
                var none = "No spoilers";
                var nsz = ImGui.CalcTextSize(none);
                dl.AddText(new Vector2(ac.X - nsz.X * 0.5f, ty), RsTheme.U.TextMuted, none);
            }

            // Buttons along the bottom.
            var bw = (cardW - S(30f)) * 0.5f;
            ImGui.SetCursorScreenPos(new Vector2(min.X + S(10f), max.Y - S(40f)));
            if (RsElements.Button($"View##ps_view_{l.id}", RsElements.ButtonVariant.Primary, new Vector2(bw, 0f)))
            {
                Plugin.plugin.OpenTargetWindow();
                AbsoluteRP.Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.RequestingProfile = true;
                AbsoluteRP.Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.ResetAllData();
                Profiles_DS.FetchProfile(Plugin.character, false, -1, string.Empty, string.Empty, l.id);
            }
            ImGui.SameLine(0f, S(10f));
            if (RsElements.Button($"Bookmark##ps_bm_{l.id}", RsElements.ButtonVariant.Ghost, new Vector2(bw, 0f)))
                Profiles_DS.BookmarkPlayer(Plugin.character, string.Empty, string.Empty, l.id);
        }
        int rows = (results.Count + cols - 1) / cols;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(avail, rows * (cardH + gap)));
    }

    private void DrawGroupsSection()
    {
        if (SocialWindow.Instance == null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
            ImGui.Text("Log in to load social.");
            ImGui.PopStyleColor();
            return;
        }
        // Route directly to the groups panel - no DrawContent means the legacy side-nav strip drawn by that path never appears here.
        AbsoluteRP.Windows.Social.Views.GroupsData.LoadGroupList();
        AbsoluteRP.Windows.Social.Views.GroupsData.ProcessPendingDisposals();
    }

    private void SubmitSearch()
    {
        _searchSubmitted = !string.IsNullOrWhiteSpace(_searchQuery);
        if (!_searchSubmitted) return;
        // Only Profiles has a live server search flow today; the other kinds show a placeholder message inside the body.
        if (_searchKindIndex == 0)
        {
            try
            {
                AbsoluteRP.Windows.Social.Views.Search.profileSearchQuery = _searchQuery;
                // Reset to page 0 for a new query - RequestPersonals will fill the results list which Search.LoadSearch renders.
                AbsoluteRP.Network.Listings_DS.RequestPersonals(
                    Plugin.character,
                    AbsoluteRP.Windows.Social.Views.Search.worldSearchQuery,
                    0, 20, _searchQuery, 0);
            }
            catch (Exception ex) { Plugin.PluginLog?.Debug("Profile search dispatch: " + ex.Message); }
        }
    }

    // Report modal + a short-lived line with the server's answer.
    private void DrawReportUi()
    {
        DrawReportPopup();
        if (!string.IsNullOrEmpty(SocialFeed.ReportMessage) && SocialFeed.NowMs() - SocialFeed.ReportMessageAt < 6000)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, SocialFeed.ReportOk ? RsTheme.AccentSuccess : RsTheme.AccentDanger);
            ImGui.TextUnformatted(SocialFeed.ReportMessage);
            ImGui.PopStyleColor();
        }
    }

    private void FetchCurrentTab(SocialCategory cat)
    {
        // The place filter travels as a path prefix; empty = anywhere.
        _ = SocialFeed_DS.Fetch(cat, "", "", offset: 0, limit: 50, filterPath: FeedFilterPath());
    }

    // Fires a Fetch for whatever the feed filter is currently pointed at. "All" fans out to every category so the merged view has something to render - each one caches independently in SocialFeed.
    private void RefreshFeedForFilter()
    {
        if (_feedFilterIndex == 0)
        {
            for (int i = 0; i < Categories.Length; i++)
                FetchCurrentTab(Categories[i].cat);
        }
        else
        {
            FetchCurrentTab(Categories[_feedFilterIndex - 1].cat);
        }
    }

    // Returns the current filter's post list. For "All", merges every category's cached posts and sorts by CreatedAt descending so newer posts float to the top of the unified feed.
    private static List<SocialPost> GetFilteredPosts()
    {
        // Redirect through the same read path the previous per-tab code used so cache invalidations still bubble through untouched.
        if (_currentFilterIsAll)
        {
            var merged = new List<SocialPost>();
            for (int i = 0; i < Categories.Length; i++)
            {
                var slice = SocialFeed.GetCached(Categories[i].cat, SocialScope.All, string.Empty);
                if (slice != null) merged.AddRange(slice);
            }
            // Deduplicate by post Id - a post's category may match only one slice today, but this future-proofs against multi-cat tagging.
            var seen = new HashSet<long>();
            merged.RemoveAll(p => !seen.Add(p.Id));
            merged.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
            return merged;
        }
        return SocialFeed.GetCached(_currentFilterCat, SocialScope.All, string.Empty);
    }

    // Static caches populated before GetFilteredPosts runs so the helper itself can be static (avoids capturing 'this' through a lambda).
    private static bool           _currentFilterIsAll;
    private static SocialCategory _currentFilterCat;

    // Draws the top-of-page section buttons: Feed / Bookmarks / Connections / Groups. Each is a highlighted toggle button so users see which section they're currently viewing.
    private void DrawTopSectionButtons()
    {
        // Sync the "which filter is active right now" statics before any Get* call in this Draw pass reads them.
        _currentFilterIsAll = _feedFilterIndex == 0;
        _currentFilterCat   = _feedFilterIndex == 0 ? SocialCategory.Event
                                                    : Categories[_feedFilterIndex - 1].cat;

        var sections = new (TopSection s, string label, FontAwesomeIcon icon)[]
        {
            (TopSection.Feed,        "Feed",        FontAwesomeIcon.Stream),
            (TopSection.Bookmarks,   "Bookmarks",   FontAwesomeIcon.Bookmark),
            (TopSection.Connections, "Connections", FontAwesomeIcon.UserFriends),
            (TopSection.Groups,      "Groups",      FontAwesomeIcon.Users),
            (TopSection.Search,      "Search Profiles", FontAwesomeIcon.Search),
        };
        for (int i = 0; i < sections.Length; i++)
        {
            var s = sections[i];
            var active = _topSection == s.s;
            if (SectionToggle(s.label, s.icon, active, idSuffix: i.ToString()))
                _topSection = s.s;
            if (i < sections.Length - 1) ImGui.SameLine();
        }
    }

    // Toggle-style button: primary variant when active, ghost otherwise. Icon renders in FontAwesome; label follows in the current font. Label MUST be the display text only - the ID suffix is appended internally so callers don't have to escape it.
    private static bool SectionToggle(string label, FontAwesomeIcon icon, bool active, string idSuffix)
    {
        var variant = active ? RsElements.ButtonVariant.Primary : RsElements.ButtonVariant.Ghost;
        var iconStr = icon.ToIconString();
        Vector2 iconSz;
        using (AbsoluteRP.RsUI.RsIcons.Push())
            iconSz = ImGui.CalcTextSize(iconStr);
        var textSz = ImGui.CalcTextSize(label);
        // Bigger padding so these read as prominent nav buttons, not hairline chips. Vertical padding intentionally generous.
        var padX = RsTheme.S(14f);
        var padY = RsTheme.S(10f);
        var gap  = RsTheme.S(8f);
        var total = new Vector2(
            padX * 2f + iconSz.X + gap + textSz.X,
            MathF.Max(iconSz.Y, textSz.Y) + padY * 2f);

        var start   = ImGui.GetCursorScreenPos();
        // Button label is a pure ID (empty display + hidden id suffix) so the raw sentinel never renders as visible text.
        var clicked = RsElements.Button("##topsec_" + idSuffix, variant, total);
        var dl      = ImGui.GetWindowDrawList();
        var iconPos = new Vector2(start.X + padX, start.Y + (total.Y - iconSz.Y) * 0.5f);
        var textPos = new Vector2(iconPos.X + iconSz.X + gap, start.Y + (total.Y - textSz.Y) * 0.5f);
        var col     = ImGui.ColorConvertFloat4ToU32(RsTheme.TextPrimary);
        using (AbsoluteRP.RsUI.RsIcons.Push())
            dl.AddText(iconPos, col, iconStr);
        dl.AddText(textPos, col, label);
        return clicked;
    }

    // Bottom-of-feed pager. Previous / page indicator / Next laid out as a single centered row. Prev disables on page 0, Next disables on the last page - matches the standard forum-pager pattern.
    private void DrawFeedPager(int totalPages)
    {
        var buttonW = RsTheme.S(96f);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var lbl     = $"Page {_feedPage + 1} / {totalPages}";
        var lblSz   = ImGui.CalcTextSize(lbl);
        var rowW    = buttonW * 2f + lblSz.X + spacing * 2f + RsTheme.S(16f);
        var availX  = ImGui.GetContentRegionAvail().X;
        var offset  = MathF.Max(0f, (availX - rowW) * 0.5f);
        if (offset > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        var atFirst = _feedPage <= 0;
        var atLast  = _feedPage >= totalPages - 1;

        if (atFirst) ImGui.BeginDisabled();
        if (RsElements.Button("← Previous##feed_prev",
                              RsElements.ButtonVariant.Ghost,
                              new Vector2(buttonW, 0f)))
            _feedPage--;
        if (atFirst) ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        // Vertically align the page label with the button text.
        var yBefore = ImGui.GetCursorPosY();
        ImGui.SetCursorPosY(yBefore + ImGui.GetStyle().FramePadding.Y);
        ImGui.TextUnformatted(lbl);
        ImGui.SetCursorPosY(yBefore);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(lblSz.X, 0f));
        ImGui.SameLine();

        if (atLast) ImGui.BeginDisabled();
        if (RsElements.Button("Next →##feed_next",
                              RsElements.ButtonVariant.Ghost,
                              new Vector2(buttonW, 0f)))
            _feedPage++;
        if (atLast) ImGui.EndDisabled();
    }

    // post card

    private void DrawPostCard(SocialPost post)
    {
        var id = "post_" + post.Id;
        // Capture the panel's outer bounds up front - RsElements.EndPanel trailing Dummy is zero-height, so GetItemRectMin/Max after it doesn't cover the card. We reconstruct min from the cursor at BeginPanel time and max from width + cursor after EndPanel.
        var cardMin   = ImGui.GetCursorScreenPos();
        var cardWidth = ImGui.GetContentRegionAvail().X;
        if (!RsElements.BeginPanel(id, title: null, fitContentsX: false, fitContentsY: true))
        {
            RsElements.EndPanel();
            return;
        }
        try
        {
            // Red flag, top-right, drawn INSIDE the card (the card is its own child window, so anything drawn after it sits underneath).
            DrawReportFlag(post, ImGui.GetCursorScreenPos(), ImGui.GetContentRegionAvail().X);

            // Header: avatar (circular) + names + prefs pill.
            DrawPostAuthorHeader(post);

            ImGui.Spacing();

            // Title.
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(post.Title) ? "(no title)" : post.Title);
            ImGui.PopStyleColor();

            // Meta row: category - scope - posted-ago (author moved to header).
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            var meta = $"{CategoryLabel(post.Category)}  •  {ScopeLabel(post)}  •  {RelativeTime(post.CreatedAt)}";
            ImGui.TextUnformatted(meta);
            if (post.ExpiresAt > 0)
            {
                var msLeft = post.ExpiresAt - SocialFeed.NowMs();
                ImGui.TextUnformatted(msLeft > 0
                    ? $"expires {RelativeFuture(msLeft)}"
                    : "expired");
            }
            ImGui.PopStyleColor();

            if (!string.IsNullOrEmpty(post.Body))
            {
                ImGui.Spacing();

                // Render the ACTUAL post body (BBCode + inline media) via SocialMarkup, clipped to a fixed max height. Overflow fades into the card's background so long posts feel trimmed rather than hard-cut. Clicking anywhere on the card still opens the full detail view.
                var previewMaxH = RsTheme.S(180f);
                var previewMin  = ImGui.GetCursorScreenPos();
                var availW      = ImGui.GetContentRegionAvail().X;
                var previewMax  = new Vector2(previewMin.X + availW, previewMin.Y + previewMaxH);

                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
                if (ImGui.BeginChild("##post_prev_" + post.Id,
                                      new Vector2(availW, previewMaxH),
                                      false,
                                      ImGuiWindowFlags.NoScrollbar
                                    | ImGuiWindowFlags.NoScrollWithMouse
                                    | ImGuiWindowFlags.NoInputs))
                {
                    SocialMarkup.Render(post.Body);
                }
                // Was the rendered content taller than the clip window? Compare via the child window's scroll-max, which is populated even when the child hides its scrollbar.
                bool overflowed;
                try { overflowed = ImGui.GetScrollMaxY() > 0f; }
                catch { overflowed = false; }
                ImGui.EndChild();
                ImGui.PopStyleColor();

                if (overflowed)
                {
                    // Bottom gradient overlay from transparent -> card bg so the trimmed body reads as fading out.
                    var dl      = ImGui.GetWindowDrawList();
                    var fadeH   = MathF.Min(previewMaxH * 0.55f, RsTheme.S(72f));
                    var fadeTop = new Vector2(previewMin.X, previewMax.Y - fadeH);
                    var bg      = RsTheme.BgPrimary;
                    var top     = ImGui.ColorConvertFloat4ToU32(new Vector4(bg.X, bg.Y, bg.Z, 0f));
                    var bot     = ImGui.ColorConvertFloat4ToU32(new Vector4(bg.X, bg.Y, bg.Z, 0.98f));
                    dl.AddRectFilledMultiColor(fadeTop, previewMax, top, top, bot, bot);

                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("(click to read more)");
                    ImGui.PopStyleColor();
                }
            }

            // Reaction row - counts + like/repost buttons. Buttons stay inside the whole-card click area, but ImGui gives them hover/click priority because they're real items, so the whole-card click check skips them via `IsAnyItemActive`.
            ImGui.Spacing();
            DrawReactionRow(post, prefix: "fd");
        }
        finally
        {
            RsElements.EndPanel();
        }

        // Whole-card click -> open detail modal. Bounds are the panel's outer rect (we captured cardMin before Begin; cardMax uses the width available then, and the cursor Y right after EndPanel).
        var cardMax = new Vector2(cardMin.X + cardWidth, ImGui.GetCursorScreenPos().Y);
        bool hovering = ImGui.IsMouseHoveringRect(cardMin, cardMax);
        if (hovering)
        {
            // Subtle hover overlay so the card visibly reads as clickable.
            var dl   = ImGui.GetWindowDrawList();
            var over = new Vector4(1f, 1f, 1f, 0.04f);
            dl.AddRectFilled(cardMin, cardMax, ImGui.ColorConvertFloat4ToU32(over), RsTheme.S(8f));
            dl.AddRect      (cardMin, cardMax, ImGui.ColorConvertFloat4ToU32(RsTheme.AccentPrimary), RsTheme.S(8f), ImDrawFlags.None, 1.5f);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemActive())
        {
            OpenDetail(post);
        }
        ImGui.Spacing();
    }

    // reporting
    private int    _reportPostId;
    // The server grants feed moderation to anyone who can warn accounts.
    private static bool IsFeedModerator => Accounts_DR.permissions != null
        && (Accounts_DR.permissions.can_warn || Accounts_DR.permissions.can_suspend || Accounts_DR.permissions.can_ban);
    private int    _reportCommentId;   // 0 = the post itself, else a single reply
    private int    _reportReasonIdx;
    private string _reportDetails = string.Empty;
    private bool   _reportOpenRequested;
    private static readonly string[] ReportReasons = { "Inappropriate", "Harassment", "NSFW", "Spam", "Impersonation", "Other" };

    // Red flag in the card's top-right corner. Sits over the card as its own item, so clicking it never opens the post detail.
    private void DrawReportFlag(SocialPost post, Vector2 contentMin, float contentWidth)
    {
        var myId = Plugin.plugin?.Configuration?.account?.userID ?? 0;
        var mine = post.AuthorUserID != 0 && post.AuthorUserID == myId;
        var reported = SocialFeed.ReportedPostIds.Contains(post.Id);
        const float sizeRaw = 24f;                       // IconButton scales this itself
        var size = RsTheme.S(sizeRaw);
        var after = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(contentMin.X + contentWidth - size, contentMin.Y));
        var disabled = mine || reported;
        if (disabled) ImGui.BeginDisabled();
        // Red flag glyph on a quiet button.
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
        var clicked = RsElements.IconButton(FontAwesomeIcon.Flag, "flag_" + post.Id, RsElements.ButtonVariant.Ghost, sizeRaw);
        ImGui.PopStyleColor();
        if (disabled) ImGui.EndDisabled();
        if (clicked && !disabled)
        {
            _reportPostId = post.Id;
            _reportCommentId = 0;
            _reportReasonIdx = 0;
            _reportDetails = string.Empty;
            _reportOpenRequested = true;
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(mine ? "Report (you can't report your own post)" : reported ? "Report (already sent)" : "Report");
        ImGui.SetCursorScreenPos(after);
    }

    // Modal with a reason + details; sends the report to the server, which files it for the moderators and posts it to Discord.
    private void DrawReportPopup()
    {
        var popup = _reportCommentId > 0 ? "Report reply##social_report" : "Report post##social_report";
        if (_reportOpenRequested) { ImGui.OpenPopup(popup); _reportOpenRequested = false; }
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(RsTheme.S(420f), 0f), ImGuiCond.Appearing);
        var open = true;
        if (!ImGui.BeginPopupModal(popup, ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)) return;
        try
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped(_reportCommentId > 0
                ? "The moderators will see this reply, the post it was left on, and anything you add here."
                : "The moderators will see the post, its pictures and videos, and anything you add here.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Reason"); ImGui.SameLine();
            RsElements.Dropdown("rep_reason", ref _reportReasonIdx, ReportReasons, RsTheme.S(200f));
            ImGui.Spacing();
            ImGui.TextUnformatted("Why are you reporting this? (optional)");
            RsElements.InputTextArea("rep_details", ref _reportDetails, 1000, "Add a comment for the moderators…", new Vector2(RsTheme.S(390f), RsTheme.S(90f)));
            ImGui.Spacing();
            if (RsElements.Button("Send report", RsElements.ButtonVariant.Danger, new Vector2(RsTheme.S(130f), 0f)))
            {
                _ = SocialFeed_DS.ReportPost(_reportPostId, ReportReasons[Math.Clamp(_reportReasonIdx, 0, ReportReasons.Length - 1)], _reportDetails.Trim(), _reportCommentId);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (RsElements.Button("Cancel", RsElements.ButtonVariant.Ghost, new Vector2(RsTheme.S(100f), 0f))) ImGui.CloseCurrentPopup();
        }
        finally { ImGui.EndPopup(); }
    }

    // Reaction row: bare clickable FontAwesome icons + counts. No button frames - icons color-swap on toggle so the row reads as inline reactions rather than a toolbar. Emoji glyphs don't render in ImGui's default font so we use FontAwesome via RsIcons.Push().
    private void DrawReactionRow(SocialPost post, string prefix)
    {
        bool isMine = post.AuthorUserID != 0
                      && post.AuthorUserID == (Plugin.plugin?.Configuration?.account?.userID ?? 0);

        // Passive comment count - icon in muted color, no click handler.
        DrawIconWithCount("cm_" + prefix + post.Id, FontAwesomeIcon.Comment,
                          post.CommentCount, active: false,
                          activeColor:   RsTheme.AccentPrimary,
                          inactiveColor: RsTheme.TextMuted,
                          onClick: null);
        ImGui.SameLine(0, RsTheme.S(18f));

        // Clickable heart - flips filled/hollow via glyph choice and tints red-active / muted-inactive.
        DrawIconWithCount("lk_" + prefix + post.Id,
                          post.ViewerLiked ? FontAwesomeIcon.Heart : FontAwesomeIcon.HeartBroken,
                          post.LikeCount,
                          active: post.ViewerLiked,
                          activeColor:   new System.Numerics.Vector4(1.00f, 0.35f, 0.45f, 1f),
                          inactiveColor: RsTheme.TextMuted,
                          onClick: () => _ = SocialFeed.ToggleLike(post.Id, !post.ViewerLiked));
        ImGui.SameLine(0, RsTheme.S(18f));

        // Repost - Retweet glyph, green-active. Hidden on own posts since the server rejects self-repost; authors still see the count as a passive icon so they know how many reposts they got.
        if (!isMine)
        {
            DrawIconWithCount("rp_" + prefix + post.Id, FontAwesomeIcon.Retweet,
                              post.RepostCount,
                              active: post.ViewerReposted,
                              activeColor:   new System.Numerics.Vector4(0.36f, 0.85f, 0.45f, 1f),
                              inactiveColor: RsTheme.TextMuted,
                              onClick: () => _ = SocialFeed.ToggleRepost(post.Id, !post.ViewerReposted));
        }
        else
        {
            DrawIconWithCount("rp_" + prefix + post.Id, FontAwesomeIcon.Retweet,
                              post.RepostCount, active: false,
                              activeColor:   RsTheme.AccentPrimary,
                              inactiveColor: RsTheme.TextMuted,
                              onClick: null);
        }

        // Save - just the star, lit when the post is in your bookmarks.
        ImGui.SameLine(0, RsTheme.S(18f));
        bool saved = SocialFeed.BookmarkedPostIds.Contains(post.Id);
        DrawIconWithCount("bm_" + prefix + post.Id, FontAwesomeIcon.Star, -1,
                          active: saved,
                          activeColor:   RsTheme.AccentWarning,
                          inactiveColor: RsTheme.TextMuted,
                          onClick: () => _ = SocialFeed_DS.ToggleBookmark(post.Id, !saved));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(saved ? "Saved — click to remove" : "Save post");
    }

    // Renders one icon + count as an inline "chip". If `onClick` is non-null, the whole hit-area is a click target that hover-highlights and calls the delegate. If null, the chip is passive.
    private static void DrawIconWithCount(string id, FontAwesomeIcon icon, int count,
                                          bool active,
                                          System.Numerics.Vector4 activeColor,
                                          System.Numerics.Vector4 inactiveColor,
                                          Action? onClick)
    {
        var glyph = icon.ToIconString();
        System.Numerics.Vector2 iconSize;
        using (AbsoluteRP.RsUI.RsIcons.Push()) iconSize = ImGui.CalcTextSize(glyph);
        var countText = count < 0 ? string.Empty : count.ToString();
        var countSize = ImGui.CalcTextSize(countText);
        var gap       = RsTheme.S(4f);
        var padX      = RsTheme.S(3f);
        var rowH      = MathF.Max(iconSize.Y, countSize.Y);
        var rowW      = count < 0 ? iconSize.X : iconSize.X + gap + countSize.X;
        var start     = ImGui.GetCursorScreenPos();
        var draw      = ImGui.GetWindowDrawList();

        // Hit-area covers both icon and count when interactive.
        bool hovered = false;
        if (onClick != null)
        {
            ImGui.InvisibleButton("##rx_" + id, new System.Numerics.Vector2(rowW + padX * 2f, rowH));
            hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) onClick();
            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        // Color: active -> activeColor. Hover on inactive -> slight brighten.
        var col = active
            ? activeColor
            : (hovered ? new System.Numerics.Vector4(inactiveColor.X + 0.15f, inactiveColor.Y + 0.15f, inactiveColor.Z + 0.15f, 1f)
                       : inactiveColor);
        var colU = ImGui.ColorConvertFloat4ToU32(col);

        var iconPos = new System.Numerics.Vector2(start.X + padX, start.Y + (rowH - iconSize.Y) * 0.5f);
        using (AbsoluteRP.RsUI.RsIcons.Push()) draw.AddText(iconPos, colU, glyph);
        var textPos = new System.Numerics.Vector2(iconPos.X + iconSize.X + gap, start.Y + (rowH - countSize.Y) * 0.5f);
        if (count >= 0) draw.AddText(textPos, colU, countText);

        // Advance cursor past the chip when we DIDN'T draw an invisible button (passive chip). InvisibleButton already advances layout.
        if (onClick == null)
        {
            ImGui.Dummy(new System.Numerics.Vector2(rowW + padX * 2f, rowH));
        }
    }

    // Author header: circular avatar left, display name + smaller account name stacked to its right, an RP-preferences pill below that summarises the author's rp-prefs on hover. Falls back to just the account name string when the profile hasn't been fetched yet (a request is fired via SocialAvatar.EnsureProfile).
    private static void DrawPostAuthorHeader(SocialPost post)
    {
        var profile = AbsoluteRP.Social.SocialAvatar.EnsureProfile(post.AuthorUserID);
        var displayName = !string.IsNullOrWhiteSpace(profile?.displayName)
            ? profile!.displayName
            : (!string.IsNullOrWhiteSpace(post.AuthorName) ? post.AuthorName : "unknown");
        var accountName = post.AuthorName ?? string.Empty;
        var avatarSize  = RsTheme.S(44f);

        // Avatar is clickable - opens the user's profile popup with bio and full RP preferences. Whole-card click still opens the post detail because IsAnyItemActive filters it out when the avatar button is engaged.
        var avatarMin = ImGui.GetCursorScreenPos();
        AbsoluteRP.Social.SocialAvatar.DrawCircle(
            profile?.avatarUrl,
            avatarSize,
            ImGui.ColorConvertFloat4ToU32(RsTheme.BgPrimary));
        ImGui.SetCursorScreenPos(avatarMin);
        if (ImGui.InvisibleButton("##avatar_hit_" + post.Id + "_" + post.AuthorUserID,
                                   new Vector2(avatarSize, avatarSize)))
        {
            AbsoluteRP.Social.SocialUserProfilePopup.Open(post.AuthorUserID);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("View " + displayName + "'s profile");
            ImGui.EndTooltip();
        }
        ImGui.SetCursorScreenPos(new Vector2(avatarMin.X + avatarSize + RsTheme.S(8f), avatarMin.Y));
        ImGui.BeginGroup();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextUnformatted(displayName);
        ImGui.PopStyleColor();
        // Only show the account-name line when it differs from what's being displayed - avoids the "name / name" duplicate look.
        if (!string.IsNullOrEmpty(accountName)
            && !string.Equals(accountName, displayName, StringComparison.Ordinal))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("@" + accountName);
            ImGui.PopStyleColor();
        }
        DrawPreferencesPill(profile);
        ImGui.EndGroup();
    }

    // Small pill below the author name summarising their RP preferences. Shows the first tag with "+N" when there's more; hover surfaces the full organised list in a tooltip.
    private static void DrawPreferencesPill(AbsoluteRP.Defines.AccountProfile? profile)
    {
        var prefs = profile?.rpPreferences;
        if (prefs == null || prefs.Count == 0) return;

        var lbl = prefs.Count == 1
            ? prefs[0]
            : $"{prefs[0]}  +{prefs.Count - 1}";

        var pad     = new Vector2(RsTheme.S(8f), RsTheme.S(3f));
        var textSz  = ImGui.CalcTextSize(lbl);
        var min     = ImGui.GetCursorScreenPos();
        var max     = min + new Vector2(textSz.X + pad.X * 2f, textSz.Y + pad.Y * 2f);
        var radius  = (max.Y - min.Y) * 0.5f;
        var dl      = ImGui.GetWindowDrawList();
        var accent  = RsTheme.AccentPrimary;
        dl.AddRectFilled(min, max,
            ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, 0.18f)),
            radius);
        dl.AddRect(min, max,
            ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f)),
            radius);
        dl.AddText(new Vector2(min.X + pad.X, min.Y + pad.Y),
            ImGui.ColorConvertFloat4ToU32(RsTheme.TextPrimary), lbl);

        // Invisible hit surface for the tooltip.
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton("##pref_pill_" + profile!.GetHashCode(),
                              max - min);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("RP preferences");
            ImGui.PopStyleColor();
            ImGui.Separator();
            for (int i = 0; i < prefs.Count; i++)
                ImGui.BulletText(prefs[i]);
            ImGui.EndTooltip();
        }
    }

    // Extracts up to `maxChars` of plain text from a BBCode body, and counts embedded image/video tags so the card can hint at them without loading media in the feed.
    private static (string preview, bool truncated, int imgCount, int vidCount)
        BuildPreview(string body, int maxChars)
    {
        var sb = new System.Text.StringBuilder(maxChars + 8);
        int i = 0, plain = 0, imgs = 0, vids = 0;
        bool truncated = false;
        while (i < body.Length)
        {
            char c = body[i];
            if (c == '[')
            {
                int close = body.IndexOf(']', i + 1);
                if (close > 0)
                {
                    var inner = body.Substring(i + 1, close - i - 1);
                    var lower = inner.ToLowerInvariant();
                    if (lower.StartsWith("img"))
                    {
                        imgs++;
                        // skip through [/img]
                        int endTag = IndexOfIgnoreCase(body, "[/img]", close + 1);
                        i = endTag > 0 ? endTag + 6 : close + 1;
                        continue;
                    }
                    if (lower.StartsWith("video"))
                    {
                        vids++;
                        int endTag = IndexOfIgnoreCase(body, "[/video]", close + 1);
                        i = endTag > 0 ? endTag + 8 : close + 1;
                        continue;
                    }
                    // Any other tag (b/i/u/color/size and closers) - strip silently for the preview.
                    i = close + 1;
                    continue;
                }
            }
            if (plain >= maxChars) { truncated = true; break; }
            sb.Append(c);
            plain++;
            i++;
        }
        if (i < body.Length && !truncated) { /* the loop exited before end only via tag skip; no ellipsis needed */ }
        var text = sb.ToString().Trim();
        if (truncated) text += "…";
        return (text, truncated, imgs, vids);
    }

    private static int IndexOfIgnoreCase(string haystack, string needle, int start)
        => haystack.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);

    // composer

    private void OpenComposer(SocialCategory cat, int editId, SocialPost? seed = null)
    {
        _composeOpen        = true;
        _composeCategory    = cat;
        _composeEditId      = editId;
        _composeScope       = seed?.ScopeType ?? SocialScope.All;
        _composeScopeValue  = seed?.ScopeValue ?? string.Empty;
        SeedScopeCascade(_composeScope, _composeScopeValue);
        _composeTitle       = seed?.Title ?? string.Empty;
        _composeBody        = seed?.Body ?? string.Empty;
        _composeHasExpiry   = seed?.ExpiresAt > 0;
        _composeExpiryHours = _composeHasExpiry && seed != null
            ? Math.Max(1, (int)Math.Ceiling((seed.ExpiresAt - SocialFeed.NowMs()) / 3_600_000.0))
            : 48;
        // Seed the WYSIWYG composer from the post's BBCode body (or clear it for a new post) so what the user sees is what they'll submit.
        if (!string.IsNullOrEmpty(_composeBody))
            AbsoluteRP.Social.SocialComposer.LoadFromBBCode(_composeBody);
        else
            AbsoluteRP.Social.SocialComposer.Clear();
    }

    private void DrawComposeView()
    {
        SectionSpacer();

        ImGui.Spacing();
        // Header row
        if (RsElements.Button("← Back to feed", RsElements.ButtonVariant.Ghost))
        {
            _view = SocialView.Feed;
            _composeOpen = false;
            return;
        }
        ImGui.Spacing();

        if (!RsElements.BeginPanel("cv_panel", title: null, fitContentsX: false, fitContentsY: true))
        {
            RsElements.EndPanel();
            return;
        }
        try
        {
            // Every row's total width is capped to the composer's width so nothing spills past the right edge of the post editor. Two spaces because RsElements' Dropdown/InputText width params get S()-scaled internally, while the composer's Draw(size) parameter is passed raw pixels. Keeping both on hand avoids the double-scale mistake that made fields overflow last iteration.
            const float composerWRaw = 560f;
            var composerWPx = RsTheme.S(composerWRaw);
            var spacing     = ImGui.GetStyle().ItemSpacing.X;

            // SECTION 1: Audience
            DrawSectionLabel("Audience");
            var catNames  = new List<string>();
            var catValues = new List<SocialCategory>();
            for (int i = 0; i < Categories.Length; i++)
            {
                catNames.Add(Categories[i].label);
                catValues.Add(Categories[i].cat);
            }
            var scopeVisible = _composeScope != SocialScope.All;
            // Raw (unscaled) widths - Dropdown/InputText S()-scale internally. Numbers here are in the same "raw" space as composerWRaw so the arithmetic stays honest.
            var audienceCatWRaw = scopeVisible ? 180f : 240f;
            var audienceScWRaw  = 180f;
            var spacingRaw      = spacing / MathF.Max(0.01f, RsTheme.S(1f));
            var audienceValWRaw = composerWRaw - audienceCatWRaw - audienceScWRaw - spacingRaw * 2f;

            int catIdx = Math.Max(0, catValues.IndexOf(_composeCategory));
            if (RsElements.Dropdown("np_cat", ref catIdx, catNames, width: audienceCatWRaw))
                _composeCategory = catValues[Math.Clamp(catIdx, 0, catValues.Count - 1)];

            ImGui.SameLine();
            var scopeNames  = new List<string> { "Everyone", "A region", "A data center", "A world" };
            var scopeValues = new List<SocialScope> { SocialScope.All, SocialScope.Region, SocialScope.DC, SocialScope.World };
            int scopeIdx = Math.Max(0, scopeValues.IndexOf(_composeScope));
            if (RsElements.Dropdown("np_scope", ref scopeIdx, scopeNames, width: audienceScWRaw))
                _composeScope = scopeValues[Math.Clamp(scopeIdx, 0, scopeValues.Count - 1)];
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Who the post is for. Region, data center and world narrow it down step by step.");

            if (scopeVisible)
            {
                // Region -> data center -> world, each list filtered by the one before it, starting from the player's own world.
                ImGui.Spacing();
                var parts = _composeScope == SocialScope.Region ? 1 : _composeScope == SocialScope.DC ? 2 : 3;
                var partWRaw = (composerWRaw - spacingRaw * (parts - 1)) / parts;

                var regions = AbsoluteRP.Defines.GameData.GetAllRegions();
                var regionNames = regions.Select(r => AbsoluteRP.Defines.GameData.GetRegionCode(r) + "  ·  " + AbsoluteRP.Defines.GameData.GetRegionName(r)).ToList();
                int ri = Math.Max(0, regions.IndexOf(_composeRegion));
                if (RsElements.Dropdown("np_region", ref ri, regionNames, width: partWRaw))
                    SetComposeRegion(regions[Math.Clamp(ri, 0, regions.Count - 1)]);

                if (parts >= 2)
                {
                    ImGui.SameLine();
                    var dcs = AbsoluteRP.Defines.GameData.GetDataCentersByRegion(_composeRegion);
                    var dcNames = dcs.Select(AbsoluteRP.Defines.GameData.GetDataCenterName).ToList();
                    int di = Math.Max(0, dcs.IndexOf(_composeDC));
                    if (RsElements.Dropdown("np_dc", ref di, dcNames, width: partWRaw))
                        SetComposeDC(dcs[Math.Clamp(di, 0, dcs.Count - 1)]);
                }
                if (parts >= 3)
                {
                    ImGui.SameLine();
                    var worlds = AbsoluteRP.Defines.GameData.GetWorldNamesByDataCenter(_composeDC);
                    int wi = Math.Max(0, worlds.FindIndex(w => string.Equals(w, _composeWorld, StringComparison.OrdinalIgnoreCase)));
                    if (RsElements.Dropdown("np_world", ref wi, worlds, width: partWRaw) && worlds.Count > 0)
                        _composeWorld = worlds[Math.Clamp(wi, 0, worlds.Count - 1)];
                }
                _composeScopeValue = ComposeScopeValue();
            }

            SectionSpacer();

            // SECTION 2: Title + body
            DrawSectionLabel("Title");
            RsElements.InputText("np_title", ref _composeTitle, 200,
                                 placeholder: "Post title", width: composerWRaw);

            ImGui.Spacing();

            DrawSectionLabel("Content");
            // Live WYSIWYG toolbar. B/I/U show their current on/off state via ToolbarToggle so users can see what will be applied at the caret. Align buttons work the same way for L/C/R. Size is now a dropdown that applies on selection change.
            if (ToolbarToggle("B##np_b", AbsoluteRP.Social.SocialComposer.CurrentBold()))
                AbsoluteRP.Social.SocialComposer.ToggleBold();
            ImGui.SameLine();
            if (ToolbarToggle("I##np_i", AbsoluteRP.Social.SocialComposer.CurrentItalic()))
                AbsoluteRP.Social.SocialComposer.ToggleItalic();
            ImGui.SameLine();
            if (ToolbarToggle("U##np_u", AbsoluteRP.Social.SocialComposer.CurrentUnderline()))
                AbsoluteRP.Social.SocialComposer.ToggleUnderline();

            ImGui.SameLine();
            var curAlign = AbsoluteRP.Social.SocialComposer.CurrentAlign();
            using (AbsoluteRP.RsUI.RsIcons.Push())
            {
                var alL = Dalamud.Interface.FontAwesomeIcon.AlignLeft.ToIconString();
                var alC = Dalamud.Interface.FontAwesomeIcon.AlignCenter.ToIconString();
                var alR = Dalamud.Interface.FontAwesomeIcon.AlignRight.ToIconString();
                if (ToolbarToggle(alL + "##np_al", curAlign == AbsoluteRP.Social.SocialComposer.TextAlign.Left))
                    AbsoluteRP.Social.SocialComposer.ApplyAlign(AbsoluteRP.Social.SocialComposer.TextAlign.Left);
                ImGui.SameLine();
                if (ToolbarToggle(alC + "##np_ac", curAlign == AbsoluteRP.Social.SocialComposer.TextAlign.Center))
                    AbsoluteRP.Social.SocialComposer.ApplyAlign(AbsoluteRP.Social.SocialComposer.TextAlign.Center);
                ImGui.SameLine();
                if (ToolbarToggle(alR + "##np_ar", curAlign == AbsoluteRP.Social.SocialComposer.TextAlign.Right))
                    AbsoluteRP.Social.SocialComposer.ApplyAlign(AbsoluteRP.Social.SocialComposer.TextAlign.Right);
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(RsTheme.S(60f));
            if (ImGui.ColorEdit3("##np_color", ref _composeColor,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
            {
                AbsoluteRP.Social.SocialComposer.ApplyColor(
                    PackColorRgba(_composeColor.X, _composeColor.Y, _composeColor.Z, 1f));
            }

            // Font-family dropdown. SocialComposerFonts lazy-loads the bundled system fonts (Segoe UI / Georgia / Arial / Times / Courier), each with Regular/Bold/Italic/BoldItalic variants so the composer can render proper italics + bolds.
            ImGui.SameLine();
            var families = AbsoluteRP.Social.SocialComposerFonts.Families;
            if (families.Count > 0)
            {
                var curFamily = AbsoluteRP.Social.SocialComposer.CurrentFontFamily();
                var famLabels = new List<string>(families.Count);
                int  famIdx   = 0;
                for (int fi = 0; fi < families.Count; fi++)
                {
                    famLabels.Add(families[fi].DisplayName);
                    if (string.Equals(families[fi].Id, curFamily, StringComparison.Ordinal))
                        famIdx = fi;
                }
                ImGui.SetNextItemWidth(RsTheme.S(120f));
                if (RsElements.Dropdown("np_font_dd", ref famIdx, famLabels, width: 120f))
                {
                    var picked = families[Math.Clamp(famIdx, 0, families.Count - 1)].Id;
                    AbsoluteRP.Social.SocialComposer.ApplyFontFamily(picked);
                }
            }

            // Size dropdown - one click on a value applies immediately to the selection (or the caret's pending style). No separate Apply button.
            ImGui.SameLine();
            var curSize = AbsoluteRP.Social.SocialComposer.CurrentSize();
            var sizeIdx = Math.Max(0, Array.IndexOf(_composeSizeOptions, curSize));
            var sizeLabels = new List<string>(_composeSizeOptions.Length);
            for (int i = 0; i < _composeSizeOptions.Length; i++)
                sizeLabels.Add(_composeSizeOptions[i] + " pt");
            if (RsElements.Dropdown("np_sz_dd", ref sizeIdx, sizeLabels, width: 80f))
            {
                var pt = _composeSizeOptions[Math.Clamp(sizeIdx, 0, _composeSizeOptions.Length - 1)];
                _composeSizePt = pt;
                AbsoluteRP.Social.SocialComposer.ApplySize(pt);
            }

            ImGui.Spacing();
            AbsoluteRP.Social.SocialComposer.Draw("np_composer",
                new Vector2(composerWPx, RsTheme.S(280f)));

            SectionSpacer();

            // SECTION 3: Attachments
            DrawSectionLabel("Attachments");
            if (_uploading) ImGui.BeginDisabled();
            if (RsElements.Button("Attach file##np_att", RsElements.ButtonVariant.Ghost))
                StartFileUpload();
            if (_uploading) ImGui.EndDisabled();

            // Hint text pushed to its own line so it can't spill past the composer width when combined with the button. Truncated rather than wrapped so it stays as a single line.
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Images (png/jpg/webp/gif) or videos (mp4/webm/mov)");
            ImGui.PopStyleColor();

            if (!string.IsNullOrEmpty(_uploadStatus))
            {
                var col = _uploadStatus.StartsWith("Attached") || _uploadStatus.StartsWith("Uploaded")
                    ? RsTheme.AccentSuccess
                    : (_uploadStatus.StartsWith("Uploading") ? RsTheme.TextMuted : RsTheme.AccentDanger);
                ImGui.PushStyleColor(ImGuiCol.Text, col);
                ImGui.TextUnformatted(_uploadStatus);
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();

            // URL-based inserts. Compute the input width from the button's own text so the "Insert image" / "Insert video" labels always fit without truncation.
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Or insert by URL:");
            ImGui.PopStyleColor();

            var framePadX      = ImGui.GetStyle().FramePadding.X;
            var insertImgLabel = "Insert image";
            var insertVidLabel = "Insert video";
            // Button width in scaled pixels (buttons take scaled sizes).
            var insertBtnWPx   = MathF.Max(
                ImGui.CalcTextSize(insertImgLabel).X,
                ImGui.CalcTextSize(insertVidLabel).X) + framePadX * 2f + RsTheme.S(8f);
            // InputText width must be raw so RsElements' internal S() brings it back to pixels that match composerWPx once the button's own pixel width is subtracted.
            var urlInputWRaw   = composerWRaw - (insertBtnWPx + spacing) / MathF.Max(0.01f, RsTheme.S(1f));

            RsElements.InputText("np_img_url", ref _composeImageUrl, 500,
                                 placeholder: "Image URL (https://…)", width: urlInputWRaw);
            ImGui.SameLine();
            if (RsElements.Button(insertImgLabel + "##ii", RsElements.ButtonVariant.Ghost,
                                  new Vector2(insertBtnWPx, 0f))
                && !string.IsNullOrWhiteSpace(_composeImageUrl))
            {
                AbsoluteRP.Social.SocialComposer.InsertImage(_composeImageUrl.Trim());
                _composeImageUrl = string.Empty;
            }
            RsElements.InputText("np_vid_url", ref _composeVideoUrl, 500,
                                 placeholder: "Video URL (mp4/webm/…)", width: urlInputWRaw);
            ImGui.SameLine();
            if (RsElements.Button(insertVidLabel + "##iv", RsElements.ButtonVariant.Ghost,
                                  new Vector2(insertBtnWPx, 0f))
                && !string.IsNullOrWhiteSpace(_composeVideoUrl))
            {
                AbsoluteRP.Social.SocialComposer.InsertVideo(_composeVideoUrl.Trim());
                _composeVideoUrl = string.Empty;
            }

            SectionSpacer();

            // SECTION 4: Options
            DrawSectionLabel("Options");
            RsElements.Checkbox("Set expiry##np_exp", ref _composeHasExpiry);
            if (_composeHasExpiry)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(RsTheme.S(90f));
                ImGui.InputInt("##np_exph", ref _composeExpiryHours);
                if (_composeExpiryHours < 1)   _composeExpiryHours = 1;
                if (_composeExpiryHours > 720) _composeExpiryHours = 720; // 30d cap
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted($"hours ({_composeExpiryHours / 24}d {_composeExpiryHours % 24}h)");
                ImGui.PopStyleColor();
            }

            SectionSpacer();

            // Actions
            var composedBody = AbsoluteRP.Social.SocialComposer.ToBBCode();
            var canSubmit = !string.IsNullOrWhiteSpace(_composeTitle)
                         && !string.IsNullOrWhiteSpace(composedBody)
                         && (_composeScope == SocialScope.All || !string.IsNullOrWhiteSpace(_composeScopeValue));

            if (!canSubmit) ImGui.BeginDisabled();
            if (RsElements.Button(_composeEditId == 0 ? "Post" : "Save", RsElements.ButtonVariant.Primary,
                                  new Vector2(RsTheme.S(120f), 0f)))
            {
                var post = new SocialPost
                {
                    Id         = _composeEditId,
                    Category   = _composeCategory,
                    ScopeType  = _composeScope,
                    ScopeValue = _composeScope == SocialScope.All ? string.Empty : _composeScopeValue.Trim(),
                    Title      = _composeTitle.Trim(),
                    Body       = composedBody.Trim(),
                    ExpiresAt  = _composeHasExpiry
                        ? SocialFeed.NowMs() + (long)_composeExpiryHours * 3_600_000L
                        : 0L,
                };
                _ = SocialFeed_DS.SendPost(post);
                _composeOpen = false;
                _view = SocialView.Feed;
            }
            if (!canSubmit) ImGui.EndDisabled();
            ImGui.SameLine();
            if (RsElements.Button("Cancel", RsElements.ButtonVariant.Ghost, new Vector2(RsTheme.S(120f), 0f)))
            {
                _composeOpen = false;
                _view = SocialView.Feed;
            }
        }
        finally { RsElements.EndPanel(); }
    }

    // Small section header used to visually chunk the compose form.
    private static void DrawSectionLabel(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
        ImGui.TextUnformatted(label.ToUpperInvariant());
        ImGui.PopStyleColor();
        var dl  = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var w   = ImGui.GetContentRegionAvail().X;
        dl.AddLine(min, new Vector2(min.X + w, min.Y),
                   ImGui.ColorConvertFloat4ToU32(RsTheme.Border), 1f);
        ImGui.Dummy(new Vector2(0f, RsTheme.S(6f)));
    }

    private static void SectionSpacer()
    {
        ImGui.Dummy(new Vector2(0f, RsTheme.S(10f)));
    }

    // Preset sizes for the compose toolbar. Baseline body = 12; the spread covers small quotes / notes up through header sizes.
    private static readonly int[] _composeSizeOptions = { 10, 12, 14, 16, 18, 24, 32, 48 };

    // Ghost-variant button that renders in an "active/toggled" state when `on` is true - accent border + accent-tinted background so users can see at a glance which formatting bits will apply at the caret.
    private static bool ToolbarToggle(string label, bool on)
    {
        if (on)
        {
            var accent = RsTheme.AccentPrimary;
            var bg     = new Vector4(accent.X, accent.Y, accent.Z, 0.25f);
            var bgH    = new Vector4(accent.X, accent.Y, accent.Z, 0.40f);
            var bgA    = new Vector4(accent.X, accent.Y, accent.Z, 0.55f);
            ImGui.PushStyleColor(ImGuiCol.Button,        ImGui.ColorConvertFloat4ToU32(bg));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.ColorConvertFloat4ToU32(bgH));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive,  ImGui.ColorConvertFloat4ToU32(bgA));
            ImGui.PushStyleColor(ImGuiCol.Border,        ImGui.ColorConvertFloat4ToU32(RsTheme.AccentPrimary));
            ImGui.PushStyleColor(ImGuiCol.Text,          ImGui.ColorConvertFloat4ToU32(RsTheme.TextPrimary));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            var clicked = RsElements.Button(label, RsElements.ButtonVariant.Ghost);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(5);
            return clicked;
        }
        return RsElements.Button(label, RsElements.ButtonVariant.Ghost);
    }

    // Pack 0..1 floats into 0xRRGGBBAA - the format SocialComposer stores per-segment. RgbaToPack inside the composer swaps to ImGui's ABGR for the draw list; this side just has to hand it RRGGBBAA.
    private static uint PackColorRgba(float r, float g, float b, float a)
    {
        var ir = (uint)Math.Clamp((int)(r * 255f + 0.5f), 0, 255);
        var ig = (uint)Math.Clamp((int)(g * 255f + 0.5f), 0, 255);
        var ib = (uint)Math.Clamp((int)(b * 255f + 0.5f), 0, 255);
        var ia = (uint)Math.Clamp((int)(a * 255f + 0.5f), 0, 255);
        return (ir << 24) | (ig << 16) | (ib << 8) | ia;
    }

    // bookmarks tab

    private int  _bookmarkKind = 0;          // 0 = profiles, 1 = posts
    private bool _profileBookmarksAsked;

    private void DrawBookmarksTab(bool tabChanged)
    {
        if (tabChanged || _bookmarksFetchedThisOpen == 0)
        {
            _ = SocialFeed_DS.FetchBookmarks(offset: 0, limit: 100);
            _bookmarksFetchedThisOpen = SocialFeed.NowMs();
            _profileBookmarksAsked = false;
        }
        if (!_profileBookmarksAsked && Plugin.character != null)
        {
            _profileBookmarksAsked = true;
            Profiles_DS.RequestBookmarks(Plugin.character);
        }

        // Profiles | Posts, refresh on the right.
        var profiles = AbsoluteRP.Windows.Social.Views.Bookmarks.profileList;
        // Two tabs: underlined label for the active one.
        bool Tab(string label, int kind)
        {
            var sz = ImGui.CalcTextSize(label);
            var pad = new Vector2(RsTheme.S(14f), RsTheme.S(8f));
            var pos = ImGui.GetCursorScreenPos();
            var clicked = ImGui.InvisibleButton("##bm_tab_" + kind, sz + pad * 2f);
            var hov = ImGui.IsItemHovered();
            var active = _bookmarkKind == kind;
            var dl = ImGui.GetWindowDrawList();
            var col = active ? RsTheme.TextPrimary : hov ? RsTheme.TextSecondary : RsTheme.TextMuted;
            dl.AddText(pos + pad, ImGui.GetColorU32(col), label);
            var y = pos.Y + sz.Y + pad.Y * 2f - RsTheme.S(2f);
            dl.AddRectFilled(new Vector2(pos.X, y), new Vector2(pos.X + sz.X + pad.X * 2f, y + RsTheme.S(2f)),
                active ? RsTheme.U.AccentPrimary : RsTheme.U.Border);
            return clicked;
        }
        if (Tab($"Profiles ({profiles.Count})", 0)) _bookmarkKind = 0;
        ImGui.SameLine(0f, 0f);
        if (Tab($"Posts ({SocialFeed.Bookmarks.Count})", 1)) _bookmarkKind = 1;
        ImGui.SameLine();
        var refreshW = ImGui.CalcTextSize("Refresh").X + RsTheme.S(36f);
        var offX     = ImGui.GetContentRegionAvail().X - refreshW;
        if (offX > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offX);
        if (RsElements.Button("Refresh##bm_refresh", RsElements.ButtonVariant.Ghost))
        {
            _ = SocialFeed_DS.FetchBookmarks(offset: 0, limit: 100);
            if (Plugin.character != null) Profiles_DS.RequestBookmarks(Plugin.character);
        }
        ImGui.Spacing();

        if (_bookmarkKind == 0) { DrawProfileBookmarks(profiles); return; }

        if (SocialFeed.Bookmarks.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("No saved posts yet. Open a post and click the star under it to keep it here. Your own posts can be saved too.");
            ImGui.PopStyleColor();
            return;
        }
        for (int i = 0; i < SocialFeed.Bookmarks.Count; i++)
            DrawPostCard(SocialFeed.Bookmarks[i]);
    }

    // Profiles bookmarked from the profile viewer: open or remove.
    private void DrawProfileBookmarks(List<AbsoluteRP.Windows.Social.Views.Bookmark> profiles)
    {
        if (profiles.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("No bookmarked profiles yet. Bookmark a player from their profile or the target menu to keep them here.");
            ImGui.PopStyleColor();
            return;
        }
        var locked = AbsoluteRP.Windows.Social.Views.Bookmarks.DisableBookmarkSelection;
        for (int i = 0; i < profiles.Count; i++)
        {
            var bm = profiles[i];
            if (!RsElements.BeginPanel("bm_profile_" + i, null, fitContentsX: false, fitContentsY: true)) { RsElements.EndPanel(); continue; }
            try
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(bm.ProfileName) ? bm.PlayerName : bm.ProfileName);
                ImGui.PopStyleColor();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted($"{bm.PlayerName}  ·  {bm.PlayerWorld}");
                ImGui.PopStyleColor();

                using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(locked))
                {
                    if (RsElements.Button("Open profile##bm_open_" + i, RsElements.ButtonVariant.Primary))
                    {
                        Plugin.plugin.OpenTargetWindow();
                        AbsoluteRP.Windows.Profiles.ReportWindow.reportCharacterName = bm.PlayerName;
                        AbsoluteRP.Windows.Profiles.ReportWindow.reportCharacterWorld = bm.PlayerWorld;
                        AbsoluteRP.Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.characterName = bm.PlayerName;
                        AbsoluteRP.Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.characterWorld = bm.PlayerWorld;
                        AbsoluteRP.Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.RequestingProfile = true;
                        AbsoluteRP.Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.ResetAllData();
                        Profiles_DS.FetchProfile(Plugin.character, false, -1, bm.PlayerName, bm.PlayerWorld, -1);
                    }
                }
                ImGui.SameLine();
                using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    if (RsElements.Button("Remove##bm_rm_" + i, RsElements.ButtonVariant.Danger))
                        Profiles_DS.RemoveBookmarkedPlayer(Plugin.character, bm.PlayerName, bm.profileIndex);
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Hold Ctrl to remove this bookmark");
            }
            finally { RsElements.EndPanel(); }
            ImGui.Spacing();
        }
    }

    // notifications popup

    private void DrawNotificationsPopup()
    {
        if (!_notificationsPopupOpen) return;

        ImGui.OpenPopup("##arp_social_notifs");
        var view = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(view.WorkPos + view.WorkSize * 0.5f, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(RsTheme.S(460f), RsTheme.S(520f)), ImGuiCond.Appearing);
        if (ImGui.BeginPopupModal("##arp_social_notifs", ref _notificationsPopupOpen,
                                  ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.TextUnformatted("Notifications");
            ImGui.PopStyleColor();
            ImGui.SameLine();

            var unseen = SocialFeed.UnseenNotificationCount;
            var msTop  = ImGui.CalcTextSize("Mark all read").X + RsTheme.S(36f);
            var closW  = ImGui.CalcTextSize("Close").X + RsTheme.S(36f);
            var trailW = msTop + RsTheme.S(6f) + closW;
            var avail  = ImGui.GetContentRegionAvail().X;
            var off    = avail - trailW;
            if (off > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + off);

            if (unseen == 0) ImGui.BeginDisabled();
            if (RsElements.Button("Mark all read", RsElements.ButtonVariant.Ghost))
            {
                var unseenIds = new List<int>();
                for (int i = 0; i < SocialFeed.Notifications.Count; i++)
                    if (SocialFeed.Notifications[i].SeenAt == 0) unseenIds.Add(SocialFeed.Notifications[i].Id);
                _ = SocialFeed.MarkNotificationsSeen(unseenIds);
            }
            if (unseen == 0) ImGui.EndDisabled();
            ImGui.SameLine();
            if (RsElements.Button("Close", RsElements.ButtonVariant.Ghost))
            {
                _notificationsPopupOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.Separator();
            ImGui.Spacing();

            if (SocialFeed.Notifications.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped("Nothing yet. Follow someone from a post card to hear when they post.");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.BeginChild("##arp_notif_scroll", new Vector2(0, 0), false);
                for (int i = 0; i < SocialFeed.Notifications.Count; i++)
                {
                    var n = SocialFeed.Notifications[i];
                    bool isUnseen = n.SeenAt == 0;
                    if (isUnseen) ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentPrimary);
                    else          ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
                    ImGui.TextUnformatted($"{n.SourceName} posted in {CategoryLabel(n.PostCategory)}");
                    ImGui.PopStyleColor();

                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                    ImGui.TextWrapped(string.IsNullOrEmpty(n.PostTitle) ? "(no title)" : n.PostTitle);
                    ImGui.PopStyleColor();

                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted(RelativeTime(n.CreatedAt));
                    ImGui.PopStyleColor();

                    if (RsElements.Button($"Jump to post##notif_{n.Id}", RsElements.ButtonVariant.Ghost))
                    {
                        // Jump to the category tab and mark this one seen.
                        for (int c = 0; c < Categories.Length; c++)
                            if (Categories[c].cat == n.PostCategory) { _selectedTab = c; _fetchedThisOpen = 0; break; }
                        if (isUnseen) _ = SocialFeed.MarkNotificationsSeen(new[] { n.Id });
                        _notificationsPopupOpen = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.Separator();
                    ImGui.Spacing();
                }
                ImGui.EndChild();
            }
            ImGui.EndPopup();
        }
    }

    // detail view (in-panel)

    private void OpenDetail(SocialPost post)
    {
        _detailPost = post;
        _view       = SocialView.Detail;
        _newCommentBody   = string.Empty;
        _editingCommentId = 0;
        if (_lastCommentsFetchedFor != post.Id)
        {
            _lastCommentsFetchedFor = post.Id;
            _ = SocialFeed.FetchComments(post.Id);
        }
    }

    private void DrawDetailView()
    {
        var post = _detailPost!;

        // Header: back button + title.
        if (RsElements.Button("← Back to feed", RsElements.ButtonVariant.Ghost))
        {
            _view = SocialView.Feed;
            _detailPost = null;
            return;
        }
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted("  " + CategoryLabel(post.Category));
        ImGui.PopStyleColor();
        ImGui.Spacing();

        // Post panel - title, meta, actions, full markup body.
        if (RsElements.BeginPanel("dv_post", title: null, fitContentsX: false, fitContentsY: true))
        {
            try
            {
                // Author header: same avatar + name + prefs pill as feed cards so the identity block is consistent across views.
                DrawPostAuthorHeader(post);
                ImGui.Spacing();

                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(post.Title) ? "(no title)" : post.Title);
                ImGui.PopStyleColor();

                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted($"{ScopeLabel(post)}  •  {RelativeTime(post.CreatedAt)}");
                if (post.ExpiresAt > 0)
                {
                    var msLeft = post.ExpiresAt - SocialFeed.NowMs();
                    ImGui.TextUnformatted(msLeft > 0 ? $"expires {RelativeFuture(msLeft)}" : "expired");
                }
                ImGui.PopStyleColor();

                ImGui.Spacing();

                bool isMine     = post.AuthorUserID != 0
                                  && post.AuthorUserID == (Plugin.plugin?.Configuration?.account?.userID ?? 0);
                bool bookmarked = SocialFeed.BookmarkedPostIds.Contains(post.Id);
                bool following  = SocialFeed.FollowedUserIds.Contains(post.AuthorUserID);
                if (isMine)
                {
                    if (RsElements.Button($"Edit##dv_edit_{post.Id}", RsElements.ButtonVariant.Ghost))
                    {
                        OpenComposer(post.Category, post.Id, post);
                        _view = SocialView.Compose;
                        // finally { EndPanel() } will balance BeginPanel; do NOT call EndPanel here - double-pop empties the PanelStack and throws next frame.
                        return;
                    }
                    ImGui.SameLine();
                    if (RsElements.Button($"Delete##dv_del_{post.Id}", RsElements.ButtonVariant.Danger))
                    {
                        _ = SocialFeed_DS.DeletePost(post.Id);
                        _view = SocialView.Feed;
                        _detailPost = null;
                        return;
                    }
                }
                else if (IsFeedModerator)
                {
                    // Moderator removal of someone else's post; hold Ctrl so a stray click can't do it.
                    using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                    {
                        if (RsElements.Button($"Remove (mod)##dv_moddel_{post.Id}", RsElements.ButtonVariant.Danger))
                        {
                            _ = SocialFeed_DS.DeletePost(post.Id);
                            _view = SocialView.Feed;
                            _detailPost = null;
                            return;
                        }
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Remove this post as a moderator (hold Ctrl)");
                }
                else
                {
                    var flLabel = following  ? "Unfollow" : "Follow";
                    if (RsElements.Button($"{flLabel}##dv_fl_{post.Id}",
                                           following ? RsElements.ButtonVariant.Ghost : RsElements.ButtonVariant.Primary))
                        _ = SocialFeed_DS.ToggleFollow(post.AuthorUserID, !following);

                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (!string.IsNullOrEmpty(post.Body))
                    SocialMarkup.Render(post.Body);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                DrawReactionRow(post, prefix: "dv");
            }
            finally { RsElements.EndPanel(); }
        }
        else { RsElements.EndPanel(); }

        // Comments panel.
        ImGui.Spacing();
        DrawCommentsPanel(post);
    }

    private void DrawCommentsPanel(SocialPost post)
    {
        if (!RsElements.BeginPanel("dv_comments", title: null, fitContentsX: false, fitContentsY: true))
        {
            RsElements.EndPanel();
            return;
        }
        try
        {
            var comments = SocialFeed.GetCommentsFor(post.Id);
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.TextUnformatted($"Comments ({comments.Count})");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            var refreshW = ImGui.CalcTextSize("Refresh").X + RsTheme.S(36f);
            var availX = ImGui.GetContentRegionAvail().X;
            var offX = availX - refreshW;
            if (offX > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offX);
            if (RsElements.Button($"Refresh##cm_refresh_{post.Id}", RsElements.ButtonVariant.Ghost))
                _ = SocialFeed.FetchComments(post.Id);
            ImGui.Separator();
            ImGui.Spacing();

            if (comments.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped("No comments yet. Be the first to reply.");
                ImGui.PopStyleColor();
            }
            else
            {
                var myUserId = Plugin.plugin?.Configuration?.account?.userID ?? 0;
                for (int i = 0; i < comments.Count; i++) DrawCommentRow(comments[i], myUserId);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Composer for new / edit.
            if (_editingCommentId != 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted($"Editing comment #{_editingCommentId}");
                ImGui.PopStyleColor();
            }
            RsElements.InputTextArea($"cm_new_{post.Id}", ref _newCommentBody, 4000,
                                     placeholder: "Write a reply…",
                                     size: new Vector2(RsTheme.S(480f), RsTheme.S(80f)));
            bool canSend = !string.IsNullOrWhiteSpace(_newCommentBody);
            if (!canSend) ImGui.BeginDisabled();
            if (RsElements.Button(_editingCommentId == 0 ? "Comment" : "Save",
                                   RsElements.ButtonVariant.Primary, new Vector2(RsTheme.S(120f), 0f)))
            {
                _ = SocialFeed.SendComment(new SocialComment
                {
                    Id     = _editingCommentId,
                    PostID = post.Id,
                    Body   = _newCommentBody.Trim(),
                });
                _newCommentBody   = string.Empty;
                _editingCommentId = 0;
            }
            if (!canSend) ImGui.EndDisabled();
            if (_editingCommentId != 0)
            {
                ImGui.SameLine();
                if (RsElements.Button("Cancel edit", RsElements.ButtonVariant.Ghost))
                {
                    _newCommentBody   = string.Empty;
                    _editingCommentId = 0;
                }
            }
        }
        finally { RsElements.EndPanel(); }
    }

    private void DrawCommentRow(SocialComment c, int myUserId)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextUnformatted(string.IsNullOrEmpty(c.AuthorName) ? "unknown" : c.AuthorName);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        var age = RelativeTime(c.CreatedAt);
        var suffix = c.UpdatedAt > c.CreatedAt ? "  (edited)" : "";
        ImGui.TextUnformatted($"  •  {age}{suffix}");
        ImGui.PopStyleColor();

        if (myUserId == 0 || c.AuthorUserID != myUserId)
        {
            // Someone else's reply: a small flag on the right reports just this reply.
            var reported = SocialFeed.ReportedCommentIds.Contains(c.Id);
            var flagW = RsTheme.S(24f);
            ImGui.SameLine();
            var off = ImGui.GetContentRegionAvail().X - flagW;
            if (off > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + off);
            if (reported) ImGui.BeginDisabled();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
            var flag = RsElements.IconButton(FontAwesomeIcon.Flag, "cm_flag_" + c.Id, RsElements.ButtonVariant.Ghost, 20f);
            ImGui.PopStyleColor();
            if (reported) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(reported ? "Report (already sent)" : "Report this reply");
            if (flag && !reported)
            {
                _reportPostId = c.PostID;
                _reportCommentId = c.Id;
                _reportReasonIdx = 0;
                _reportDetails = string.Empty;
                _reportOpenRequested = true;
            }
        }
        if (myUserId != 0 && c.AuthorUserID == myUserId)
        {
            var editW  = ImGui.CalcTextSize("Edit").X   + RsTheme.S(36f);
            var delW   = ImGui.CalcTextSize("Delete").X + RsTheme.S(36f);
            var trailW = editW + RsTheme.S(6f) + delW;
            ImGui.SameLine();
            var availX = ImGui.GetContentRegionAvail().X;
            var offX   = availX - trailW;
            if (offX > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offX);
            if (RsElements.Button($"Edit##cm_edit_{c.Id}", RsElements.ButtonVariant.Ghost))
            {
                _editingCommentId = c.Id;
                _newCommentBody   = c.Body;
            }
            ImGui.SameLine();
            if (RsElements.Button($"Delete##cm_del_{c.Id}", RsElements.ButtonVariant.Danger))
                _ = SocialFeed.DeleteComment(c.Id);
        }
        else if (IsFeedModerator)
        {
            var remW = ImGui.CalcTextSize("Remove (mod)").X + RsTheme.S(36f);
            ImGui.SameLine();
            var availR = ImGui.GetContentRegionAvail().X - remW;
            if (availR > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availR);
            using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
            {
                if (RsElements.Button($"Remove (mod)##cm_moddel_{c.Id}", RsElements.ButtonVariant.Danger))
                    _ = SocialFeed.DeleteComment(c.Id);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Remove this reply as a moderator (hold Ctrl)");
        }

        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextWrapped(c.Body);
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    // composer helpers

    // Opens the file picker for image or video, reads the bytes, and fires an upload. The server responds with a URL that gets appended to the composer body as `[img]url[/img]` or `[video]url[/video]`. One upload at a time - the toolbar disables itself while `_uploading` is set. A status line surfaces server errors (unsupported type, size cap, magic-byte mismatch).
    private static readonly string[] ImageExts = { "png", "jpg", "jpeg", "webp", "gif" };
    private static readonly string[] VideoExts = { "mp4", "webm", "mov", "mkv", "m4v" };
    private static readonly string[] AudioExts = { "mp3", "ogg", "wav", "flac", "m4a" };

    // Single "attach file" entry point. Accepts both image and video extensions in the same picker and dispatches to the right MediaKind based on the file's actual extension after the user selects it.
    private void StartFileUpload()
    {
        if (_uploading) return;
        var allExts = ImageExts.Concat(VideoExts).Concat(AudioExts).ToArray();

        AbsoluteRP.RsUI.RsFileDialog.OpenImagePicker("Attach an image, video or audio clip", (ok, path) =>
        {
            if (!ok || string.IsNullOrWhiteSpace(path)) return;
            byte[] bytes;
            string ext;
            try
            {
                bytes = System.IO.File.ReadAllBytes(path);
                ext   = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _uploadStatus = "Read failed: " + ex.Message;
                return;
            }
            if (bytes.Length == 0)
            {
                _uploadStatus = "Empty file";
                return;
            }

            SocialFeed.MediaKind kind;
            if (Array.IndexOf(ImageExts, ext) >= 0)      kind = SocialFeed.MediaKind.Image;
            else if (Array.IndexOf(VideoExts, ext) >= 0) kind = SocialFeed.MediaKind.Video;
            else if (Array.IndexOf(AudioExts, ext) >= 0) kind = SocialFeed.MediaKind.Audio;
            else { _uploadStatus = "Unsupported file type"; return; }
            var cap = kind == SocialFeed.MediaKind.Image ? 10L << 20 : kind == SocialFeed.MediaKind.Audio ? 30L << 20 : 100L << 20;
            if (bytes.Length > cap) { _uploadStatus = $"Too large: the limit is {cap >> 20} MB"; return; }

            _uploading    = true;
            _uploadStatus = kind == SocialFeed.MediaKind.Image ? "Uploading image…" : kind == SocialFeed.MediaKind.Audio ? "Uploading audio…" : "Uploading video…";
            SocialFeed.PendingUploadCallback = (respKind, url, error) =>
            {
                _uploading = false;
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(url))
                {
                    _uploadStatus = "Upload failed: " + (string.IsNullOrEmpty(error) ? "no url" : error);
                    return;
                }
                _uploadStatus = "Attached";
                // Audio rides in a media block too; posts draw it with the mp3 player.
                if (respKind == SocialFeed.MediaKind.Video || respKind == SocialFeed.MediaKind.Audio)
                    AbsoluteRP.Social.SocialComposer.InsertVideo(url);
                else
                    AbsoluteRP.Social.SocialComposer.InsertImage(url);
            };
            _ = SocialFeed.UploadMedia(kind, ext, bytes);
        }, startPath: null, extensions: allExts);
    }

    // Appends `[tag]...[/tag]` (or `[tag=value]...[/tag]`) at the end of the body. ImGui doesn't expose caret position on multiline inputs in this binding, so an append-only insertion is the honest option that also matches how users usually type: format text at the end.
    private void InsertMarkupTag(string tag, string? value = null)
    {
        var open  = value == null ? $"[{tag}]" : $"[{tag}={value}]";
        var close = $"[/{tag}]";
        if (string.IsNullOrEmpty(_composeBody)) _composeBody = $"{open}{close}";
        else                                    _composeBody = _composeBody.TrimEnd() + $" {open}{close}";
    }

    private void AppendToBody(string s)
    {
        if (string.IsNullOrEmpty(_composeBody)) _composeBody = s.TrimStart();
        else                                    _composeBody = _composeBody.TrimEnd() + s;
    }

    // lifecycle

    public void OnSelected()
    {
        // Force a fresh fetch of the current tab on re-select so returning from another page always shows current data (mirrors the pattern used in ProfilesPage).
        _fetchedThisOpen          = 0;
        _bookmarksFetchedThisOpen = 0;
        // Prime the follow set + notifications once when the page opens so Follow/Unfollow buttons and the DTR bell badge are accurate right away, without waiting for the user to click either surface.
        _ = SocialFeed_DS.FetchFollows();
        _ = SocialFeed_DS.FetchNotifications(unseenOnly: false, limit: 100);
        try { SocialWindow.Instance?.OnOpen(); }
        catch (Exception ex) { Plugin.PluginLog.Debug($"SocialPage OnSelected: {ex.Message}"); }
    }

    public void OnDeselected()
    {
        try { SocialWindow.Instance?.OnClose(); }
        catch (Exception ex) { Plugin.PluginLog.Debug($"SocialPage OnDeselected: {ex.Message}"); }
    }

    // formatting helpers

    private static string DisplayAuthor(SocialPost p)
        => string.IsNullOrWhiteSpace(p.AuthorName) ? "unknown" : p.AuthorName;

    private static string CategoryLabel(SocialCategory c) => c switch
    {
        SocialCategory.Event       => "Event",
        SocialCategory.Venue       => "Venue",
        SocialCategory.Recruitment => "Recruitment",
        SocialCategory.LFRP        => "LFRP",
        _                          => "Post",
    };

    private static string ScopeLabel(SocialPost p) => p.ScopeType switch
    {
        SocialScope.All   => "All",
        SocialScope.DC    => string.IsNullOrEmpty(p.ScopeValue) ? "DC" : $"DC: {PlaceLeaf(p.ScopeValue)}",
        SocialScope.World => string.IsNullOrEmpty(p.ScopeValue) ? "World" : $"World: {PlaceLeaf(p.ScopeValue)}",
        SocialScope.Region => string.IsNullOrEmpty(p.ScopeValue) ? "Region" : $"Region: {PlaceLeaf(p.ScopeValue)}",
        _                 => "",
    };

    private static string RelativeTime(long unixMs)
    {
        if (unixMs <= 0) return "";
        var diff = SocialFeed.NowMs() - unixMs;
        if (diff < 0) diff = 0;
        var s = diff / 1000;
        if (s < 60)          return $"{s}s ago";
        if (s < 3600)        return $"{s / 60}m ago";
        if (s < 86400)       return $"{s / 3600}h ago";
        if (s < 86400 * 30)  return $"{s / 86400}d ago";
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime.ToString("yyyy-MM-dd");
    }

    private static string RelativeFuture(long msFromNow)
    {
        var s = msFromNow / 1000;
        if (s < 60)          return $"in {s}s";
        if (s < 3600)        return $"in {s / 60}m";
        if (s < 86400)       return $"in {s / 3600}h";
        return $"in {s / 86400}d";
    }
}

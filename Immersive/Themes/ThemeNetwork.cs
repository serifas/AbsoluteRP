using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Networking;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Network;

namespace AbsoluteRP.Immersive.Themes;

// Theme gallery packets. The game server stores theme documents as opaque JSON strings and gates the public gallery behind review. CLIENT -> SERVER (ClientPackets) CThemeList (340) accountKey, string filter ("gallery" | "mine" | "installed"), int page CThemeGet (341) accountKey, int themeId CThemeSave (342) accountKey, int themeId (0 = new), string name, string description, string json CThemeSubmit (343) accountKey, int themeId -> review_status = 'pending' CThemeWithdraw (344) accountKey, int themeId -> back to draft CThemeDelete (345) accountKey, int themeId CThemeInstall (346) accountKey, int themeId CThemeUninstall (347) accountKey, int themeId SERVER -> CLIENT (ServerPackets) SThemeList (340) string filter, int page, int total, int count, then count x ThemeSummary SThemeDocument (341) int themeId, string json, string status, string reviewerNotes, int installCount, bool installed SThemeSaved (342) int localRequestId? - we key by name; server returns int themeId, bool ok, string message SThemeResult (343) string action, bool ok, string message, int themeId ThemeSummary = int id, string name, string description, string author, int authorAccountId, string status ("draft" | "pending" | "approved" | "rejected"), int installCount, bool official, string material, bool installedByMe, bool mine Server-side rules (spec for the TCP server): themes table: id, owner_account_id, name, description, document_json (MEDIUMTEXT), material, status ENUM('draft','pending','approved','rejected'), reviewer_notes, install_count, is_official, created_at, updated_at. theme_installs: theme_id, account_id, installed_at (unique pair). "gallery" filter returns status='approved' ordered by install_count DESC. "mine" returns everything the caller owns, any status. CThemeGet on someone else's theme is allowed only when status='approved' (that's how viewers fetch the theme a profile references). A profile's immersive theme id (trailing int on SSendProfileStatus / packet 63) may only reference a theme the owner has installed or owns+approved; the server should validate and reset to 0 otherwise. Saving an approved theme flips it back to 'pending' (re-review).
public static class ThemeNetwork
{
    public sealed class ThemeSummary
    {
        public int Id;
        public string Name = "";
        public string Description = "";
        public string Author = "";
        public int AuthorAccountId;
        public string Status = "draft";
        public int InstallCount;
        public bool Official;
        public string Material = "Allagan";
        public bool InstalledByMe;
        public bool Mine;
        // Versioning: the approved version shown by default, the newest version, and that newest version's status.
        public int CurrentVersion = 1;
        public int LatestVersion = 1;
        public string LatestStatus = "draft";
        public string Visibility = "gallery";
        public bool AllowRemix = true;
    }

    public sealed class ThemeVersionInfo
    {
        public int Version;
        public string Status = "draft";
        public string Notes = "";
        public string Created = "";
    }

    public sealed class ThemeVersions
    {
        public int ThemeId, CurrentVersion, LatestVersion;
        public List<ThemeVersionInfo> Items = new();
        public DateTime FetchedAt;
    }

    // Version lists by theme id (approved-only for themes we don't own).
    public static readonly Dictionary<int, ThemeVersions> Versions = new();
    private static readonly HashSet<int> _versionsRequested = new();

    public static ThemeVersions? VersionsOf(int themeId, bool request = true)
    {
        if (Versions.TryGetValue(themeId, out var v)) return v;
        if (request) RequestVersions(themeId);
        return null;
    }

    public static void RequestVersions(int themeId)
    {
        if (!Ready || themeId <= 0 || !_versionsRequested.Add(themeId)) return;
        _ = Send(b =>
        {
            b.WriteInt((int)ClientPackets.CThemeVersions);
            b.WriteString(Key);
            b.WriteInt(themeId);
        });
    }

    public static void ForgetVersions(int themeId) { Versions.Remove(themeId); _versionsRequested.Remove(themeId); }

    // Last results, read by the Themes page.
    public static readonly List<ThemeSummary> Gallery = new();
    public static readonly List<ThemeSummary> Mine = new();
    public static readonly List<ThemeSummary> InstalledRemote = new();
    public static bool GalleryLoading, MineLoading;
    public static string LastMessage = "";
    public static bool LastOk = true;
    public static DateTime LastMessageAt = DateTime.MinValue;
    public static event Action<ThemeDocument, string, string>? DocumentReceived;   // (doc, status, reviewerNotes)
    public static event Action<int, bool, string>? Saved;                           // (themeId, ok, message)

    private static bool Ready => ClientTCP.IsConnected() && Plugin.plugin?.Configuration?.account != null;
    private static string Key => Plugin.plugin.Configuration.account.accountKey;

    // sends
    public static void RequestList(string filter, int page = 0)
    {
        if (!Ready) return;
        if (filter == "gallery") GalleryLoading = true; else if (filter == "mine") MineLoading = true; else if (filter == "pending") PendingLoading = true;
        _ = Send(b =>
        {
            b.WriteInt((int)ClientPackets.CThemeList);
            b.WriteString(Key);
            b.WriteString(filter);
            b.WriteInt(page);
        });
    }

    // version: 0 = my pinned version if any, else the current approved one; -1 = the newest version (only the owner / a moderator gets it); n = that version (must be approved unless I own it).
    public static void RequestTheme(int themeId, int version = 0)
    {
        if (!Ready) return;
        _ = Send(b =>
        {
            b.WriteInt((int)ClientPackets.CThemeGet);
            b.WriteString(Key);
            b.WriteInt(themeId);
            b.WriteInt(version);
        });
    }

    public static void Save(ThemeDocument doc)
    {
        if (!Ready) { Notify(false, "Not connected."); return; }
        _ = Send(b =>
        {
            b.WriteInt((int)ClientPackets.CThemeSave);
            b.WriteString(Key);
            b.WriteInt(doc.ServerId);
            b.WriteString(doc.Name);
            b.WriteString(doc.Description ?? "");
            b.WriteString(doc.ToJson());
        });
    }

    public static void Submit(int themeId)    => SimpleAction(ClientPackets.CThemeSubmit, themeId);
    public static void Withdraw(int themeId)  => SimpleAction(ClientPackets.CThemeWithdraw, themeId);
    public static void Delete(int themeId)    => SimpleAction(ClientPackets.CThemeDelete, themeId);
    // Install, optionally pinned to one approved version (0 = follow the current approved version as it changes).
    public static void Install(int themeId, int version = 0)
    {
        if (!Ready) { Notify(false, "Not connected."); return; }
        _ = Send(b =>
        {
            b.WriteInt((int)ClientPackets.CThemeInstall);
            b.WriteString(Key);
            b.WriteInt(themeId);
            b.WriteInt(Math.Max(0, version));
        });
    }
    public static void Uninstall(int themeId) => SimpleAction(ClientPackets.CThemeUninstall, themeId);

    // Set one of my profiles' immersive theme (what viewers see there). themeId: 0 = viewer's choice, 1..N = built-ins, >= 1000 = gallery.
    public static void Assign(int profileIndex, int themeId, string? playerName = null, string? playerWorld = null, int profileId = 0)
    {
        if (!Ready) { Notify(false, "Not connected."); return; }
        var name = string.IsNullOrEmpty(playerName) ? Plugin.plugin?.playername ?? "" : playerName;
        var world = string.IsNullOrEmpty(playerWorld) ? Plugin.plugin?.playerworld ?? "" : playerWorld;
        _pendingAssign = (profileIndex, themeId, name, world);
        _ = Send(b =>
        {
            b.WriteInt((int)ClientPackets.CThemeAssign);
            b.WriteString(Key);
            b.WriteString(name);
            b.WriteString(world);
            b.WriteInt(profileIndex);
            b.WriteInt(themeId);
            b.WriteInt(profileId);   // trailing: the profile's row id, matched directly server-side
        });
    }
    private static (int index, int theme, string name, string world)? _pendingAssign;

    // Moderator decision (server enforces the permission).
    public static void Review(int themeId, bool approve, string notes)
    {
        if (!Ready) { Notify(false, "Not connected."); return; }
        _ = Send(b =>
        {
            b.WriteInt((int)ClientPackets.CThemeReview);
            b.WriteString(Key);
            b.WriteInt(themeId);
            b.WriteBool(approve);
            b.WriteString(notes ?? "");
        });
    }

    public static readonly List<ThemeSummary> Pending = new();
    // Learned from the server: a pending (review) list only arrives for theme moderators; everyone else gets a refusal. null = not asked yet.
    public static bool? IsThemeModerator;
    public static bool PendingLoading;

    private static void SimpleAction(ClientPackets packet, int themeId)
    {
        if (!Ready) { Notify(false, "Not connected."); return; }
        _ = Send(b =>
        {
            b.WriteInt((int)packet);
            b.WriteString(Key);
            b.WriteInt(themeId);
        });
    }

    private static async Task Send(Action<ByteBuffer> write)
    {
        try
        {
            using var buffer = new ByteBuffer();
            write(buffer);
            await ClientTCP.SendDataAsync(buffer.ToArray());
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Debug("[ThemeNetwork] send: " + ex.Message);
        }
    }

    // receives (registered in ClientHandleData)
    public static void HandleList(byte[] data)
    {
        try
        {
            using var b = new ByteBuffer();
            b.WriteBytes(data);
            b.ReadInt();
            var filter = b.ReadString();
            var page = b.ReadInt();
            var total = b.ReadInt();
            var count = b.ReadInt();
            var list = new List<ThemeSummary>(count);
            for (int i = 0; i < count; i++)
            {
                var s = new ThemeSummary
                {
                    Id = b.ReadInt(),
                    Name = b.ReadString(),
                    Description = b.ReadString(),
                    Author = b.ReadString(),
                    AuthorAccountId = b.ReadInt(),
                    Status = b.ReadString(),
                    InstallCount = b.ReadInt(),
                    Official = b.ReadBool(),
                    Material = b.ReadString(),
                    InstalledByMe = b.ReadBool(),
                    Mine = b.ReadBool(),
                };
                if (b.Length() >= 12)
                {
                    s.CurrentVersion = b.ReadInt();
                    s.LatestVersion = b.ReadInt();
                    s.LatestStatus = b.ReadString();
                }
                if (b.Length() >= 5)
                {
                    s.Visibility = b.ReadString();
                    s.AllowRemix = b.ReadBool();
                }
                list.Add(s);
            }
            if (filter == "pending") IsThemeModerator = true;
            var target = filter == "mine" ? Mine : filter == "installed" ? InstalledRemote : filter == "pending" ? Pending : Gallery;
            if (page == 0) target.Clear();
            target.AddRange(list);
            if (filter == "gallery") GalleryLoading = false; else if (filter == "mine") MineLoading = false; else if (filter == "pending") PendingLoading = false;
        }
        catch (Exception ex) { Plugin.PluginLog?.Debug("[ThemeNetwork] list: " + ex.Message); GalleryLoading = MineLoading = PendingLoading = false; }
    }

    public static void HandleDocument(byte[] data)
    {
        try
        {
            using var b = new ByteBuffer();
            b.WriteBytes(data);
            b.ReadInt();
            var id = b.ReadInt();
            var json = b.ReadString();
            var status = b.ReadString();
            var notes = b.ReadString();
            var installs = b.ReadInt();
            var installed = b.ReadBool();
            int version = 0, current = 0, latest = 0;
            if (b.Length() >= 12) { version = b.ReadInt(); current = b.ReadInt(); latest = b.ReadInt(); }
            var doc = ThemeDocument.FromJson(json);
            if (doc == null) { ThemeLibrary.ForgetRequest(id); return; }
            doc.ServerId = id;
            doc.ServerVersion = version;
            doc.UpdatedUtc = DateTime.UtcNow;
            // The newest (unapproved) version is for editing / review only: never let it replace the installed or cached copy viewers use.
            var isLatestUnapproved = version > 0 && version == latest && status != "approved";
            if (isLatestUnapproved) { /* handed to whoever asked via the event */ }
            else if (installed) ThemeLibrary.SaveInstalled(doc);
            else ThemeLibrary.CacheServerTheme(doc);
            DocumentReceived?.Invoke(doc, status, notes);
        }
        catch (Exception ex) { Plugin.PluginLog?.Debug("[ThemeNetwork] document: " + ex.Message); }
    }

    public static void HandleVersions(byte[] data)
    {
        try
        {
            using var b = new ByteBuffer();
            b.WriteBytes(data);
            b.ReadInt();
            var v = new ThemeVersions { ThemeId = b.ReadInt(), CurrentVersion = b.ReadInt(), LatestVersion = b.ReadInt(), FetchedAt = DateTime.UtcNow };
            var n = b.ReadInt();
            for (int i = 0; i < n; i++)
                v.Items.Add(new ThemeVersionInfo { Version = b.ReadInt(), Status = b.ReadString(), Notes = b.ReadString(), Created = b.ReadString() });
            Versions[v.ThemeId] = v;
        }
        catch (Exception ex) { Plugin.PluginLog?.Debug("[ThemeNetwork] versions: " + ex.Message); }
    }

    public static void HandleSaved(byte[] data)
    {
        try
        {
            using var b = new ByteBuffer();
            b.WriteBytes(data);
            b.ReadInt();
            var id = b.ReadInt();
            var ok = b.ReadBool();
            var msg = b.ReadString();
            Notify(ok, msg);
            if (ok) { ForgetVersions(id); RequestList("mine"); }
            Saved?.Invoke(id, ok, msg);
        }
        catch (Exception ex) { Plugin.PluginLog?.Debug("[ThemeNetwork] saved: " + ex.Message); }
    }

    public static void HandleResult(byte[] data)
    {
        try
        {
            using var b = new ByteBuffer();
            b.WriteBytes(data);
            b.ReadInt();
            var action = b.ReadString();
            var ok = b.ReadBool();
            var msg = b.ReadString();
            var id = b.ReadInt();
            Notify(ok, msg);
            if (ok && action == "install") RequestTheme(id);          // pull the document so it's usable offline
            if (ok && action == "uninstall") ThemeLibrary.Uninstall(id);
            if (ok && (action == "submit" || action == "withdraw" || action == "delete")) RequestList("mine");
            if (ok && (action == "install" || action == "uninstall")) RequestList("gallery");
            if (ok && action == "review") { ForgetVersions(id); ThemeLibrary.ForgetRequest(id); RequestList("pending"); RequestList("gallery"); }
            if (ok && (action == "submit" || action == "withdraw")) ForgetVersions(id);
            if (action == "assign" && _pendingAssign.HasValue)
            {
                if (ok)
                {
                    // Mirror the change locally so the gallery and the profile window agree without a refetch.
                    var (idx, theme, name, world) = _pendingAssign.Value;
                    bool Same(AbsoluteRP.ProfileData p) => p != null && p.index == idx
                        && (string.IsNullOrEmpty(p.playerName) || (p.playerName == name && p.playerWorld == world));
                    foreach (var p in AbsoluteRP.RsUI.Pages.ProfilesPage.profiles)
                        if (Same(p)) p.immersiveTheme = theme;
                    var cur = AbsoluteRP.RsUI.Pages.ProfilesPage.CurrentProfile;
                    if (cur != null && cur.index == idx && (name == Plugin.plugin?.playername && world == Plugin.plugin?.playerworld)) cur.immersiveTheme = theme;
                    AbsoluteRP.RsUI.Pages.ThemesPage.OnThemeAssigned(name, world, idx, theme);
                    RefreshViewedProfileIfMine(name, world);
                }
                _pendingAssign = null;
            }
            if (action == "list" && !ok) { PendingLoading = false; Pending.Clear(); IsThemeModerator = false; }
        }
        catch (Exception ex) { Plugin.PluginLog?.Debug("[ThemeNetwork] result: " + ex.Message); }
    }

    // If the profile window is showing one of MY profiles for that character, pull it again so the new theme shows straight away. The server decides which theme the viewed profile has, so a theme set on a different profile of mine (or anyone else's profile) is untouched.
    private static void RefreshViewedProfileIfMine(string name, string world)
    {
        try
        {
            var p = Plugin.plugin;
            if (p == null || !p.IsTargetWindowOpen) return;
            var pd = TargetProfileWindow.profileData;
            var myId = p.Configuration?.account?.userID ?? -1;
            if (pd == null || myId < 0 || pd.accountID != myId) return;
            var viewedName = TargetProfileWindow.characterName;
            var viewedWorld = TargetProfileWindow.characterWorld;
            if (!string.Equals(viewedName, name, StringComparison.OrdinalIgnoreCase) || !string.Equals(viewedWorld, world, StringComparison.OrdinalIgnoreCase)) return;
            TargetProfileWindow.RequestingProfile = true;
            TargetProfileWindow.ResetAllData();
            Profiles_DS.FetchProfile(Plugin.character, false, -1, name, world, -1);
        }
        catch (Exception ex) { Plugin.PluginLog?.Debug("[ThemeNetwork] refresh viewed: " + ex.Message); }
    }

    private static void Notify(bool ok, string message)
    {
        LastOk = ok;
        LastMessage = message ?? "";
        LastMessageAt = DateTime.UtcNow;
    }
}

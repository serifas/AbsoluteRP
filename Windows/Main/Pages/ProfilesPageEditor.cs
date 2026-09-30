using System;
using System.Numerics;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Networking;
using AbsoluteRP.Network;

namespace AbsoluteRP.RsUI.Pages;

/// Static helpers that render individual pieces of the profile editor (hero panel, visibility toggles, spoiler checkboxes, action row, tab body, and the two overlays) using RsUI widgets. The shell page (ProfilesPage) owns character selection, the profile picker, verification, the tab bar and modal dialogs and calls into these helpers for each subsection. Business logic (network calls, file-dialog uploads, save-tracker state, backup import/export, transparent-PNG scrubbing) is preserved as-is from ProfileWindow.cs - only the widget layer is re-expressed in RsUI.
public static class ProfilesPageEditor
{
    // Reuse the single dialog manager owned by ProfilesPage so avatar uploads, backup save, and backup load all pump through the same manager - otherwise a dialog registered by ProfileWindow.Instance methods (Backup / Load Backup) never gets drawn.
    private static AbsoluteRP.RsUI.RsFileDialogManager DialogManager => ProfilesPage._fileDialogManager;

    // Per-panel open state for the collapsibles the user asked for. Hero opens by default; the rest start collapsed.
    private static bool _heroOpen        = true;
    private static bool _visibilityOpen  = false;
    private static bool _spoilersOpen    = false;

    // Hero panel - avatar, title, background, format-help

    public static void DrawHeroPanel(Character character, ProfileData profile)
    {
        if (profile == null) return;

        // File dialogs (avatar/background upload) need to pump every frame.
        try { DialogManager.Draw(); }
        catch (Exception ex) { Plugin.PluginLog.Debug("ProfilesPageEditor file-dialog draw: " + ex.Message); }

        if (!RsElements.BeginCollapsible("prof.hero", "Avatar & Name", ref _heroOpen))
        {
            RsElements.EndCollapsible();
            return;
        }

        try
        {
            var handle = ProfileWindow.currentAvatarImg?.Handle
                         ?? ProfileWindow.pictureTab?.Handle;

            // Avatar
            if (handle.HasValue)
            {
                var diameter = RsTheme.S(128f);
                var avail = ImGui.GetContentRegionAvail().X;
                var centeredX = MathF.Max(0f, (avail - diameter) * 0.5f);
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + centeredX);
                try
                {
                    Anim.DrawCircleAvatarInline(
                        handle.Value,
                        diameter,
                        profile.titleColor,
                        borderThickness: 3f);
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("ProfilesPageEditor avatar draw: " + ex.Message);
                }
            }

            // Edit Avatar button
            CenterNextItem(EstimateButtonWidth("Edit Avatar"));
            if (RsElements.Button("Edit Avatar##prof.hero.editAvatar", RsElements.ButtonVariant.Ghost))
            {
                RsFileDialog.OpenImagePicker(
                    title: "Choose an avatar image",
                    onPicked: (ok, path) =>
                    {
                        if (!ok || string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
                        try
                        {
                            var bytes = System.IO.File.ReadAllBytes(path);
                            profile.avatarBytes = bytes;
                            ProfileWindow.currentAvatarImg = Plugin.TextureProvider
                                .CreateFromImageAsync(AbsoluteRP.Helpers.Imaging.ScaleImageBytes(bytes, 100, 100))
                                .Result;
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug("Avatar image load: " + ex.Message);
                        }
                    });
            }
            AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_EditAvatarBtn);

            ImGui.Dummy(new Vector2(0f, RsTheme.S(6f)));

            // Title input + color picker (centered as a group)
            var title = profile.title ?? string.Empty;
            var color = profile.titleColor;

            // Centered muted label above the input.
            var labelSize = ImGui.CalcTextSize("Title");
            var availTitle = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (availTitle - labelSize.X) * 0.5f));
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Title");
            ImGui.PopStyleColor();

            // RsElements.InputText + inline color picker, centered as one group.
            var inputW    = RsTheme.S(320f);
            var swatchW   = ImGui.GetFrameHeight();
            var groupGap  = RsTheme.S(6f);
            var groupW    = inputW + groupGap + swatchW;
            var titleX    = MathF.Max(0f, (availTitle - groupW) * 0.5f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + titleX);

            if (RsElements.InputText($"prof.hero.title.{ProfileWindow.profileIndex}",
                                      ref title, 50, placeholder: "Profile title", width: 320f))
            {
                profile.title = title;
            }
            AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_TitleInput);
            ImGui.SameLine(0f, groupGap);
            if (ImGui.ColorEdit4(
                    $"##Text Input Color{ProfileWindow.profileIndex}",
                    ref color,
                    ImGuiColorEditFlags.NoInputs))
            {
                profile.titleColor = color;
            }

            ImGui.Dummy(new Vector2(0f, RsTheme.S(6f)));

            // Set Background + Remove-background pair
            var bgBtnW = EstimateButtonWidth("Set Background");
            var xBtnW  = EstimateButtonWidth("X");
            var gap    = RsTheme.S(8f);
            var pairW  = bgBtnW + gap + xBtnW;
            var bgX    = MathF.Max(0f, (ImGui.GetContentRegionAvail().X - pairW) * 0.5f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + bgX);
            var __setBgMin = ImGui.GetCursorScreenPos();
            if (RsElements.Button("Set Background##prof.hero.setBg", RsElements.ButtonVariant.Ghost))
            {
                // Backgrounds accept images AND videos - the video path is handled by ProfilesPage.SyncBackgroundVideo when the bytes sniff as an MP4/WebM/AVI container.
                RsFileDialog.OpenImagePicker(
                    title: "Choose a background image or video",
                    onPicked: (ok, path) =>
                    {
                        if (!ok || string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
                        try
                        {
                            var bytes = System.IO.File.ReadAllBytes(path);
                            profile.backgroundBytes = bytes;

                            // Only decode a still texture for actual images - videos would fail CreateFromImageAsync and would paint garbage. The video pipeline takes over from SyncBackgroundVideo() next frame.
                            if (ProfilesPage.LooksLikeVideoBytes(bytes))
                            {
                                ProfileWindow.backgroundImage = null;
                            }
                            else
                            {
                                ProfileWindow.backgroundImage = Plugin.TextureProvider
                                    .CreateFromImageAsync(AbsoluteRP.Helpers.Imaging.ScaleImageBytes(bytes, 1000, 1500))
                                    .Result;
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug("Background image/video load: " + ex.Message);
                        }
                    },
                    extensions: new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",
                                        ".mp4", ".webm", ".mov", ".mkv", ".avi" });
            }
            ImGui.SameLine(0f, gap);
            if (RsElements.Button("X##prof.hero.removeBg", RsElements.ButtonVariant.Danger))
            {
                profile.backgroundBytes = CreateTransparentPng();
                ProfileWindow.backgroundImage = null;
            }
            AbsoluteRP.Helpers.TutorialManager.AnchorRect(
                AbsoluteRP.Helpers.ProfileTutorial.Anchor_SetBackground,
                __setBgMin, ImGui.GetItemRectMax());
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Remove background image");

            ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));

            // Format Information callout
            var formatText = "Format Information";
            var formatSize = ImGui.CalcTextSize(formatText);
            var formatX = MathF.Max(0f, (ImGui.GetContentRegionAvail().X - formatSize.X) * 0.5f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + formatX);
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentPrimary);
            try
            {
                ImGui.TextUnformatted(formatText);
            }
            finally
            {
                ImGui.PopStyleColor();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(UI.inputHelperUrlInfo);

            // Save / preview / delete / backup, left-aligned under the callout.
            ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
            DrawActionsRow(character, ActionsProfileIndex);
        }
        finally
        {
            RsElements.EndCollapsible();
        }

        // Legacy: some external code (e.g. tutorial hooks) still flips ProfileWindow.editAvatar to request the picker. Route it into our themed dialog too.
        if (ProfileWindow.editAvatar)
        {
            ProfileWindow.editAvatar = false;
            RsFileDialog.OpenImagePicker(
                title: "Choose an avatar image",
                onPicked: (ok, path) =>
                {
                    if (!ok || string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
                    try
                    {
                        var bytes = System.IO.File.ReadAllBytes(path);
                        profile.avatarBytes = bytes;
                        ProfileWindow.currentAvatarImg = Plugin.TextureProvider
                            .CreateFromImageAsync(AbsoluteRP.Helpers.Imaging.ScaleImageBytes(bytes, 100, 100))
                            .Result;
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug("Avatar image load (deferred): " + ex.Message);
                    }
                });
        }
    }

    // Visibility panel - private / active / compass / NSFW / triggering

    public static void DrawVisibilityPanel(ProfileData profile, int profileIndex)
    {
        if (profile == null) return;

        if (!RsElements.BeginCollapsible("prof.visibility", "Visibility", ref _visibilityOpen))
        {
            RsElements.EndCollapsible();
            return;
        }

        try
        {
            var isPrivate = profile.isPrivate;
            if (RsElements.Toggle("prof.visibility.private", ref isPrivate, "Set Private"))
                profile.isPrivate = isPrivate;
            AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_Private);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Leave unchecked to keep profile publicly viewable");

            // "Set as current" makes this profile the one other players see when they mouseover the user in-game. That only makes sense on a profile the user actually created for the character they're currently playing. Editing a different character's profiles is fine - flagging one of those as the live tooltip on the wrong character is not.
            var liveChar = Plugin.character;
            bool matchesLiveChar = liveChar != null
                && !string.IsNullOrEmpty(liveChar.characterName)
                && string.Equals(profile.playerName,  liveChar.characterName,  StringComparison.OrdinalIgnoreCase)
                && string.Equals(profile.playerWorld, liveChar.characterWorld, StringComparison.OrdinalIgnoreCase);

            var isActive = profile.isActive;
            if (!matchesLiveChar) ImGui.BeginDisabled();
            if (RsElements.Toggle("prof.visibility.active", ref isActive, "Set As Current"))
                profile.isActive = isActive;
            if (!matchesLiveChar) ImGui.EndDisabled();
            AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_Active);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(matchesLiveChar
                    ? "Sets this profile as your current viewable profile when public"
                    : (liveChar == null || string.IsNullOrEmpty(liveChar.characterName)
                        ? "Log in as the character who owns this profile to set it as your current tooltip."
                        : $"This profile belongs to {profile.playerName} @ {profile.playerWorld}. Log in as that character to set it as your current tooltip."));
            }

            var compass = ProfileWindow.showOnCompass;
            if (RsElements.Toggle("prof.visibility.compass", ref compass, "Show on Compass"))
            {
                ProfileWindow.showOnCompass = compass;
                try
                {
                    Connections_DS.SetCompassStatus(Plugin.character, compass, profileIndex);
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("ProfilesPageEditor SetCompassStatus: " + ex.Message);
                }
            }
            AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_Compass);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This will make your position publicly visible on the compass while this profile is set as current");

        }
        finally
        {
            RsElements.EndCollapsible();
        }
    }

    // Content warnings - expansion spoiler checkboxes

    public static void DrawContentWarningsPanel(ProfileData profile)
    {
        if (profile == null) return;

        if (!RsElements.BeginCollapsible("prof.spoilers", "Content Warnings", ref _spoilersOpen))
        {
            RsElements.EndCollapsible();
            return;
        }

        try
        {
            var nsfw = profile.NSFW;
            ImGui.TextUnformatted("Contains the following content:");
            if (RsElements.Checkbox("Set as 18+", ref nsfw))
                profile.NSFW = nsfw;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Flags this profile as containing adult (18+) content");

            var triggering = profile.TRIGGERING;
            if (RsElements.Checkbox("Set as Triggering", ref triggering))
                profile.TRIGGERING = triggering;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Warns viewers that this profile contains potentially triggering content");


            var nsfwMin = ImGui.GetCursorScreenPos();

            AbsoluteRP.Helpers.TutorialManager.AnchorRect(
                AbsoluteRP.Helpers.ProfileTutorial.Anchor_NsfwTrigger,
                nsfwMin, ImGui.GetItemRectMax());

            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            try
            {
                ImGui.TextUnformatted("Contains spoilers from:");
            }
            finally
            {
                ImGui.PopStyleColor();
            }
            ImGui.Dummy(new Vector2(0f, RsTheme.S(4f)));

            var spoilerMin = ImGui.GetCursorScreenPos();
            var arr = profile.SpoilerARR;
            if (RsElements.Checkbox("A Realm Reborn##prof.sp.arr", ref arr)) profile.SpoilerARR = arr;
            ImGui.SameLine();
            var hw = profile.SpoilerHW;
            if (RsElements.Checkbox("Heavensward##prof.sp.hw", ref hw)) profile.SpoilerHW = hw;
            ImGui.SameLine();
            var sb = profile.SpoilerSB;
            if (RsElements.Checkbox("Stormblood##prof.sp.sb", ref sb)) profile.SpoilerSB = sb;

            var shb = profile.SpoilerSHB;
            if (RsElements.Checkbox("Shadowbringers##prof.sp.shb", ref shb)) profile.SpoilerSHB = shb;
            ImGui.SameLine();
            var ew = profile.SpoilerEW;
            if (RsElements.Checkbox("Endwalker##prof.sp.ew", ref ew)) profile.SpoilerEW = ew;
            ImGui.SameLine();
            var dt = profile.SpoilerDT;
            if (RsElements.Checkbox("Dawntrail##prof.sp.dt", ref dt)) profile.SpoilerDT = dt;
            AbsoluteRP.Helpers.TutorialManager.AnchorRect(
                AbsoluteRP.Helpers.ProfileTutorial.Anchor_Spoilers,
                spoilerMin, ImGui.GetItemRectMax());
        }
        finally
        {
            RsElements.EndCollapsible();
        }
    }

    // Actions - save / delete / backup / load backup

    // Which profile slot the action buttons act on; set by the page before the hero panel draws.
    public static int ActionsProfileIndex;

    // Kept for callers: the actions now live in the hero panel.
    public static void DrawActionsPanel(Character character, int profileIndex) { }

    private static void DrawActionsRow(Character character, int profileIndex)
    {
        {
            var savingLabel = ProfileSaveTracker.IsSaving ? "Saving..." : "Save Profile";
            var saveWidth = RsTheme.S(140f);

            using (ImRaii.Disabled(ProfileSaveTracker.IsSaving))
            {
                if (RsElements.Button(
                        savingLabel + "##prof.actions.save",
                        RsElements.ButtonVariant.Primary,
                        new Vector2(saveWidth, 0f)))
                {
                    try
                    {
                        ProfileWindow.Instance?.SubmitProfileData(false);
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug("ProfilesPageEditor SubmitProfileData: " + ex.Message);
                    }
                }
                AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_SaveBtn);
            }

            ImGui.SameLine();
            if (RsElements.Button("Preview Profile##prof.actions.preview", RsElements.ButtonVariant.Ghost))
            {
                try
                {
                    // Opens your own profile the way other players see it.
                    TargetProfileWindow.RequestingProfile = true;
                    TargetProfileWindow.ResetAllData();
                    Plugin.plugin.OpenTargetWindow();
                    Profiles_DS.FetchProfile(character, false, ProfileWindow.profileIndex, character.characterName, character.characterWorld, -1);
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("ProfilesPageEditor PreviewProfile: " + ex.Message);
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("See this profile as other players do. Shows the last saved version, so save first.");

            ImGui.Dummy(new Vector2(0f, RsTheme.S(6f)));

            if (RsElements.Button("Backup##prof.actions.backup", RsElements.ButtonVariant.Ghost))
            {
                try
                {
                    ProfileWindow.Instance?.LoadBackupSaveDialog();
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("ProfilesPageEditor LoadBackupSaveDialog: " + ex.Message);
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Save a local backup of your profile.");

            ImGui.SameLine();
            if (RsElements.Button("Load Backup##prof.actions.loadBackup", RsElements.ButtonVariant.Ghost))
            {
                try
                {
                    ProfileWindow.Instance?.LoadBackupLoaderDialog();
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("ProfilesPageEditor LoadBackupLoaderDialog: " + ex.Message);
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Loading a backup can completely replace this profile. If you do not wish to overwrite it, please make a new profile to load onto.");
        }
    }

    // Loading overlay - themed replacement for ProfileWindow's private DrawProfileLoadingOverlay. Card centered inside the current window using the current draw list; readable in both themes.

    public static void DrawLoadingOverlay()
    {
        var winPos  = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        var dl      = ImGui.GetWindowDrawList();

        // Dim scrim across the whole page.
        var scrim = new Vector4(RsTheme.BgPrimary.X, RsTheme.BgPrimary.Y, RsTheme.BgPrimary.Z, 0.85f);
        dl.AddRectFilled(winPos, winPos + winSize, ImGui.ColorConvertFloat4ToU32(scrim));

        var tabsLoading = ProfileWindowState.TabsCount > 0
                          && ProfileWindowState.LoadedTabs < ProfileWindowState.TabsCount;
        var galleryLoading = ProfileWindowState.GalleryToLoad > 0
                             && ProfileWindowState.LoadedGallery < ProfileWindowState.GalleryToLoad;

        var cardW = MathF.Min(winSize.X - RsTheme.S(40f), RsTheme.S(340f));
        var lineH = RsTheme.S(24f);
        int lines = 1; // title
        if (ProfileWindow.Sending) lines++;
        if (ProfileWindow.Fetching && !tabsLoading && !galleryLoading) lines++;
        if (tabsLoading) lines += 2;
        if (galleryLoading) lines += 2;
        var cardH = RsTheme.S(40f) + lines * lineH;

        var cardX = winPos.X + (winSize.X - cardW) * 0.5f;
        var cardY = winPos.Y + (winSize.Y - cardH) * 0.5f;
        var cardMin = new Vector2(cardX, cardY);
        var cardMax = new Vector2(cardX + cardW, cardY + cardH);

        dl.AddRectFilled(cardMin, cardMax, RsTheme.U.BgSecondary, RsTheme.CornerRadius);
        dl.AddRect(cardMin, cardMax, RsTheme.U.Border, RsTheme.CornerRadius, ImDrawFlags.None, RsTheme.BorderThickness);

        var pad = RsTheme.S(16f);
        var y = cardY + RsTheme.S(14f);

        // Title.
        ImGui.SetCursorScreenPos(new Vector2(cardX + pad, y));
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentPrimary);
        try { ImGui.TextUnformatted("Loading Profile..."); }
        finally { ImGui.PopStyleColor(); }
        y += lineH;

        if (ProfileWindow.Sending)
        {
            DrawOverlayMutedLine(cardX + pad, y, "Sending data to server...");
            y += lineH;
        }

        if (tabsLoading)
        {
            DrawOverlayMutedLine(cardX + pad, y,
                $"Loading tabs ({ProfileWindowState.LoadedTabs}/{ProfileWindowState.TabsCount})...");
            y += lineH;

            var frac = ProfileWindowState.TabsCount > 0
                ? (float)ProfileWindowState.LoadedTabs / ProfileWindowState.TabsCount
                : 0f;
            DrawOverlayProgress(dl, cardX + pad, y, cardW - pad * 2f, frac);
            y += lineH;
        }

        if (galleryLoading)
        {
            DrawOverlayMutedLine(cardX + pad, y,
                $"Loading images ({ProfileWindowState.LoadedGallery}/{ProfileWindowState.GalleryToLoad})...");
            y += lineH;

            var frac = ProfileWindowState.GalleryToLoad > 0
                ? (float)ProfileWindowState.LoadedGallery / ProfileWindowState.GalleryToLoad
                : 0f;
            DrawOverlayProgress(dl, cardX + pad, y, cardW - pad * 2f, frac);
            y += lineH;
        }

        if (ProfileWindow.Fetching && !tabsLoading && !galleryLoading)
        {
            DrawOverlayMutedLine(cardX + pad, y, "Waiting for server...");
            y += lineH;
        }
    }

    // Save progress overlay - themed variant of ProfileWindow's private DrawSaveProgressOverlay.

    public static void DrawSaveProgressOverlay()
    {
        var winPos  = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        var dl      = ImGui.GetWindowDrawList();

        var scrim = new Vector4(RsTheme.BgPrimary.X, RsTheme.BgPrimary.Y, RsTheme.BgPrimary.Z, 0.85f);
        dl.AddRectFilled(winPos, winPos + winSize, ImGui.ColorConvertFloat4ToU32(scrim));

        var cardW = MathF.Min(winSize.X - RsTheme.S(40f), RsTheme.S(320f));
        var cardH = RsTheme.S(120f);
        var cardX = winPos.X + (winSize.X - cardW) * 0.5f;
        var cardY = winPos.Y + (winSize.Y - cardH) * 0.5f;
        var cardMin = new Vector2(cardX, cardY);
        var cardMax = new Vector2(cardX + cardW, cardY + cardH);

        dl.AddRectFilled(cardMin, cardMax, RsTheme.U.BgSecondary, RsTheme.CornerRadius);
        dl.AddRect(cardMin, cardMax, RsTheme.U.Border, RsTheme.CornerRadius, ImDrawFlags.None, RsTheme.BorderThickness);

        var pad = RsTheme.S(16f);
        var y = cardY + RsTheme.S(14f);

        if (ProfileSaveTracker.HasError)
        {
            ImGui.SetCursorScreenPos(new Vector2(cardX + pad, y));
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
            try { ImGui.TextUnformatted("Save Failed"); }
            finally { ImGui.PopStyleColor(); }

            ImGui.SetCursorScreenPos(new Vector2(cardX + pad, y + RsTheme.S(26f)));
            ImGui.PushTextWrapPos(cardX + cardW - pad);
            DrawOverlayMutedLine(cardX + pad, y + RsTheme.S(26f), ProfileSaveTracker.ErrorMessage ?? string.Empty);
            ImGui.PopTextWrapPos();
            return;
        }

        // Title (accent when working, success when done).
        ImGui.SetCursorScreenPos(new Vector2(cardX + pad, y));
        var titleColor = ProfileSaveTracker.Progress >= 1f
            ? RsTheme.AccentSuccess
            : RsTheme.AccentPrimary;
        var titleText = ProfileSaveTracker.Progress >= 1f
            ? "Profile Saved!"
            : "Saving Profile...";
        ImGui.PushStyleColor(ImGuiCol.Text, titleColor);
        try { ImGui.TextUnformatted(titleText); }
        finally { ImGui.PopStyleColor(); }

        // Current-step subtitle.
        DrawOverlayMutedLine(cardX + pad, cardY + RsTheme.S(40f), ProfileSaveTracker.CurrentStep ?? string.Empty);

        // Progress bar.
        DrawOverlayProgress(dl, cardX + pad, cardY + RsTheme.S(66f), cardW - pad * 2f, ProfileSaveTracker.Progress);

        // Step counter.
        DrawOverlayMutedLine(cardX + pad, cardY + RsTheme.S(92f),
            $"Step {ProfileSaveTracker.CompletedSteps} of {ProfileSaveTracker.TotalSteps}");
    }

    // Tab body - dispatches to the appropriate layout renderer inside a themed panel.

    public static void DrawTabBody(Character character, ProfileData profile, CustomTab tab, int tabIndex)
    {
        if (tab == null) return;

        var name = string.IsNullOrEmpty(tab.Name) ? $"Tab {tabIndex}" : tab.Name;
        var uniqueId = $"{name}##{tabIndex}";

        if (!RsElements.BeginPanel("prof.tab_body_" + tabIndex, name, default, false, true))
        {
            RsElements.EndPanel();
            return;
        }

        try
        {
            if (tab.Layout == null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                try { ImGui.TextUnformatted("(no layout for this tab)"); }
                finally { ImGui.PopStyleColor(); }
                return;
            }

            try
            {
                switch (tab.Layout)
                {
                    case BioLayout bioLayout:
                        Bio.RenderBioLayout(tabIndex, uniqueId, bioLayout);
                        break;
                    case DetailsLayout detailsLayout:
                        Details.RenderDetailsLayout(tabIndex, uniqueId, detailsLayout);
                        break;
                    case GalleryLayout galleryLayout:
                        Gallery.RenderGalleryLayout(tabIndex, uniqueId, galleryLayout);
                        break;
                    case InfoLayout infoLayout:
                        Info.RenderInfoLayout(tabIndex, uniqueId, infoLayout);
                        break;
                    case StoryLayout storyLayout:
                        Story.RenderStoryLayout(tabIndex, uniqueId, storyLayout);
                        break;
                    case InventoryLayout inventoryLayout:
                        Inventory.RenderInventoryLayout(tabIndex, uniqueId, inventoryLayout);
                        break;
                    case TreeLayout treeLayout:
                        Tree.RenderTreeLayout(tabIndex, true, uniqueId, treeLayout, string.Empty, new Vector4(0, 0, 0, 0));
                        break;
                    default:
                        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                        try { ImGui.TextUnformatted($"(unsupported layout: {tab.Layout.GetType().Name})"); }
                        finally { ImGui.PopStyleColor(); }
                        break;
                }
            }
            catch (Exception exLayout)
            {
                Plugin.PluginLog.Debug(
                    $"ProfilesPageEditor DrawTabBody: exception rendering '{name}' index={tabIndex}: {exLayout}");
            }
        }
        finally
        {
            RsElements.EndPanel();
        }
    }

    // Local helpers

    private static float EstimateButtonWidth(string label)
    {
        // Mirrors RsElements.Button's internal padding (18px x, scaled).
        var pad = RsTheme.S(18f) * 2f;
        return ImGui.CalcTextSize(label).X + pad;
    }

    private static void CenterNextItem(float itemWidth)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var x = MathF.Max(0f, (avail - itemWidth) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + x);
    }

    private static void DrawOverlayMutedLine(float x, float y, string text)
    {
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        try { ImGui.TextUnformatted(text ?? string.Empty); }
        finally { ImGui.PopStyleColor(); }
    }

    private static void DrawOverlayProgress(ImDrawListPtr dl, float x, float y, float width, float frac01)
    {
        frac01 = Math.Clamp(frac01, 0f, 1f);
        var h = RsTheme.S(8f);
        var radius = h * 0.5f;
        var min = new Vector2(x, y);
        var max = new Vector2(x + width, y + h);
        dl.AddRectFilled(min, max, RsTheme.U.BgTertiary, radius);
        if (frac01 > 0f)
        {
            var fillMax = new Vector2(x + width * frac01, y + h);
            var col = frac01 >= 1f ? RsTheme.U.AccentSuccess : RsTheme.U.AccentPrimary;
            dl.AddRectFilled(min, fillMax, col, radius);
        }
        dl.AddRect(min, max, RsTheme.U.Border, radius, ImDrawFlags.None, RsTheme.BorderThickness);
    }

    // Small facade over DataReceiver's public counters so the overlay reads the current loading state without leaking DataReceiver into every caller. Keeps this file self-contained if the DataReceiver field names ever change.
    private static class ProfileWindowState
    {
        public static int TabsCount     => Profiles_DR.tabsCount;
        public static int LoadedTabs    => Profiles_DR.loadedTabsCount;
        public static int GalleryToLoad => Profiles_DR.GalleryImagesToLoad;
        public static int LoadedGallery => Profiles_DR.loadedGalleryImages;
    }

    /// Copy of the 4x4 transparent PNG factory used by ProfileWindow.CreateTransparentPng - needed here because the original is private. Kept byte-identical so a background scrub produces the same bytes either way.
    private static byte[] CreateTransparentPng() => new byte[]
    {
        // PNG signature.
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        // IHDR chunk.
        0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x04,
        0x00, 0x00, 0x00, 0x04,
        0x08, 0x06, 0x00, 0x00, 0x00,
        0x90, 0x6B, 0x0B, 0x5C,
        // IDAT chunk.
        0x00, 0x00, 0x00, 0x1B,
        0x49, 0x44, 0x41, 0x54,
        0x78, 0x9C, 0x62, 0x60, 0x60, 0x60, 0x60, 0x60,
        0x00, 0x02, 0x06, 0x20, 0x08, 0x00, 0x00, 0x00,
        0x00, 0x04, 0x00, 0x01, 0x00, 0x00, 0x15, 0x7F,
        0x00, 0x11,
        0x11, 0x94, 0x19, 0x8E,
        // IEND chunk.
        0x00, 0x00, 0x00, 0x00,
        0x49, 0x45, 0x4E, 0x44,
        0xAE, 0x42, 0x60, 0x82,
    };
}

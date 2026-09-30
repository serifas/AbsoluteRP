using System;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Helpers;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Windows.Profiles
{
    // "Ask to link profiles": opened from someone's profile. The viewer picks which of their own profiles is linking and types the position they are proposing ("Father", "FC Leader"). The other owner has to accept.
    public static class RelationshipRequestPopup
    {
        private static bool _open;
        private static string _targetName = string.Empty, _targetWorld = string.Empty, _targetTitle = string.Empty;
        private static int _targetProfileId;
        private static string _position = string.Empty, _note = string.Empty, _status = string.Empty;
        private static int _senderIdx;
        private static bool _profilesAsked;
        private static bool _targetIsMine;

        public static void OpenForCurrentTarget()
        {
            var pd = TargetProfileWindow.profileData;
            _targetName = TargetProfileWindow.characterName ?? pd?.playerName ?? string.Empty;
            _targetWorld = TargetProfileWindow.characterWorld ?? pd?.playerWorld ?? string.Empty;
            _targetTitle = pd?.title ?? string.Empty;
            _targetProfileId = pd?.id ?? 0;
            _targetIsMine = TargetProfileWindow.IsOwnProfile
                || (Plugin.character != null && string.Equals(_targetName, Plugin.character.characterName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(_targetWorld, Plugin.character.characterWorld, StringComparison.OrdinalIgnoreCase));
            _position = string.Empty; _note = string.Empty; _status = string.Empty;
            _profilesAsked = false;
            _open = true;
            RelationshipManager.EnsureFetched(true);
        }

        public static void Draw()
        {
            if (!_open) return;
            var profiles = ProfileWindow.profiles ?? new System.Collections.Generic.List<ProfileData>();
            if (profiles.Count == 0 && !_profilesAsked && Plugin.character != null)
            {
                _profilesAsked = true;
                Profiles_DS.FetchProfiles(Plugin.character);
            }

            ImGui.SetNextWindowSize(new Vector2(RsTheme.S(420f), 0f), ImGuiCond.Always);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, RsTheme.BgSecondary);
            var shown = ImGui.Begin("Link profiles###arp_bond_request", ref _open, ImGuiWindowFlags.NoCollapse);
            ImGui.PopStyleColor();
            try
            {
                if (!shown) return;
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                ImGui.TextWrapped(_targetIsMine
                    ? $"Link {(string.IsNullOrWhiteSpace(_targetTitle) ? "this profile" : _targetTitle)} with another of your profiles."
                    : $"Ask {(string.IsNullOrWhiteSpace(_targetTitle) ? _targetName : _targetTitle)} to link with one of your profiles.");
                ImGui.PopStyleColor();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped(_targetIsMine
                    ? "Links between your own profiles are accepted at once. Each can then add the other as a relationship node on a Relationship tab."
                    : "They have to accept. After that you can both add each other as a relationship node on a Relationship tab.");
                ImGui.PopStyleColor();
                ImGui.Spacing();

                // An existing link with this profile: say so instead of stacking requests.
                var existing = _targetIsMine ? null : RelationshipManager.All.FirstOrDefault(b =>
                    string.Equals(b.PeerName, _targetName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(b.PeerWorld, _targetWorld, StringComparison.OrdinalIgnoreCase) &&
                    b.Status != (int)RelationshipStatus.Declined);
                if (existing != null)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                    ImGui.TextWrapped(existing.Status == (int)RelationshipStatus.Accepted
                        ? $"You are already linked as \"{existing.Title}\". Sending again asks for another link."
                        : $"A request as \"{existing.Title}\" is still waiting.");
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                }

                if (profiles.Count == 0)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("Loading your profiles…");
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("YOUR PROFILE:");
                    ImGui.PopStyleColor();
                    // Linking one of your own profiles: the target itself is left out of the choices.
                    if (_targetIsMine) profiles = profiles.Where(p => p.id != _targetProfileId || _targetProfileId <= 0).ToList();
                    if (profiles.Count == 0)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                        ImGui.TextWrapped("You need a second profile to link this one with.");
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        _senderIdx = Math.Clamp(_senderIdx, 0, profiles.Count - 1);
                        var labels = profiles.Select(p => string.IsNullOrWhiteSpace(p.title) ? "(untitled profile)" : p.title).ToList();
                        RsElements.Dropdown("bond_req_sender", ref _senderIdx, labels, 380f);
                    }
                }

                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("THEIR POSITION (what they are to you):");
                ImGui.PopStyleColor();
                RsElements.InputText("bond_req_position", ref _position, 60, placeholder: _targetIsMine ? "Sister, Twin, Mentor, Alt…" : "Father, Sister, FC Leader, Rival…", width: 380f);

                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("NOTE (optional):");
                ImGui.PopStyleColor();
                RsElements.InputText("bond_req_note", ref _note, 120, placeholder: "Anything you want them to know", width: 380f);

                ImGui.Spacing();
                bool can = profiles.Count > 0 && !string.IsNullOrWhiteSpace(_position) && !string.IsNullOrWhiteSpace(_targetName) && Plugin.character != null;
                if (!can) ImGui.BeginDisabled();
                if (RsElements.Button((_targetIsMine ? "Link" : "Send request") + "##bond_req_send", RsElements.ButtonVariant.Primary))
                {
                    var mine = profiles[_senderIdx];
                    Relationships_DS.SendRelationshipRequest(Plugin.character, mine.index, _targetName, _targetWorld, -1, _position.Trim(), _note.Trim(), _targetProfileId);
                    _status = _targetIsMine ? "Linked. Both profiles can now add each other as a relationship node." : "Request sent. It shows under Profile links on your Relationship tab.";
                    _position = string.Empty; _note = string.Empty;
                }
                if (!can) ImGui.EndDisabled();
                ImGui.SameLine();
                if (RsElements.Button("Close##bond_req_close", RsElements.ButtonVariant.Ghost)) _open = false;

                if (!string.IsNullOrEmpty(_status))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentSuccess);
                    ImGui.TextWrapped(_status);
                    ImGui.PopStyleColor();
                }
            }
            finally { ImGui.End(); }
        }
    }
}

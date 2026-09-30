using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.RsUI.Pages;

public sealed class QuestsPage : IPage
{
    public string Id => "quests";
    public string Title => "Quests";
    public FontAwesomeIcon Icon => FontAwesomeIcon.Scroll;

    public void Draw()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.Text("Quests");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.Text("Work in progress");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
        ImGui.TextWrapped("Just like it is in the original for now.");
        ImGui.PopStyleColor();
    }
}

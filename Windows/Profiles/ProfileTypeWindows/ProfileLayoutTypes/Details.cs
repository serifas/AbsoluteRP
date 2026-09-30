using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;

namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    // Details tab layout - displays editable key-value fields (hooks) for character details
    internal class Details
    {

        public static void RenderDetailsLayout(int index, string uniqueID, DetailsLayout layout)
        {
            /*
            bool viewable = layout.viewable;
            if (ImGui.Checkbox($"Viewable##Viewable{layout.id}", ref viewable))
            {
                layout.viewable = viewable;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("If checked, this tab will be viewable by others.\nIf unchecked, it will not be displayed.");
            }*/

            if (RsElements.Button("Add", RsElements.ButtonVariant.Primary))
            {
                Detail detail = new Detail
                {
                    name = "New Detail",
                    content = string.Empty
                };
                layout.details.Add(detail);
            }
            ImGui.NewLine();
            for (var i = 0; i < layout.details.Count; i++)
            {
                layout.details[i].id = i; // Ensure each detail has a unique ID based on its index
                DrawDetail(layout.details[i], layout);
            }
        }
        public static void RenderDetailPreview(DetailsLayout layout, Vector4 TitleColor)
        {

            Misc.SetTitle(Plugin.plugin, true, layout.name, TitleColor);
            foreach(Detail detail in layout.details)
            {
                Misc.RenderHtmlColoredTextInline(detail.name.ToUpper(), 400);
                Misc.RenderHtmlElements(detail.content, true, true, true, false);
            }
        }
        public static void DrawDetail(Detail detail, DetailsLayout layout)
        {
            if (detail != null)
            {

                var innerAvail = RsElements.AvailContentWidth();
                using var detailChild = ImRaii.Child("##Detail" + detail.id, new Vector2(innerAvail, 350));
                if (detailChild)
                {
                    string name = detail.name;
                    string content = detail.content;
                    var w = ImGui.GetContentRegionAvail().X;
                    if (RsElements.InputText("##DetailName" + detail.id, ref name, 300, "Name", width: w / RsTheme.Scale)) { detail.name = name; }
                    if (RsElements.InputTextArea("##DetailContent" + detail.id, ref content, 5000, size: new Vector2(w, 200))) { detail.content = content; }

                    try
                    {

                        using var detailControlsTable = ImRaii.Child("##DetailControls" + detail.id);
                        if (detailControlsTable)
                        {
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (RsElements.Button("Remove##" + "detail" + detail.id, RsElements.ButtonVariant.Danger))
                                {
                                    Detail toRemove = layout.details.Find(d => d.id == detail.id);
                                    layout.details.Remove(toRemove);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

    }
}

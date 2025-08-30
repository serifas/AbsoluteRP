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

            if (ImGui.Button("Add"))
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

                using var detailChild = ImRaii.Child("##Detail" + detail.id, new Vector2(ImGui.GetWindowSize().X, 350));
                if (detailChild)
                {
                    string name = detail.name;
                    string content = detail.content;
                    if(ImGui.InputTextWithHint("##DetailName" + detail.id, "Name", ref name, 300)) { detail.name = name; }
                    if(ImGui.InputTextMultiline("##DetailContent" + detail.id, ref content, 5000, new Vector2(ImGui.GetWindowSize().X - 20, 200))) {detail.content = content;}

                    try
                    {

                        using var detailControlsTable = ImRaii.Child("##DetailControls" + detail.id);
                        if (detailControlsTable)
                        {
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Remove##" + "detail" + detail.id))
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

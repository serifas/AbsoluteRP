using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTabs
{
    internal class HooksTab
    {
        public static string[] HookNames = new string[31];
        public static string[] HookContents = new string[31];
        public static int hookCount = 0;
        public static bool AddHooks = false;
        public static bool[] hookExists = new bool[31];
        public static bool ReorderHooks;
        public static void LoadHooksTab()
        {
            if (ImGui.Button("Add Hook"))
            {
                if (hookCount < 30)
                {
                    hookCount++;
                }
            }

            ImGui.NewLine();
            AddHooks = true;
            hookExists[hookCount] = true;
        }
        public static void DrawHooksUI(Plugin plugin, int hookCount)
        {
            for (var i = 0; i < hookCount; i++)
            {
                DrawHook(i, plugin);
            }
        }
        public static void ReorderHooksData(Plugin plugin)
        {

            var nextHookExists = hookExists[NextAvailableHookIndex() + 1];
            var firstHookOpen = NextAvailableHookIndex();
            hookExists[firstHookOpen] = true;
            if (nextHookExists)
            {
                for (var i = firstHookOpen; i < HooksTab.hookCount; i++)
                {
                    HooksTab.HookNames[i] = HooksTab.HookNames[i + 1];
                    HooksTab.HookContents[i] = HooksTab.HookContents[i + 1];

                }
            }

            HooksTab.hookCount--;
            HooksTab.HookNames[HooksTab.hookCount] = string.Empty;
            HooksTab.HookContents[HooksTab.hookCount] = string.Empty;
            HooksTab.hookExists[HooksTab.hookCount] = false;
        }
        //gets the next hook index that does not exist
        public static int NextAvailableHookIndex()
        {
            var load = true;
            var index = 0;
            for (var i = 0; i < hookExists.Length; i++)
            {
                if (hookExists[i] == false && load == true)
                {
                    load = false;
                    index = i;
                    return index;
                }
            }
            return index;
        }
        public static void DrawHook(int i, Plugin plugin)
        {
            if (hookExists[i] == true)
            {

                using var hookChild = ImRaii.Child("##Hook" + i, new Vector2(ImGui.GetWindowSize().X, 350));
                if (hookChild)
                {
                    ImGui.InputTextWithHint("##HookName" + i, "Hook Name", ref HookNames[i], 300);
                    ImGui.InputTextMultiline("##HookContent" + i, ref HookContents[i], 5000, new Vector2(ImGui.GetWindowSize().X - 20, 200));

                    try
                    {

                        using var hookControlsTable = ImRaii.Child("##HookControls" + i);
                        if (hookControlsTable)
                        {
                            using (OtterGui.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Remove##" + "hook" + i))
                                {
                                    hookExists[i] = false;
                                    ReorderHooks = true;
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

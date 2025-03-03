using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTabs
{
    internal class StoryTab
    {
        public static string[] ChapterContents = new string[31];
        public static string[] ChapterNames = new string[31];
        public static int currentChapter;
        public static int storyChapterCount = -1;
        public static bool drawChapter;
        public static int chapterCount;
        public static bool AddStoryChapter;
        public static string storyTitle = string.Empty;
        public static bool[] storyChapterExists = new bool[31]; //same again but for story chapters
        public static bool[] viewChapter = new bool[31]; //to check which chapter we are currently viewing
        public static bool ReorderChapters;
        public static void LoadStoryTab()
        {
            ImGui.Text("Story Title");
            ImGui.SameLine();
            ImGui.InputText("##storyTitle", ref storyTitle, 35);

            ImGui.Text("Chapter");
            ImGui.SameLine();
            //add our chapter combo select input
            AddChapterSelection();
            ImGui.SameLine();
            if (ImGui.Button("Add Chapter"))
            {
                CreateChapter();
            }
            ImGui.NewLine();
        }
        public static void AddChapterSelection()
        {
            var chapterName = ChapterNames[currentChapter];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Chapter", chapterName);
            if (!combo)
                return;
            foreach (var (newText, idx) in ChapterNames.WithIndex())
            {
                var label = newText;
                if (label == string.Empty)
                {
                    label = "New Chapter";
                }
                if (newText != string.Empty)
                {
                    if (ImGui.Selectable(label + "##" + idx, idx == currentChapter))
                    {
                        currentChapter = idx;
                        storyChapterExists[currentChapter] = true;
                        viewChapter[currentChapter] = true;
                        drawChapter = true;
                    }
                    ImGuiUtil.SelectableHelpMarker("Select to edit chapter");
                }
            }
        }
        public static void CreateChapter()
        {
            if (storyChapterCount < 30)
            {
                storyChapterCount++; //increase chapter count
                storyChapterExists[storyChapterCount] = true; //set our chapter to exist
                ChapterNames[storyChapterCount] = "New Chapter"; //set a base title
                currentChapter = storyChapterCount; //switch our current selected chapter to the one we just made
                viewChapter[storyChapterCount] = true; //view the chapter we just made aswell
            }

        }
        public static void RemoveChapter(int index)
        {
            if (index < 0 || index >= ChapterNames.Length)
                return; // Prevent invalid index access.

            if (storyChapterCount < 0)
                return; // Ensure there are chapters to remove.

            // Shift all elements after the removed chapter
            for (int i = index; i < storyChapterCount; i++)
            {
                ChapterNames[i] = ChapterNames[i + 1];
                ChapterContents[i] = ChapterContents[i + 1];
                storyChapterExists[i] = storyChapterExists[i + 1];
                viewChapter[i] = viewChapter[i + 1];
            }

            // Clear the last slot since everything shifted
            ChapterNames[storyChapterCount] = string.Empty;
            ChapterContents[storyChapterCount] = string.Empty;
            storyChapterExists[storyChapterCount] = false;
            viewChapter[storyChapterCount] = false;

            // Reduce chapter count
            storyChapterCount--;

            // Set the new current chapter
            if (storyChapterCount >= 0)
            {
                currentChapter = Math.Max(0, index - 1);
                viewChapter[currentChapter] = true;
            }
            else
            {
                // No more chapters exist
                currentChapter = 0;
            }

            ReorderChapters = true;
        }



        public static void ReorderChapterData(Plugin plugin)
        {
            var nextChapterExists = storyChapterExists[NextAvailableChapterIndex() + 1];
            var firstChapterOpen = NextAvailableChapterIndex();
            storyChapterExists[firstChapterOpen] = true;
            if (nextChapterExists)
            {
                for (var i = firstChapterOpen; i < storyChapterCount; i++)
                {
                    ChapterNames[i] = ChapterNames[i + 1];
                    ChapterContents[i] = ChapterContents[i + 1];
                    DrawChapter(i, plugin);
                }
            }
        }
        public static void ResetStory()
        {
            for (var s = 0; s <= storyChapterCount; s++)
            {
                ChapterNames[s] = string.Empty;
                ChapterContents[s] = string.Empty;
                chapterCount = 0;
                storyChapterExists[s] = false;
            }



            currentChapter = 0;
            chapterCount = 0;
            storyChapterCount = -1;
            storyTitle = string.Empty;
        }

        public static int NextAvailableChapterIndex()
        {
            var load = true;
            var index = 0;
            for (var i = 0; i < storyChapterExists.Length; i++)
            {
                if (storyChapterExists[i] == false && load == true)
                {
                    load = false;
                    index = i;
                    return index;
                }
            }
            return index;
        }

        public void ClearChaptersInView() //not used at the moment
        {
            for (var i = 0; i < viewChapter.Length; i++)
            {
                viewChapter[i] = false;
            }
        }
        public static void DrawChapter(int i, Plugin plugin)
        {

            if (ProfileWindow.TabOpen[TabValue.Story] == true && i >= 0)
            {
                //if our chapter exists and we are viewing it
                if (storyChapterExists[i] == true && viewChapter[i] == true)
                {
                    //create a new child with the scale of the window size but inset slightly
                    var windowSize = ImGui.GetWindowSize();
                    using var profileTable = ImRaii.Child("##Chapter" + i, new Vector2(windowSize.X - 20, windowSize.Y - 130));
                    if (profileTable)
                    {
                        ImGui.TextUnformatted("Chapter Name:");
                        ImGui.SameLine();
                        ImGui.InputText(string.Empty, ref ChapterNames[i], 100);
                        //set an input size for our input text as well to adjust with window scale
                        var inputSize = new Vector2(windowSize.X - 30, windowSize.Y / 1.7f); // Adjust as needed
                        //ChapterContents[i] = Misc.WrapTextToFit(ChapterContents[i], inputSize.X);
                        ImGui.InputTextMultiline("##ChapterContent" + i, ref ChapterContents[i], 50000, inputSize);
                        // Display InputTextMultiline and detect changes


                        using var chapterControlTable = ImRaii.Child("##ChapterControls" + i);
                        if (chapterControlTable)
                        {
                            using (OtterGui.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Remove##" + "chapter" + i))
                                {
                                    RemoveChapter(i);
                                }

                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }


                        }
                    }


                }
            }
        }



    }
}

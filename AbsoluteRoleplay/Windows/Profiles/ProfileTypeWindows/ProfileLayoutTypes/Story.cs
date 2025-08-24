using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using System.Collections.Generic;
using System.Numerics;
namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    public class Story
    {
        public static string[] ChapterContents = new string[31];
        public static string[] ChapterNames = new string[31];
        public static int currentChapter;
        public static int storyChapterCount = -1;
        public static bool drawChapter;
        public static bool AddStoryChapter;
        public static string storyTitle = string.Empty;
        public static bool ReorderChapters;
        private static bool viewable = true;

        public static void RenderStoryPreview(StoryLayout storyLayout, Vector4 TitleColor)
        {
            Misc.SetTitle(Plugin.plugin, true, storyLayout.name, TitleColor);

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0.8f));
            using (ImRaii.Child("StoryNavigation", new Vector2(ImGui.GetWindowSize().X, ImGui.GetIO().FontGlobalScale * 32), true))
            {
                if (currentChapter > 0)
                {
                    if (ImGui.Button("《 "))
                    {
                        currentChapter--;
                    }
                }

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetWindowSize().X / 2 - ImGui.CalcTextSize(storyLayout.chapters[currentChapter].title.ToUpper()).X / 2);
                ImGui.TextUnformatted(storyLayout.chapters[currentChapter].title.ToUpper());
                ImGui.SameLine();
                if (currentChapter > 0 && currentChapter < storyLayout.chapters.Count - 1)
                {
                    ImGui.SameLine();
                }
                if (currentChapter < storyLayout.chapters.Count - 1)
                {
                    Misc.RenderAlignmentToRight(" 》");
                    if (ImGui.Button(" 》"))
                    {
                        currentChapter++;
                    }
                }
            }

            ImGui.PopStyleColor();
            using (ImRaii.Child("StoryContent", new Vector2(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y / 2), true))
            {
                Misc.RenderHtmlElements(storyLayout.chapters[currentChapter].content, true, true, true, false);
            }
        }

        public static void RenderStoryLayout(int profileIndex, string uniqueID, StoryLayout layout)
        {
            /*
            ImGui.Checkbox($"Viewable##Viewable{layout.id}", ref viewable);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("If checked, this tab will be viewable by others.\nIf unchecked, it will not be displayed.");
            }
            */
            ImGui.Text("Story Title");
            ImGui.SameLine();
            string title = layout.name;
            if (currentChapter >= layout.chapters.Count)
            {
                currentChapter = 0; // Reset to first chapter if current exceeds available chapters
            }
            if (ImGui.InputText($"##storyTitle_{profileIndex}_{uniqueID}", ref title, 35))
            {
                layout.name = title; // Update the title if changed
            }

            if (layout.chapters.Count > 0)
            {
                AddChapterSelection(layout);
                ImGui.SameLine();
                if (ImGui.Button("Add Chapter"))
                {
                    CreateChapter(layout);
                }
                if (currentChapter < layout.chapters.Count && layout.chapters[currentChapter] != null)
                {
                    DrawChapter(currentChapter, layout.chapters[currentChapter], layout, Plugin.plugin);
                }
            }
            else
            {
                if (ImGui.Button("Add Chapter"))
                {
                    CreateChapter(layout);
                }
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "No chapters yet.");
            }
            ImGui.NewLine();
        }

        public static void AddChapterSelection(StoryLayout layout)
        {
            var safeId = layout.id > 0 ? layout.id.ToString() : "default";
            var chapterTitle = layout.chapters[currentChapter].title;
            using var combo = ImRaii.Combo($"##ChapterCombo_{safeId}", chapterTitle);
            if (!combo)
                return;
            foreach (var (newText, idx) in layout.chapters.WithIndex())
            {
                var label = string.IsNullOrEmpty(newText.title) ? "New Chapter" : newText.title;
                if (ImGui.Selectable($"{label}##{safeId}_{idx}", idx == currentChapter))
                {
                    currentChapter = idx;
                    layout.loadChapters = true; // Set to true to load the chapter content
                }
                UIHelpers.SelectableHelpMarker("Select to edit chapter");
            }
        }

        public static void CreateChapter(StoryLayout layout)
        {
            storyChapterCount = layout.chapters.Count - 1;
            storyChapterCount++;
            StoryChapter chapter = new StoryChapter
            {
                id = storyChapterCount,
                title = "New Chapter",
                content = string.Empty
            };
            layout.chapters.Add(chapter);
            currentChapter = storyChapterCount;
            drawChapter = true;
            ReorderChapters = true;
        }

        public static void DrawChapter(int i, StoryChapter chapter, StoryLayout layout, Plugin plugin)
        {
            var safeId = layout.id > 0 ? layout.id.ToString() : "default";
            var windowSize = ImGui.GetWindowSize();
            using var profileTable = ImRaii.Child($"Chapter_{safeId}_{i}", new Vector2(windowSize.X - 20, windowSize.Y - 130));
            if (profileTable)
            {
                ImGui.TextUnformatted("Chapter Name:");
                ImGui.SameLine();
                string chapterTitle = chapter.title;
                if (ImGui.InputText($"##ChapterTitle_{safeId}_{i}", ref chapterTitle, 100))
                {
                    chapter.title = chapterTitle; // Update the title if changed
                }
                var inputSize = new Vector2(windowSize.X - 30, windowSize.Y / 1.7f);
                string chapterContent = chapter.content;
                if (ImGui.InputTextMultiline($"##ChapterContent_{safeId}_{i}", ref chapterContent, 50000, inputSize))
                {
                    chapter.content = chapterContent; // Update the content if changed
                }

                using var chapterControlTable = ImRaii.Child($"ChapterControls_{safeId}_{i}");
                if (chapterControlTable)
                {
                    using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                    {
                        if (ImGui.Button($"Remove##{safeId}_chapter_{i}"))
                        {
                            layout.chapters.Remove(chapter);
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

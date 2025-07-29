using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiFontChooserDialog;
using Dalamud.Interface.Internal;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Storage.Assets;
using FFXIVClientStructs.FFXIV.Common.Lua;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Configuration;
using OtterGui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace AbsoluteRoleplay
{
    public class Misc
    {
        private static string previousInputText = "";
        private static float previousBoxWidth = 0f;
        private static string cachedWrappedText = ""; // Buffer for displaying wrapped text
        public static IFontHandle Jupiter;
        public static float _modVersionWidth;
        public static int loaderIndex = 0;
        private static Random random = new Random();

        public static List<ImFontPtr> availableFonts = new();
        public static ImFontPtr selectedFont;
        private static int selectedFontIndex = 0;

        public static float ConvertToPercentage(float value)
        {
            // Clamp the value between 0 and 100
            value = Math.Max(0f, Math.Min(100f, value));

            // Return the percentage
            return value / 100f * 100f;
        }
        //sets position of content to center
        public static void RenderHtmlColoredTextInline(string text)
        {
            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.LoadHtml(text);

            bool first = true;
            foreach (var node in htmlDoc.DocumentNode.ChildNodes)
            {
                string nodeText = node.InnerText.Replace("\r", "");
                string[] lines = nodeText.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    // Only use SameLine if not the first segment and not after a line break
                    if (!first) ImGui.SameLine(0, 0);

                    if (node.Name == "color" && node.Attributes["hex"] != null)
                    {
                        var hexColor = node.Attributes["hex"].Value;
                        if (TryParseHexColor(hexColor, out Vector4 color))
                            ImGui.TextColored(color, lines[i]);
                        else
                            ImGui.TextUnformatted(lines[i]);
                    }
                    else
                    {
                        ImGui.TextUnformatted(lines[i]);
                    }

                    // If this is a line break, reset first so next segment starts a new line
                    first = (i < lines.Length - 1);
                }
            }
        }
        private static bool TryParseHexColor(string hex, out Vector4 color)
        {
            color = new Vector4(1, 1, 1, 1);
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);
            if (hex.Length != 6)
                return false;
            if (int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) &&
                int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) &&
                int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
            {
                color = new Vector4(r / 255f, g / 255f, b / 255f, 1);
                return true;
            }
            return false;
        }
        public static void SetCenter(Plugin plugin, string name)
        {
         
                int NameWidth = name.Length * 6;
                var decidingWidth = Math.Max(500, ImGui.GetWindowWidth());
                var offsetWidth = (decidingWidth - NameWidth) / 2;
                var offsetVersion = name.Length > 0
                    ? _modVersionWidth + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X
                    : 0;
                var offset = Math.Max(offsetWidth, offsetVersion);
                if (offset > 0)
                {
                    ImGui.SetCursorPosX(offset);
                }
        }
        public static string ExtractTextBetweenTags(string input, string tag)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(tag))
                return null;

            string pattern = $@"<{tag}>(.*?)</{tag}>";
            Match match = Regex.Match(input, pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null; // Return null if the tag is not found
        }
        public static void DrawCenteredInput(float center, Vector2 size, string label, string hint, ref string input, uint length, ImGuiInputTextFlags flags)
        {
            var currentCursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(center, currentCursorY));
            ImGui.PushItemWidth(size.X);
            ImGui.InputTextWithHint(label, hint, ref input, length, flags);
            ImGui.PopItemWidth();
        }
        public static bool DrawXCenteredInput(string label, string id, ref string input, uint length)
        {
            var size = ImGui.CalcTextSize(label).X + 400;

            var windowSize = ImGui.GetWindowSize();

            // Set the cursor position to center the button horizontally
            var currentCursorY = ImGui.GetCursorPosY();
            float centeredX = (ImGui.GetContentRegionAvail().X - size) / 2.0f;
            ImGui.SetCursorPos(new Vector2(centeredX, currentCursorY));
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.PushItemWidth(350);
            var centeredInput = ImGui.InputText("##ID" + id, ref input, length);
            ImGui.PopItemWidth();
            return centeredInput;
        }
     
        public static void EditImage(Plugin plugin, FileDialogManager _fileDialogManager, GalleryLayout layout, bool avatar, bool background, int imageIndex)
        {
            _fileDialogManager.OpenFileDialog("Select Image", "Image{.png,.jpg}", (s, f) =>
            {
                if (!s)
                    return;
                var imagePath = f[0].ToString();
                var image = Path.GetFullPath(imagePath);
                var imageBytes = File.ReadAllBytes(image);
                if (avatar == true)
                {
                    ProfileWindow.avatarBytes = imageBytes;
                    ProfileWindow.currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes( ProfileWindow.avatarBytes, 100,100)).Result;
                }
                else if(background == true)
                {
                    ProfileWindow.backgroundBytes = imageBytes;
                    ProfileWindow.backgroundImage = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(ProfileWindow.backgroundBytes, 1000, 1500)).Result;
                }
                else
                {
                    layout.images[imageIndex].imageBytes = imageBytes;
                    layout.images[imageIndex].thumbnail = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(imageBytes, 250, 250)).Result;
                    layout.images[imageIndex].image = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(imageBytes, 2000, 2000)).Result;
                }
            }, 0, null, plugin.Configuration.AlwaysOpenDefaultImport);

        }
      
        public static bool DrawCenteredButton(float center, Vector2 size, string label)
        {
            var currentCursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(center, currentCursorY));
            ImGui.PushItemWidth(size.X);
            var button = DrawButton(label);
            ImGui.PopItemWidth();
            return button;
        }
        public static bool DrawButton(string label)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.3f, 1.0f))) // Light gray on hover
            {
                var button = ImGui.Button(label);
                return button;
            }
        }
        public static void DrawCenteredButtonTable(int rows, List<ProfileTab> profileTabs)
        {
            int columns = profileTabs.Count;
            // Get window size
            var windowSize = ImGui.GetWindowSize();

            // Define button size (width and height)
            var buttonSize = new System.Numerics.Vector2(100, 50);

            // Calculate total width of the button table (button width + padding between buttons)
            float totalTableWidth = (buttonSize.X * columns) + (ImGui.GetStyle().ItemSpacing.X * (columns - 1));

            // Calculate the X position to start drawing the table (centered horizontally)
            float startX = (windowSize.X - totalTableWidth) / 2;

            // Set cursor position to center the table horizontally
            ImGui.SetCursorPosX(startX);

            // Create a table layout for the buttons
            using (var table = ImRaii.Table("ButtonTable", columns))
            {
                if (table)
                {
                    int buttonIndex = 0;
                    for (int row = 0; row < rows; row++)
                    {
                        ImGui.TableNextRow(); // Move to the next row in the table

                        for (int column = 0; column < columns; column++)
                        {
                            ImGui.TableSetColumnIndex(column); // Move to the next column in the table

                            if (buttonIndex < profileTabs.Count)
                            {
                                // Draw a button
                                if (ImGui.Button(profileTabs[buttonIndex].name, buttonSize))
                                {
                                    profileTabs[buttonIndex].action();
                                    profileTabs[buttonIndex].showValue = true;
                                }
                                buttonIndex++;
                            }
                        }
                    }
                }
            }
        }

        
        //sets a title at the center of the window and resets the font back to default afterwards
        public static void SetTitle(Plugin plugin, bool center, string title, Vector4 borderColor)
        {
            Jupiter = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Jupiter, 35));
            
      


            using var col = ImRaii.PushColor(ImGuiCol.Border, borderColor);
            using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 2 * ImGuiHelpers.GlobalScale);
            using var font = Jupiter.Push();
            if(center == true)
            {
                var size = ImGui.CalcTextSize(title);

                var windowSize = ImGui.GetWindowSize();

                // Set the cursor position to center the button horizontally
                float xPos = (windowSize.X - size.X -15) / 2; // Center horizontally
                ImGui.SetCursorPosX(xPos);
            }
            ImGuiUtil.DrawTextButton(title, Vector2.Zero, 0);

            using var defInfFontDen = ImRaii.DefaultFont();
            using var defCol = ImRaii.DefaultColors();
            using var defStyle = ImRaii.DefaultStyle();
        }




        // Helper method to wrap text to fit within a specified width

         // WrapTextToFit now only returns the wrapped text without modifying the original input
    public static string WrapTextToFit(string inputText, float boxWidth)
    {
        // Only re-wrap if input text or box width has changed
        if (inputText == previousInputText && boxWidth == previousBoxWidth)
        {
            return cachedWrappedText;
        }

        // Update cached values
        previousInputText = inputText;
        previousBoxWidth = boxWidth;

        // Remove existing newlines to prevent accumulation of line breaks
        string unwrappedText = inputText.Replace("\n", " ").Trim();

        StringBuilder wrappedText = new StringBuilder();
        StringBuilder lineBuilder = new StringBuilder();
        float lineWidth = 0f;

        // Split by whitespace to preserve spaces in the calculation
        string[] words = unwrappedText.Split(' ');

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word)) continue; // Skip extra spaces
            
            var wordSize = ImGui.CalcTextSize(word + " ").X;

            // Check if adding this word exceeds the box width
            if (lineWidth + wordSize > boxWidth)
            {
                wrappedText.AppendLine(lineBuilder.ToString().TrimEnd()); // Add the line to wrapped text
                lineBuilder.Clear();  // Clear the line builder for the next line
                lineWidth = 0f; // Reset line width
            }

            // Add the word to the current line and update line width
            lineBuilder.Append(word + " ");
            lineWidth += wordSize;
        }

        // Append any remaining text in the line builder
        if (lineBuilder.Length > 0)
        {
            wrappedText.Append(lineBuilder.ToString().TrimEnd());
        }

        // Cache the result for future calls
        cachedWrappedText = wrappedText.ToString();
        return cachedWrappedText;
    }

        //loader for ProfileWindow and TargetWindow
        public static void StartLoader(float value, float max, string loading, Vector2 scale)
        {
            value = Math.Max(0f, Math.Min(100f, value));
           
            ImGui.ProgressBar(value / max, new Vector2(scale.X - 20, ImGui.GetIO().FontGlobalScale * 20), "Loading " + loading);
        }
        public static byte[] ImageToByteArray(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file not found.", imagePath);
            }

            return File.ReadAllBytes(imagePath);
        }
        public static void AddIncrementBar(ImDrawListPtr drawList, float width, Vector4 color)
        {
            var cursorPos = ImGui.GetCursorScreenPos();
            // Set the rectangle size and color
            float height = 10;
            uint colorVal = ImGui.GetColorU32(color); // Solid blue color (RGBA)

            // Draw the outlined rectangle
            drawList.AddRectFilled(new Vector2(cursorPos.X, cursorPos.Y), new Vector2(cursorPos.X + width, cursorPos.Y + height), colorVal);
        }

        public static void RenderAlignmentToRight(string buttonText)
        {
            float windowWidth = ImGui.GetWindowSize().X;
            float scale = ImGui.GetIO().FontGlobalScale;

            // Calculate button width dynamically based on the label text and UI scale
            float buttonWidth = ImGui.CalcTextSize(buttonText).X + (20f * scale); // Add padding to match button appearance

            // Calculate position for right alignment, keeping it within bounds
            float buttonXPosition = Math.Max(0, windowWidth - buttonWidth);

            // Set cursor to the calculated position
            ImGui.SetCursorPosX(buttonXPosition);

        }

    }
}

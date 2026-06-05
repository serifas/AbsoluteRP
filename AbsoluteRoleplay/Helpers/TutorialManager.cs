using Dalamud.Bindings.ImGui;
using System.Collections.Generic;
using System.Numerics;

namespace AbsoluteRP.Helpers
{
    public static class TutorialManager
    {
        public sealed class Step
        {
            public string Id = string.Empty;
            public string AnchorId = string.Empty;
            public string Title = string.Empty;
            public string Body = string.Empty;
            public string NextStepId = string.Empty;
            public string PrevStepId = string.Empty;
            public bool BlockInteraction = false;
            public System.Func<bool> AutoAdvanceWhen = null;
            public string AutoAdvanceToStepId = string.Empty;
            public System.Func<bool> RetreatWhen = null;
            public string RetreatToStepId = string.Empty;
            public bool HideNextUntilAdvance = false;
            public System.Action OnEnter = null;
            public System.Action OnFinish = null;
            public System.Action OnTick = null;
        }

        private static readonly Dictionary<string, Step> steps = new();
        private static readonly Dictionary<string, (Vector2 Min, Vector2 Max)> anchors = new();
        private static readonly Dictionary<string, int> anchorFrame = new();
        private static string lastDrawnStepId = string.Empty;

        public static bool Active { get; private set; }
        public static string CurrentStepId { get; private set; } = string.Empty;
        public static string CurrentFlow { get; private set; } = string.Empty;

        public static void RegisterFlow(string flowId, IEnumerable<Step> flowSteps, string startStepId)
        {
            foreach (var s in flowSteps)
            {
                steps[s.Id] = s;
            }
            if (Active && CurrentFlow == flowId) return;
            CurrentFlow = flowId;
            CurrentStepId = startStepId;
            Active = false;
        }

        public static void Start(string flowId, string startStepId)
        {
            CurrentFlow = flowId;
            CurrentStepId = startStepId;
            Active = true;
            FireOnEnter();
        }

        public static void Stop()
        {
            Active = false;
            CurrentStepId = string.Empty;
            lastDrawnStepId = string.Empty;
        }

        public static void Advance()
        {
            if (!Active) return;
            if (!steps.TryGetValue(CurrentStepId, out var s)) { Stop(); return; }
            if (string.IsNullOrEmpty(s.NextStepId)) { Stop(); return; }
            CurrentStepId = s.NextStepId;
            FireOnEnter();
        }

        public static void GoTo(string stepId)
        {
            if (!Active || string.IsNullOrEmpty(stepId)) return;
            CurrentStepId = stepId;
            FireOnEnter();
        }

        public static void GoBack()
        {
            if (!Active) return;
            if (!steps.TryGetValue(CurrentStepId, out var s)) return;
            if (string.IsNullOrEmpty(s.PrevStepId)) return;
            CurrentStepId = s.PrevStepId;
            FireOnEnter();
        }

        private static void FireOnEnter()
        {
            if (steps.TryGetValue(CurrentStepId, out var s))
            {
                try { s.OnEnter?.Invoke(); } catch { }
            }
        }

        public static bool IsAnchor(string anchorId)
            => Active && steps.TryGetValue(CurrentStepId, out var s) && s.AnchorId == anchorId;

        public static bool IsCurrent(string stepId)
            => Active && CurrentStepId == stepId;

        public static void Anchor(string anchorId)
        {
            if (!Active || string.IsNullOrEmpty(anchorId)) return;
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            anchors[anchorId] = (min, max);
            anchorFrame[anchorId] = ImGui.GetFrameCount();
        }

        public static void AnchorRect(string anchorId, Vector2 min, Vector2 max)
        {
            if (!Active || string.IsNullOrEmpty(anchorId)) return;
            anchors[anchorId] = (min, max);
            anchorFrame[anchorId] = ImGui.GetFrameCount();
        }

        public static void BeginFrame()
        {
        }

        public static void Draw()
        {
            if (!Active) return;
            if (!steps.TryGetValue(CurrentStepId, out var step)) { Stop(); return; }

            try { step.OnTick?.Invoke(); } catch { }

            if (step.AutoAdvanceWhen != null)
            {
                try
                {
                    if (step.AutoAdvanceWhen())
                    {
                        if (!string.IsNullOrEmpty(step.AutoAdvanceToStepId)) GoTo(step.AutoAdvanceToStepId);
                        else Advance();
                        return;
                    }
                }
                catch { }
            }

            if (step.RetreatWhen != null)
            {
                try
                {
                    if (step.RetreatWhen())
                    {
                        if (!string.IsNullOrEmpty(step.RetreatToStepId)) GoTo(step.RetreatToStepId);
                        else GoBack();
                        return;
                    }
                }
                catch { }
            }

            var dl = ImGui.GetForegroundDrawList();
            var io = ImGui.GetIO();
            var screen = io.DisplaySize;

            uint dim = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.45f));

            var rect = default((Vector2 Min, Vector2 Max));
            bool haveRect = false;
            if (!string.IsNullOrEmpty(step.AnchorId) && anchors.TryGetValue(step.AnchorId, out rect))
            {
                var lastFrame = anchorFrame.TryGetValue(step.AnchorId, out var f) ? f : 0;
                var ageFrames = ImGui.GetFrameCount() - lastFrame;
                haveRect = ageFrames <= 4;
            }

            Vector2 highlightMin = default, highlightMax = default;
            if (haveRect)
            {
                var pad = 4f;
                highlightMin = new Vector2(rect.Min.X - pad, rect.Min.Y - pad);
                highlightMax = new Vector2(rect.Max.X + pad, rect.Max.Y + pad);

                dl.AddRectFilled(new Vector2(0, 0), new Vector2(screen.X, highlightMin.Y), dim);
                dl.AddRectFilled(new Vector2(0, highlightMax.Y), new Vector2(screen.X, screen.Y), dim);
                dl.AddRectFilled(new Vector2(0, highlightMin.Y), new Vector2(highlightMin.X, highlightMax.Y), dim);
                dl.AddRectFilled(new Vector2(highlightMax.X, highlightMin.Y), new Vector2(screen.X, highlightMax.Y), dim);

                var glow = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.25f, 0.95f));
                dl.AddRect(highlightMin, highlightMax, glow, 6f, ImDrawFlags.None, 3f);
                var glowSoft = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.25f, 0.30f));
                dl.AddRect(
                    new Vector2(highlightMin.X - 4, highlightMin.Y - 4),
                    new Vector2(highlightMax.X + 4, highlightMax.Y + 4),
                    glowSoft, 8f, ImDrawFlags.None, 6f);
            }
            else
            {
                dl.AddRectFilled(new Vector2(0, 0), new Vector2(screen.X, screen.Y), dim);
            }

            DrawTooltipCard(step, haveRect ? (highlightMin, highlightMax) : ((Vector2, Vector2)?)null, screen);
        }

        private static void DrawTooltipCard(Step step, (Vector2 Min, Vector2 Max)? near, Vector2 screen)
        {
            var cardW = 380f;
            var lineH = ImGui.GetTextLineHeight();
            var fontSize = ImGui.GetFontSize();
            float padX = 12f, padY = 10f;
            float buttonH = lineH + 8f;
            float separatorPad = 4f;

            var bodySize = ImGui.CalcTextSize(step.Body ?? string.Empty, false, cardW - padX * 2f);
            var bodyH = bodySize.Y;
            var titleH = string.IsNullOrEmpty(step.Title) ? 0f : lineH + separatorPad;
            var separatorAfterTitleH = string.IsNullOrEmpty(step.Title) ? 0f : separatorPad * 2f + 1f;
            var footerSeparatorH = separatorPad * 2f + 1f;
            var cardH = padY * 2f + titleH + separatorAfterTitleH + bodyH + footerSeparatorH + buttonH + 4f;

            Vector2 pos;
            if (near.HasValue)
            {
                var (min, max) = near.Value;
                var rightSpace = screen.X - max.X;
                var leftSpace = min.X;
                var bottomSpace = screen.Y - max.Y;
                var topSpace = min.Y;
                if (rightSpace >= cardW + 16)
                    pos = new Vector2(max.X + 16, min.Y);
                else if (leftSpace >= cardW + 16)
                    pos = new Vector2(min.X - cardW - 16, min.Y);
                else if (bottomSpace >= cardH + 16)
                    pos = new Vector2((screen.X - cardW) * 0.5f, max.Y + 16);
                else if (topSpace >= cardH + 16)
                    pos = new Vector2((screen.X - cardW) * 0.5f, min.Y - cardH - 16);
                else
                    pos = new Vector2((screen.X - cardW) * 0.5f, (screen.Y - cardH) * 0.5f);
            }
            else
            {
                pos = new Vector2((screen.X - cardW) * 0.5f, (screen.Y - cardH) * 0.5f);
            }
            pos.X = System.Math.Max(8, System.Math.Min(pos.X, screen.X - cardW - 8));
            pos.Y = System.Math.Max(8, System.Math.Min(pos.Y, screen.Y - cardH - 8));

            lastDrawnStepId = step.Id;

            var dl = ImGui.GetForegroundDrawList();
            var cardMin = pos;
            var cardMax = new Vector2(pos.X + cardW, pos.Y + cardH);

            uint shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.40f));
            dl.AddRectFilled(new Vector2(cardMin.X + 4, cardMin.Y + 6), new Vector2(cardMax.X + 4, cardMax.Y + 6), shadow, 10f);

            uint bg = ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.10f, 0.13f, 0.98f));
            dl.AddRectFilled(cardMin, cardMax, bg, 10f);

            uint borderSoft = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.25f, 0.30f));
            dl.AddRect(new Vector2(cardMin.X - 2, cardMin.Y - 2), new Vector2(cardMax.X + 2, cardMax.Y + 2), borderSoft, 12f, ImDrawFlags.None, 3f);

            uint border = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.25f, 0.95f));
            dl.AddRect(cardMin, cardMax, border, 10f, ImDrawFlags.None, 2f);

            uint sepColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.25f, 0.35f));
            uint titleColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.25f, 1f));
            uint bodyColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.92f, 0.92f, 0.94f, 1f));

            float y = cardMin.Y + padY;
            if (!string.IsNullOrEmpty(step.Title))
            {
                dl.AddText(new Vector2(cardMin.X + padX, y), titleColor, step.Title);
                y += lineH + separatorPad;
                dl.AddLine(new Vector2(cardMin.X + padX, y), new Vector2(cardMax.X - padX, y), sepColor);
                y += separatorPad + 1f;
            }

            dl.AddText(ImGui.GetFont(), fontSize, new Vector2(cardMin.X + padX, y), bodyColor, step.Body ?? string.Empty, cardW - padX * 2f);
            y += bodyH + separatorPad;
            dl.AddLine(new Vector2(cardMin.X + padX, y), new Vector2(cardMax.X - padX, y), sepColor);
            y += separatorPad + 1f;

            float btnW = 90f;
            float btnGap = 6f;
            float buttonY = y;
            float bx = cardMin.X + padX;

            if (!string.IsNullOrEmpty(step.PrevStepId))
            {
                if (DrawForegroundButton(dl, "Back", new Vector2(bx, buttonY), new Vector2(btnW, buttonH), false))
                    GoBack();
                bx += btnW + btnGap;
            }

            bool isFinish = string.IsNullOrEmpty(step.NextStepId);
            bool showNext = !(step.HideNextUntilAdvance && step.AutoAdvanceWhen != null);
            if (showNext)
            {
                if (DrawForegroundButton(dl, isFinish ? "Finish" : "Next", new Vector2(bx, buttonY), new Vector2(btnW, buttonH), false))
                {
                    if (isFinish)
                    {
                        try { step.OnFinish?.Invoke(); } catch { }
                        Stop();
                    }
                    else
                    {
                        Advance();
                    }
                }
            }

            float closeW = btnW + 30f;
            float closeX = cardMax.X - padX - closeW;
            if (DrawForegroundButton(dl, "Close", new Vector2(closeX, buttonY), new Vector2(closeW, buttonH), true))
                Stop();
        }

        private static bool DrawForegroundButton(ImDrawListPtr dl, string label, Vector2 pos, Vector2 size, bool danger)
        {
            var min = pos;
            var max = new Vector2(pos.X + size.X, pos.Y + size.Y);
            var io = ImGui.GetIO();
            var mouse = io.MousePos;
            bool hovered = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
            bool clicked = hovered && io.MouseClicked[0];
            if (hovered) io.WantCaptureMouse = true;

            Vector4 idle, hover, edge;
            if (danger)
            {
                idle = new Vector4(0.55f, 0.18f, 0.18f, 1f);
                hover = new Vector4(0.78f, 0.24f, 0.24f, 1f);
                edge = new Vector4(1f, 0.45f, 0.45f, 0.65f);
            }
            else
            {
                idle = new Vector4(0.18f, 0.20f, 0.26f, 1f);
                hover = new Vector4(0.30f, 0.34f, 0.44f, 1f);
                edge = new Vector4(1f, 0.85f, 0.25f, 0.60f);
            }

            var fill = hovered ? hover : idle;
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(fill), 5f);
            dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(edge), 5f, ImDrawFlags.None, 1f);

            var textSize = ImGui.CalcTextSize(label);
            var textPos = new Vector2(min.X + (size.X - textSize.X) * 0.5f, min.Y + (size.Y - textSize.Y) * 0.5f);
            dl.AddText(textPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), label);

            return clicked;
        }
    }
}

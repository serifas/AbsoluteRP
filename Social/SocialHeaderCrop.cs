using System;
using System.Numerics;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace AbsoluteRP.Social;

// Aspect-preserving cover-fit math for header banners + a modal crop editor that opens after upload. Users drag the image inside a fixed-aspect frame to pick the visible portion, then confirm to save.
public static class SocialHeaderCrop
{
    // Computes the uv-rectangle (uv0..uv1) needed to render a portion of `imgW x imgH` into a frame of `frameW x frameH` at (offsetX, offsetY, zoom) without stretching. offset is in normalized image space (top-left of visible region), zoom shrinks the visible rect (1 = fit, higher = tighter crop). Handles the edge case where the image is wider or taller than the frame - the visible fraction on each axis is picked so the drawn pixels always have the source's aspect.
    public static (Vector2 uv0, Vector2 uv1) ComputeUv(
        int imgW, int imgH, float frameW, float frameH,
        float offsetX, float offsetY, float zoom)
    {
        if (imgW <= 0 || imgH <= 0 || frameW <= 0 || frameH <= 0)
            return (new Vector2(0f, 0f), new Vector2(1f, 1f));
        var z = Math.Clamp(zoom, 1f, 5f);
        var imgAspect   = imgW / (float)imgH;
        var frameAspect = frameW / frameH;
        float uvW, uvH;
        if (imgAspect > frameAspect)
        {
            uvH = 1f / z;
            uvW = uvH * frameAspect / imgAspect;
        }
        else
        {
            uvW = 1f / z;
            uvH = uvW * imgAspect / frameAspect;
        }
        var ox = Math.Clamp(offsetX, 0f, MathF.Max(0f, 1f - uvW));
        var oy = Math.Clamp(offsetY, 0f, MathF.Max(0f, 1f - uvH));
        return (new Vector2(ox, oy), new Vector2(ox + uvW, oy + uvH));
    }

    // Called by StartHeaderUpload after the upload lands, with the URL of the freshly uploaded image and a callback that receives the framing the user picked (offsetX, offsetY, zoom).
    public static void Open(string url, Action<float, float, float> onConfirm)
    {
        _url        = url;
        _onConfirm  = onConfirm;
        _offX       = 0f;
        _offY       = 0f;
        _zoom       = 1f;
        _open       = true;
    }

    // Draw the modal - call once per frame from a top-level UI loop.
    public static void Draw()
    {
        if (!_open) return;
        ImGui.OpenPopup("##arp_hdr_crop");
        var view = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(view.WorkPos + view.WorkSize * 0.5f,
                                ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(RsTheme.S(680f), RsTheme.S(380f)),
                                 ImGuiCond.Appearing);

        if (ImGui.BeginPopupModal("##arp_hdr_crop", ref _open, ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.TextUnformatted("Frame your header");
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("Drag inside the frame to move the image, scroll wheel to zoom. Confirm when you're happy.");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            var availW  = ImGui.GetContentRegionAvail().X;
            var frameH  = availW * 0.28f;
            var tex     = string.IsNullOrEmpty(_url)
                ? null
                : SocialMediaCache.Get(_url);

            DrawInteractiveFrame(availW, frameH, tex, ref _offX, ref _offY, ref _zoom);

            ImGui.Spacing();
            var confirmSz = new Vector2(RsTheme.S(120f), 0f);
            if (RsElements.Button("Confirm##hdr_crop_ok", RsElements.ButtonVariant.Primary, confirmSz))
            {
                _onConfirm?.Invoke(_offX, _offY, _zoom);
                _open = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (RsElements.Button("Reset##hdr_crop_reset", RsElements.ButtonVariant.Ghost))
            {
                _offX = 0f; _offY = 0f; _zoom = 1f;
            }
            ImGui.SameLine();
            if (RsElements.Button("Cancel##hdr_crop_cancel", RsElements.ButtonVariant.Ghost, confirmSz))
            {
                _open = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    // Reusable interactive crop frame: draws the image with the current framing, handles drag + scroll wheel, and mutates the offset/zoom refs. Suitable for both the crop popup and any inline re-crop preview elsewhere.
    public static void DrawInteractiveFrame(
        float frameW, float frameH, IDalamudTextureWrap? tex,
        ref float offX, ref float offY, ref float zoom)
    {
        var dl   = ImGui.GetWindowDrawList();
        var hMin = ImGui.GetCursorScreenPos();
        var hMax = new Vector2(hMin.X + frameW, hMin.Y + frameH);

        dl.AddRectFilled(hMin, hMax,
            ImGui.ColorConvertFloat4ToU32(RsTheme.BgSecondary), RsTheme.S(6f));

        if (tex != null && tex.Width > 0)
        {
            var (uv0, uv1) = ComputeUv(tex.Width, tex.Height,
                                       frameW, frameH, offX, offY, zoom);
            dl.AddImageRounded(tex.Handle, hMin, hMax,
                uv0, uv1,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
                RsTheme.S(6f));

            // Rule-of-thirds grid.
            var gridCol = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.15f));
            for (int gi = 1; gi < 3; gi++)
            {
                var t = gi / 3f;
                dl.AddLine(new Vector2(hMin.X + frameW * t, hMin.Y),
                           new Vector2(hMin.X + frameW * t, hMax.Y), gridCol, 1f);
                dl.AddLine(new Vector2(hMin.X, hMin.Y + frameH * t),
                           new Vector2(hMax.X, hMin.Y + frameH * t), gridCol, 1f);
            }
        }
        dl.AddRect(hMin, hMax,
            ImGui.ColorConvertFloat4ToU32(RsTheme.Border), RsTheme.S(6f));

        ImGui.SetCursorScreenPos(hMin);
        ImGui.InvisibleButton("##hdr_crop_hit", new Vector2(frameW, frameH));

        if (ImGui.IsItemActive() && tex != null && tex.Width > 0)
        {
            var delta = ImGui.GetIO().MouseDelta;
            if (delta.LengthSquared() > 0.0001f)
            {
                var (uv0, uv1) = ComputeUv(tex.Width, tex.Height,
                                           frameW, frameH, offX, offY, zoom);
                var uvW = uv1.X - uv0.X;
                var uvH = uv1.Y - uv0.Y;
                offX = Math.Clamp(offX - delta.X * (uvW / frameW), 0f, MathF.Max(0f, 1f - uvW));
                offY = Math.Clamp(offY - delta.Y * (uvH / frameH), 0f, MathF.Max(0f, 1f - uvH));
            }
        }
        if (ImGui.IsItemHovered() && tex != null && tex.Width > 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var wheel = ImGui.GetIO().MouseWheel;
            if (MathF.Abs(wheel) > 0.001f)
            {
                zoom = Math.Clamp(zoom + wheel * 0.15f, 1f, 5f);
                var (uv0, uv1) = ComputeUv(tex.Width, tex.Height,
                                           frameW, frameH, offX, offY, zoom);
                offX = Math.Clamp(offX, 0f, MathF.Max(0f, 1f - (uv1.X - uv0.X)));
                offY = Math.Clamp(offY, 0f, MathF.Max(0f, 1f - (uv1.Y - uv0.Y)));
            }
        }
    }

    // Popup state.
    private static string?                  _url;
    private static Action<float, float, float>? _onConfirm;
    private static float _offX, _offY, _zoom = 1f;
    private static bool  _open;
}

using System;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.D3D11_BIND_FLAG;
using static TerraFX.Interop.DirectX.D3D11_CPU_ACCESS_FLAG;
using static TerraFX.Interop.DirectX.D3D11_USAGE;
using static TerraFX.Interop.DirectX.DXGI_FORMAT;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace AbsoluteRP.Video;

public sealed unsafe class MmfFrameReader : IDisposable
{
    // MMF read state
    private readonly string mmfName;
    private MemoryMappedFile? mmf;
    private MemoryMappedViewAccessor? view;
    private byte* mmfBase;

    // Local D3D11 state
    private ID3D11Texture2D* gpuTex;
    private ID3D11Texture2D* stagingTex;
    private ID3D11ShaderResourceView* srv;
    private int width;
    private int height;
    private long lastFrameCounter = -1;

    private bool disposed;
    // Captured failure detail for debugging when Failed flips
    public string? FailureReason { get; private set; }

    // Pending-frame state, written by Update()
    private readonly object pendingLock = new();
    private int pendingWidth;
    private int pendingHeight;
    private long pendingFrameCounter = -1;

    public int Width => width;
    public int Height => height;
    // Ready server exists
    public bool Ready => !Failed && (srv != null || HasPendingFrame);

    // Raw SRV pointer for callers that want to draw the current frame via ImDrawList.AddImage (e.g. as a backdrop behind the UI). Returns IntPtr.Zero when nothing has been decoded yet. Callers must apply any pending frame first - use ApplyLatestFrame().
    public IntPtr TextureId => (IntPtr)srv;

    // Apply any pending frame right now (framework-thread only). Callers that want to read TextureId THIS frame should call this first so they get the freshest SRV. Returns true if an SRV exists.
    public bool ApplyLatestFrame() => ApplyPendingFrame();
    private bool HasPendingFrame
    {
        get { lock (pendingLock) return pendingFrameCounter >= 0; }
    }

    // Set on any failure path.
    public bool Failed { get; private set; }
    public void MarkFailed() => Failed = true;

    private void Fail(string reason)
    {
        Failed = true;
        if (FailureReason == null) FailureReason = reason;
    }

    public MmfFrameReader(string mmfName, long mmfSize)
    {
        this.mmfName = mmfName;
        if (!DxHandler.Initialized)
        {
            Fail("DxHandler not initialized");
            return;
        }
        try
        {
            mmf = MemoryMappedFile.OpenExisting(mmfName, MemoryMappedFileRights.Read);
            view = mmf.CreateViewAccessor(0, mmfSize, MemoryMappedFileAccess.Read);
            byte* p = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
            mmfBase = p;
        }
        catch (Exception ex)
        {
            Fail("MMF open failed: " + ex.GetType().Name + ": " + ex.Message);
            CloseMmf();
        }
    }

    // Called by VideoPlayerSession on each FrameReady RPC
    public void Update(int newWidth, int newHeight, long frameCounter)
    {
        if (disposed || Failed) return;
        if (newWidth <= 0 || newHeight <= 0) return;
        lock (pendingLock)
        {
            pendingWidth = newWidth;
            pendingHeight = newHeight;
            pendingFrameCounter = frameCounter;
        }
    }

    // ApplyPendingFrame on framework thead
    private bool ApplyPendingFrame()
    {
        // Age any retired textures. We drive this from the per-frame apply hook so the countdown ticks on real Present cycles, not on arbitrary reader lifetimes.
        DrainGraves();
        if (disposed || Failed || mmfBase == null) return srv != null;

        int newWidth, newHeight;
        long frameCounter;
        lock (pendingLock)
        {
            newWidth = pendingWidth;
            newHeight = pendingHeight;
            frameCounter = pendingFrameCounter;
        }
        if (newWidth <= 0 || newHeight <= 0) return srv != null;
        // Frame-counter dedupe
        if (frameCounter == lastFrameCounter) return srv != null;

        try
        {
            EnsureTextures(newWidth, newHeight);
            if (Failed || gpuTex == null || stagingTex == null) return false;

            ID3D11DeviceContext* ctx = null;
            DxHandler.Device->GetImmediateContext(&ctx);
            if (ctx == null) { Fail("GetImmediateContext returned null"); return false; }
            try
            {
                D3D11_MAPPED_SUBRESOURCE mapped;
                // texture staging
                HRESULT hr = ctx->Map((ID3D11Resource*)stagingTex, 0,
                    D3D11_MAP.D3D11_MAP_WRITE, 0, &mapped);
                if (hr.FAILED) { Fail($"ctx->Map staging failed hr=0x{hr.Value:X}"); return false; }
                try
                {
                    var rowBytes = newWidth * 4;
                    var src = mmfBase;
                    var dst = (byte*)mapped.pData;
                    if (mapped.RowPitch == (uint)rowBytes)
                    {
                        Buffer.MemoryCopy(src, dst,
                            (long)mapped.RowPitch * newHeight,
                            (long)rowBytes * newHeight);
                    }
                    else
                    {
                        for (int y = 0; y < newHeight; y++)
                        {
                            Buffer.MemoryCopy(src + y * rowBytes,
                                dst + y * mapped.RowPitch,
                                mapped.RowPitch,
                                rowBytes);
                        }
                    }
                }
                finally
                {
                    ctx->Unmap((ID3D11Resource*)stagingTex, 0);
                }

                ctx->CopyResource((ID3D11Resource*)gpuTex, (ID3D11Resource*)stagingTex);
            }
            finally
            {
                ctx->Release();
            }
            lastFrameCounter = frameCounter;
            return srv != null;
        }
        catch (Exception ex)
        {
            Fail("ApplyPendingFrame threw " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    // Old-generation resources kept alive for N frames after they've been replaced by a resolution change. Releasing them the same frame we hand out the new SRV races with any DrawList.AddImage that still holds the previous pointer - the NVIDIA driver then dereferences a freed resource on its own worker thread and crashes. Each entry burns down one frame per ApplyPendingFrame; at 0 we release.
    private const int GraveHoldFrames = 3;
    private readonly System.Collections.Generic.List<Grave> graves = new();
    private struct Grave
    {
        public ID3D11Texture2D* Gpu;
        public ID3D11Texture2D* Staging;
        public ID3D11ShaderResourceView* Srv;
        public int Countdown;
    }

    private void DrainGraves()
    {
        if (graves.Count == 0) return;
        for (int i = graves.Count - 1; i >= 0; i--)
        {
            var g = graves[i];
            g.Countdown--;
            if (g.Countdown <= 0)
            {
                if (g.Srv     != null) g.Srv->Release();
                if (g.Staging != null) g.Staging->Release();
                if (g.Gpu     != null) g.Gpu->Release();
                graves.RemoveAt(i);
            }
            else
            {
                graves[i] = g;
            }
        }
    }

    private void EnsureTextures(int w, int h)
    {
        if (gpuTex != null && srv != null && w == width && h == height) return;
        // Retire (don't release) any existing resources - a Draw pass this same frame may still be holding pointers to them via ImGui's draw-list. GraveHoldFrames drives will let the GPU consume those draws before we free the underlying textures.
        if (gpuTex != null || stagingTex != null || srv != null)
        {
            graves.Add(new Grave
            {
                Gpu       = gpuTex,
                Staging   = stagingTex,
                Srv       = srv,
                Countdown = GraveHoldFrames,
            });
            gpuTex     = null;
            stagingTex = null;
            srv        = null;
        }
        width = w;
        height = h;

        var gpuDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
            Usage = D3D11_USAGE_DEFAULT,
            BindFlags = (uint)D3D11_BIND_SHADER_RESOURCE,
        };
        ID3D11Texture2D* gt = null;
        HRESULT hr = DxHandler.Device->CreateTexture2D(&gpuDesc, null, &gt);
        if (hr.FAILED || gt == null) { Fail($"CreateTexture2D(gpu) failed hr=0x{hr.Value:X}"); return; }
        gpuTex = gt;

        var stagingDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
            Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
            BindFlags = 0,
            CPUAccessFlags = (uint)D3D11_CPU_ACCESS_WRITE,
        };
        ID3D11Texture2D* st = null;
        hr = DxHandler.Device->CreateTexture2D(&stagingDesc, null, &st);
        if (hr.FAILED || st == null) { Fail($"CreateTexture2D(staging) failed hr=0x{hr.Value:X}"); return; }
        stagingTex = st;

        var srvDesc = new D3D11_SHADER_RESOURCE_VIEW_DESC
        {
            Format = DXGI_FORMAT_B8G8R8A8_UNORM,
            ViewDimension = D3D_SRV_DIMENSION.D3D_SRV_DIMENSION_TEXTURE2D,
        };
        srvDesc.Anonymous.Texture2D.MostDetailedMip = 0;
        srvDesc.Anonymous.Texture2D.MipLevels = 1;
        ID3D11ShaderResourceView* s = null;
        hr = DxHandler.Device->CreateShaderResourceView((ID3D11Resource*)gpuTex, &srvDesc, &s);
        if (hr.FAILED || s == null) { Fail($"CreateShaderResourceView failed hr=0x{hr.Value:X}"); return; }
        srv = s;
    }

    public void Render(Vector2 size)
    {
        // Apply any pending frame
        ApplyPendingFrame();

        if (Failed)
        {
            ImGui.Dummy(size);
            return;
        }
        // Grab SRV pointer locally
        var s = srv;
        if (s == null) { ImGui.Dummy(size); return; }
        try
        {
            var texId = new ImTextureID((nint)s);
            ImGui.Image(texId, size);
        }
        catch
        {
            Failed = true;
            ImGui.Dummy(size);
        }
    }

    private void ReleaseTextures()
    {
        if (srv != null) { srv->Release(); srv = null; }
        if (stagingTex != null) { stagingTex->Release(); stagingTex = null; }
        if (gpuTex != null) { gpuTex->Release(); gpuTex = null; }
    }

    private void CloseMmf()
    {
        if (view != null && mmfBase != null)
        {
            try { view.SafeMemoryMappedViewHandle.ReleasePointer(); } catch { }
            mmfBase = null;
        }
        try { view?.Dispose(); } catch { }
        try { mmf?.Dispose(); } catch { }
        view = null;
        mmf = null;
    }

    // Freeze mode: close the MMF but keep the D3D11 GPU/staging textures + SRV alive. Called after the plugin has copied the first frame into the SRV - we then don't need the shared-memory pipeline anymore, and the SRV serves as a persistent poster image on the game's D3D device. After this, Update() is a no-op (mmfBase is null -> ApplyPendingFrame early-returns with the existing srv still valid).
    public void CloseMmfKeepTextures() => CloseMmf();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        // Flush graves - no further Present cycles will consume them, so we release now. VideoPlayerSession.Dispose is itself expected to be run via a deferred queue by callers who care about the last-frame draw hazard (see ProfilesPage.RetireBackgroundVideo).
        for (int i = 0; i < graves.Count; i++)
        {
            var g = graves[i];
            if (g.Srv     != null) g.Srv->Release();
            if (g.Staging != null) g.Staging->Release();
            if (g.Gpu     != null) g.Gpu->Release();
        }
        graves.Clear();
        ReleaseTextures();
        CloseMmf();
    }
}

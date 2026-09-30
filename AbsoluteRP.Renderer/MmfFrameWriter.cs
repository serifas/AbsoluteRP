using System;
using System.IO.MemoryMappedFiles;

namespace AbsoluteRP.Video.Renderer;

internal sealed unsafe class MmfFrameWriter : IDisposable
{
    public const int MaxWidth = 1920;
    public const int MaxHeight = 1080;
    public const long MmfCapacity = (long)MaxWidth * MaxHeight * 4L;

    private readonly string mmfName;
    private MemoryMappedFile? mmf;
    private MemoryMappedViewAccessor? view;
    private byte* basePtr;
    private long frameCounter;
    private int currentWidth;
    private int currentHeight;
    private bool disposed;

    public string MmfName => mmfName;
    public long Capacity => MmfCapacity;
    public int Width => currentWidth;
    public int Height => currentHeight;
    public long FrameCounter => frameCounter;

    public MmfFrameWriter()
    {
        mmfName = "AbsoluteRP.Frame." + Guid.NewGuid().ToString("N");
    }

    public void Initialize()
    {
        mmf = MemoryMappedFile.CreateNew(mmfName, MmfCapacity, MemoryMappedFileAccess.ReadWrite);
        view = mmf.CreateViewAccessor(0, MmfCapacity, MemoryMappedFileAccess.ReadWrite);
        byte* p = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
        basePtr = p;
    }

    public bool Write(IntPtr cefBuffer, int width, int height)
    {
        if (disposed || basePtr == null) return false;
        if (width <= 0 || height <= 0) return false;
        if (width > MaxWidth || height > MaxHeight) return false;

        var bytes = (long)width * height * 4L;
        if (bytes > MmfCapacity) return false;

        Buffer.MemoryCopy((void*)cefBuffer, basePtr, MmfCapacity, bytes);
        currentWidth = width;
        currentHeight = height;
        frameCounter++;
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (view != null && basePtr != null)
        {
            try { view.SafeMemoryMappedViewHandle.ReleasePointer(); } catch { }
            basePtr = null;
        }
        try { view?.Dispose(); } catch { }
        try { mmf?.Dispose(); } catch { }
        view = null;
        mmf = null;
    }
}

using System;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace AbsoluteRP.Video;

public static unsafe class DxHandler
{
    public static IntPtr DeviceHandle { get; private set; }
    public static uint AdapterLuidLow { get; private set; }
    public static int AdapterLuidHigh { get; private set; }
    public static bool Initialized { get; private set; }

    // Internal pointer we use to OpenSharedResource for the texture
    internal static ID3D11Device* Device;

    public static void Initialize(IDalamudPluginInterface pi)
    {
        if (Initialized) return;

        DeviceHandle = pi.UiBuilder.DeviceHandle;
        if (DeviceHandle == IntPtr.Zero)
            throw new InvalidOperationException("UiBuilder.DeviceHandle was null");

        Device = (ID3D11Device*)DeviceHandle;

        // ID3D11Device -> IDXGIDevice -> IDXGIAdapter -> AdapterLuid.
        IDXGIDevice* dxgiDevice = null;
        IDXGIAdapter* adapter = null;
        try
        {
            Guid dxgiIid = typeof(IDXGIDevice).GUID;
            HRESULT hr = Device->QueryInterface(&dxgiIid, (void**)&dxgiDevice);
            Marshal.ThrowExceptionForHR(hr);
            hr = dxgiDevice->GetAdapter(&adapter);
            Marshal.ThrowExceptionForHR(hr);

            DXGI_ADAPTER_DESC desc;
            hr = adapter->GetDesc(&desc);
            Marshal.ThrowExceptionForHR(hr);

            AdapterLuidLow = desc.AdapterLuid.LowPart;
            AdapterLuidHigh = desc.AdapterLuid.HighPart;
        }
        finally
        {
            if (adapter != null) adapter->Release();
            if (dxgiDevice != null) dxgiDevice->Release();
        }

        Initialized = true;
    }
}

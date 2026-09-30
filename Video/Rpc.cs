using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AbsoluteRP.Video.Common;

public static class RpcOp
{
    public const string Hello = "hello";
    public const string FrameReady = "frame_ready";
    public const string MouseMove = "mouse_move";
    public const string MouseButton = "mouse_button";
    public const string MouseWheel = "mouse_wheel";
    public const string Resize = "resize";
    public const string Navigate = "navigate";
    public const string Shutdown = "shutdown";
    public const string Log = "log";
    // set audio volume for vlc
    public const string SetVolume = "set_volume";
    // Playback transport controls.
    public const string SetPaused  = "set_paused";
    public const string Seek       = "seek";
    // Renderer -> plugin push of current position + total length.
    public const string TimeUpdate = "time_update";
}


public sealed class RpcMessage
{
    [JsonProperty("op")]   public string Op { get; set; } = string.Empty;
    [JsonProperty("json")] public string Json { get; set; } = string.Empty;
}

// Renderer -> plugin: announce that we have come online.
public sealed class HelloRequest
{
    [JsonProperty("pid")] public int Pid { get; set; }
}

// Renderer new BGRA framer
public sealed class FrameReady
{
    [JsonProperty("mmf")]    public string MmfName { get; set; } = string.Empty;
    [JsonProperty("size")]   public long MmfSize { get; set; }
    [JsonProperty("width")]  public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("frame")]  public long FrameCounter { get; set; }
}
// don't think this is in use really but going to keep for now pointer movement in renderer-local pixel coords.
public sealed class MouseMove
{
    [JsonProperty("x")] public int X { get; set; }
    [JsonProperty("y")] public int Y { get; set; }
}

// Button: 0 left, 1 middle, 2 right.
public sealed class MouseButton
{
    [JsonProperty("x")]      public int X { get; set; }
    [JsonProperty("y")]      public int Y { get; set; }
    [JsonProperty("button")] public int Button { get; set; }
    [JsonProperty("down")]   public bool Down { get; set; }
}

// scroll wheel delta in pixels.
public sealed class MouseWheel
{
    [JsonProperty("x")]      public int X { get; set; }
    [JsonProperty("y")]      public int Y { get; set; }
    [JsonProperty("deltaY")] public int DeltaY { get; set; }
}

// Plugin -> renderer: notify the browser the surface size has changed.
public sealed class Resize
{
    [JsonProperty("width")]  public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
}

// Plugin -> renderer: change the URL in the loaded browser.
public sealed class Navigate
{
    [JsonProperty("url")] public string Url { get; set; } = string.Empty;
}

// Plugin -> renderer: clean shutdown. The renderer should exit shortly after.
public sealed class Shutdown { }

// Plugin -> renderer: set output volume on the libVLC media player. Volume is 0..100; libVLC accepts >100 for amplification but we cap at 100 in the UI so the slider stays predictable.
public sealed class SetVolume
{
    [JsonProperty("volume")] public int Volume { get; set; }
}

// Plugin -> renderer: pause / resume playback.
public sealed class SetPaused
{
    [JsonProperty("paused")] public bool Paused { get; set; }
}

// Plugin -> renderer: jump to a specific position in the media.
public sealed class Seek
{
    [JsonProperty("ms")] public long Ms { get; set; }
}

// Renderer -> plugin: periodic push of position + duration in ms and the current paused flag so the UI can drive its seek bar / play glyph without polling.
public sealed class TimeUpdate
{
    [JsonProperty("position")] public long PositionMs { get; set; }
    [JsonProperty("duration")] public long DurationMs { get; set; }
    [JsonProperty("paused")]   public bool Paused     { get; set; }
}

// Renderer -> plugin: free-form log line we surface in the Dalamud log.
public sealed class LogLine
{
    [JsonProperty("level")] public string Level { get; set; } = "info";
    [JsonProperty("text")]  public string Text { get; set; } = string.Empty;
}

// Length-prefixed JSON over a duplex pipe. 4-byte big-endian length, then. UTF-8 JSON bytes. The same helper is used on both sides of the pipe.
public static class RpcWire
{
    public static async Task WriteAsync(Stream s, RpcMessage msg, CancellationToken ct = default)
    {
        var json = JsonConvert.SerializeObject(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        var len = bytes.Length;
        var header = new byte[4];
        // Big-endian length so debugging with hex tools is sane.
        header[0] = (byte)((len >> 24) & 0xFF);
        header[1] = (byte)((len >> 16) & 0xFF);
        header[2] = (byte)((len >> 8) & 0xFF);
        header[3] = (byte)(len & 0xFF);
        await s.WriteAsync(header, 0, 4, ct).ConfigureAwait(false);
        await s.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
        await s.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<RpcMessage?> ReadAsync(Stream s, CancellationToken ct = default)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(s, header, 0, 4, ct).ConfigureAwait(false))
            return null;
        var len = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
        if (len < 0 || len > 32 * 1024 * 1024)
            return null;
        var buf = new byte[len];
        if (!await ReadExactAsync(s, buf, 0, len, ct).ConfigureAwait(false))
            return null;
        var json = Encoding.UTF8.GetString(buf);
        return JsonConvert.DeserializeObject<RpcMessage>(json);
    }

    private static async Task<bool> ReadExactAsync(Stream s, byte[] buf, int offset, int count, CancellationToken ct)
    {
        var read = 0;
        while (read < count)
        {
            var n = await s.ReadAsync(buf, offset + read, count - read, ct).ConfigureAwait(false);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    // Convenience: build an envelope with a typed payload.
    public static RpcMessage Make<T>(string op, T payload)
    {
        return new RpcMessage
        {
            Op = op,
            Json = JsonConvert.SerializeObject(payload),
        };
    }

    public static T? Decode<T>(RpcMessage msg) where T : class
    {
        if (string.IsNullOrEmpty(msg.Json)) return null;
        return JsonConvert.DeserializeObject<T>(msg.Json);
    }
}

using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using AbsoluteRP.Video.Common;
using Dalamud.Plugin.Services;

namespace AbsoluteRP.Video;

public sealed class RpcHost : IDisposable
{
    private readonly IPluginLog log;
    private readonly string pipeName;
    private readonly NamedPipeServerStream server;
    private readonly CancellationTokenSource cts = new();

    private readonly SemaphoreSlim writeGate = new(1, 1);
    private Task? readTask;
    private bool disposed;
    private bool connected;

    public string PipeName => pipeName;
    public bool Connected => connected;

    public event Action<FrameReady>? FrameReady;
    public event Action<HelloRequest>? Hello;
    public event Action<LogLine>? Log;
    public event Action<TimeUpdate>? TimeUpdate;
    public event Action? Disconnected;

    public RpcHost(IPluginLog log)
    {
        this.log = log;
        pipeName = "AbsoluteRP." + Guid.NewGuid().ToString("N");
        server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    // Begin waiting for the renderer to connect.
    public void Start()
    {
        readTask = Task.Run(() => RunAsync(cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            log.Information("[AbsoluteRP] rpc waiting for renderer on pipe {P}", pipeName);
            await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
            connected = true;
            log.Information("[AbsoluteRP] rpc renderer connected on pipe {P}", pipeName);

            long messageCount = 0;
            while (!ct.IsCancellationRequested && server.IsConnected)
            {
                var msg = await RpcWire.ReadAsync(server, ct).ConfigureAwait(false);
                if (msg == null) break;
                messageCount++;
                if (messageCount == 1 || messageCount % 100 == 0)
                    log.Information("[AbsoluteRP] rpc msg #{N} op={Op}", messageCount, msg.Op);
                Dispatch(msg);
            }
            log.Information("[AbsoluteRP] rpc read loop exited (msgsReceived={N}, ctCancel={C}, srvConn={S})",
                messageCount, ct.IsCancellationRequested, server.IsConnected);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            log.Warning("[AbsoluteRP] rpc read loop error: {E}", ex.Message);
        }
        finally
        {
            connected = false;
            try { Disconnected?.Invoke(); } catch { }
        }
    }

    private void Dispatch(RpcMessage msg)
    {
        try
        {
            switch (msg.Op)
            {
                case RpcOp.Hello:
                {
                    var p = RpcWire.Decode<HelloRequest>(msg);
                    if (p != null) Hello?.Invoke(p);
                    break;
                }
                case RpcOp.FrameReady:
                {
                    var p = RpcWire.Decode<FrameReady>(msg);
                    if (p != null) FrameReady?.Invoke(p);
                    break;
                }
                case RpcOp.Log:
                {
                    var p = RpcWire.Decode<LogLine>(msg);
                    if (p != null) Log?.Invoke(p);
                    break;
                }
                case RpcOp.TimeUpdate:
                {
                    var p = RpcWire.Decode<TimeUpdate>(msg);
                    if (p != null) TimeUpdate?.Invoke(p);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            log.Debug("[AbsoluteRP] rpc dispatch error: {E}", ex.Message);
        }
    }

    public void Send<T>(string op, T payload)
    {
        if (!connected || disposed) return;
        var env = RpcWire.Make(op, payload);

        _ = SendSerializedAsync(env);
    }

    private async Task SendSerializedAsync(RpcMessage env)
    {
        try
        {
            await writeGate.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }
        catch (ObjectDisposedException) { return; }

        try
        {
            await RpcWire.WriteAsync(server, env, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Debug("[AbsoluteRP] rpc send failed: {E}", ex.Message);
        }
        finally
        {
            try { writeGate.Release(); } catch { }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { cts.Cancel(); } catch { }
        try { server.Dispose(); } catch { }
        try { readTask?.Wait(500); } catch { }
        try { cts.Dispose(); } catch { }
        try { writeGate.Dispose(); } catch { }
    }
}

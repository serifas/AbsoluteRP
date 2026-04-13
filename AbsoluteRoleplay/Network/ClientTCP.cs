using System;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AbsoluteRP;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AbsoluteRP.Windows.MainPanel;
using AbsoluteRP.Helpers;

namespace Networking
{
    // Handles the TCP connection to the AbsoluteRP server.
    // All communication goes through an SSL/TLS encrypted stream.
    // Uses a length-prefixed protocol: each packet is [4-byte length][payload].
    public class ClientTCP
    {
        // Only one write at a time to prevent interleaved packets on the wire
        private static readonly SemaphoreSlim writeSemaphore = new SemaphoreSlim(1, 1);
        public static bool Connected;
        public static TcpClient clientSocket;
        public static SslStream sslStream;
        private static byte[] recBuffer = new byte[8192]; // Read buffer for incoming data
        private static readonly string server = "join.absolute-roleplay.net";
        private static readonly int port = 53923;
        public static Plugin Plugin;

        // Callback for when data arrives from the server (async read pattern).
        // Copies the received bytes into a new array before processing to avoid
        // buffer reuse issues, then kicks off another read to keep listening.
        private static void OnReceiveData(IAsyncResult result)
        {
            try
            {
                if (!Connected)
                    return;

                SslStream localSslStream = (SslStream)result.AsyncState;

                int bytesRead = 0;
                try
                {
                    bytesRead = localSslStream.EndRead(result);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                // Zero bytes means the server closed the connection
                if (bytesRead <= 0)
                {
                    Plugin.PluginLog.Debug("No data received or connection closed by the server.");
                    Disconnect();
                    return;
                }

                // Copy buffer before passing to handler — the read buffer gets reused
                byte[] newBytes = new byte[bytesRead];
                Buffer.BlockCopy(recBuffer, 0, newBytes, 0, bytesRead);

                // Hand off to the packet splitter/dispatcher
                try
                {
                    ClientHandleData.HandleData(newBytes);
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Exception in HandleData: " + ex);
                }

                // Queue up the next read
                if (Connected && localSslStream != null && localSslStream.CanRead)
                {
                    localSslStream.BeginRead(recBuffer, 0, recBuffer.Length, OnReceiveData, localSslStream);
                }
            }
            catch (IOException ioEx)
            {
                Plugin.PluginLog.Debug("IO Debug during data reception: " + ioEx.Message);
                Disconnect();
            }
            catch (ObjectDisposedException ex)
            {
                Plugin.PluginLog.Debug("SslStream has been disposed: " + ex.Message);
                Disconnect();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug during data reception: {ex}");
                Disconnect();
            }
        }

        // Alternative async receive loop (not currently used — OnReceiveData handles it).
        // Reads in a loop until disconnected, processing each chunk as it arrives.
        private static async Task ReceiveDataAsync()
        {
            try
            {
                while (Connected && sslStream != null && sslStream.CanRead)
                {
                    int length = 0;
                    try
                    {
                        length = await sslStream.ReadAsync(recBuffer, 0, recBuffer.Length);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    if (length <= 0)
                    {
                        Plugin.PluginLog.Debug("Server closed the connection.");
                        Disconnect();
                        break;
                    }

                    Plugin.PluginLog.Debug($"Received {length} bytes from the server.");

                    // Same defensive copy as OnReceiveData
                    var newBytes = new byte[length];
                    Buffer.BlockCopy(recBuffer, 0, newBytes, 0, length);

                    try
                    {
                        ClientHandleData.HandleData(newBytes);
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug("Exception in HandleData: " + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug receiving data: " + ex);
                Disconnect();
            }
        }

        // Updates the UI status text and color based on the current connection state
        public static void UpdateConnectionStatus(TcpClient client)
        {
            if (client != null && client.Connected)
            {
                MainPanel.serverStatusColor = new Vector4(0, 255, 0, 255);
                MainPanel.serverStatus = "Connected";
            }
            else
            {
                MainPanel.serverStatusColor = new Vector4(255, 0, 0, 255);
                MainPanel.serverStatus = "Disconnected";
            }
        }

        // Returns the connection status as a color + label tuple.
        // Uses socket polling + peek to detect dead connections that
        // the OS still reports as "Connected".
        public static async Task<Tuple<Vector4, string>> GetConnectionStatusAsync(TcpClient _tcpClient)
        {
            Tuple<Vector4, string> connected = Tuple.Create(new Vector4(0, 255, 0, 255), "Connected");
            Tuple<Vector4, string> disconnected = Tuple.Create(new Vector4(255, 0, 0, 255), "Disconnected");
            try
            {
                if (_tcpClient != null && _tcpClient.Client != null && _tcpClient.Client.Connected)
                {
                    bool isSocketReadable = _tcpClient.Client.Poll(0, SelectMode.SelectRead);
                    bool isSocketWritable = _tcpClient.Client.Poll(0, SelectMode.SelectWrite);

                    if (isSocketReadable)
                    {
                        // Peek at incoming data without consuming it
                        byte[] buffer = new byte[1];
                        int bytesRead = await _tcpClient.Client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.Peek);
                        Plugin.PluginLog.Debug($"Bytes peeked: {bytesRead}");

                        // Zero bytes on a readable socket means the remote end closed
                        if (bytesRead == 0)
                        {
                            Plugin.PluginLog.Debug("No data received (0 bytes), connection likely closed.");
                            return disconnected;
                        }
                        return connected;
                    }

                    // Writable but not readable = normal idle connection
                    if (isSocketWritable && !isSocketReadable)
                    {
                        Plugin.PluginLog.Debug("Connected");
                        return connected;
                    }

                    Plugin.PluginLog.Debug("Socket is neither readable nor writable, returning Disconnected.");
                    return disconnected;
                }

                Plugin.PluginLog.Debug("TcpClient is null or not connected.");
                return disconnected;
            }
            catch (SocketException ex)
            {
                Plugin.PluginLog.Debug($"SocketException during connection check: {ex.Message}");
                return disconnected;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Exception during connection check: {ex.Message}");
                return disconnected;
            }
        }

        // Kicks off the async read loop using BeginRead (callback-based pattern)
        public static void StartReceiving()
        {
            if (sslStream != null && sslStream.CanRead)
            {
                sslStream.BeginRead(recBuffer, 0, recBuffer.Length, OnReceiveData, sslStream);
            }
            else
            {
                Plugin.PluginLog.Debug("SSL/TLS stream is not readable after handshake.");
            }
        }

        // Opens a TCP connection to the server and performs the TLS handshake.
        // On success, starts the receive loop so we can get packets from the server.
        public static async Task EstablishConnectionAsync()
        {
            try
            {
                clientSocket = new TcpClient();
                await clientSocket.ConnectAsync(server, port);

                // Wrap the raw TCP stream in SSL for encryption
                sslStream = new SslStream(clientSocket.GetStream(), false, ValidateServerCertificate);

                // TLS 1.2+ only
                sslStream.AuthenticateAsClient(server, null, SslProtocols.Tls12 | SslProtocols.Tls13, false);

                if (sslStream.IsAuthenticated && sslStream.CanRead && sslStream.CanWrite)
                {
                    Connected = true;
                    StartReceiving();
                }
                else
                {
                    Plugin.PluginLog.Debug("SSL/TLS stream is not authenticated or readable.");
                    Disconnect();
                    Plugin.CloseAllWindows();
                    Plugin.OpenMainPanel();
                }
            }
            catch (AuthenticationException authEx)
            {
                Plugin.PluginLog.Debug("SSL/TLS authentication failed: " + authEx.Message);
                if (authEx.InnerException != null)
                    Plugin.PluginLog.Debug("Inner exception: " + authEx.InnerException.Message);
                Disconnect();
            }
            catch (SocketException sockEx)
            {
                Plugin.PluginLog.Debug("Socket Debug during connection: " + sockEx.Message);
                Disconnect();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Connection Debug: " + ex);
                Disconnect();
            }
        }

        // SSL certificate validation callback — rejects invalid certs
        public static bool ValidateServerCertificate(
            object sender, X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("SSL certificate Debug: " + sslPolicyErrors);
            return false;
        }

        // Tears down the connection — closes both the SSL stream and the TCP socket.
        // Wrapped in try/catch because either might already be disposed.
        public static void Disconnect()
        {
            Connected = false;
            try
            {
                sslStream?.Close();
            }
            catch { }
            try
            {
                WindowOperations.SafeDispose(sslStream);
                sslStream = null;
            }
            catch { }
            sslStream = null;

            try
            {
                clientSocket?.Close();
            }
            catch { }
            try
            {
                WindowOperations.SafeDispose(clientSocket);
                clientSocket = null;
            }
            catch { }
            clientSocket = null;

            Plugin.PluginLog.Debug("Disconnected from server.");
        }

        // Synchronous wrapper around the async connection check (blocks the calling thread)
        public static bool IsConnected()
        {
            return Task.Run(async () => await IsConnectedToServerAsync(clientSocket)).GetAwaiter().GetResult();
        }

        // Checks if the TCP connection is still alive by polling + peeking.
        // A socket can report Connected=true even after the remote end closed,
        // so we peek to detect that case.
        public static async Task<bool> IsConnectedToServerAsync(TcpClient tcpClient)
        {
            try
            {
                if (tcpClient != null && tcpClient.Client != null && tcpClient.Client.Connected)
                {
                    if (tcpClient.Client.Poll(0, SelectMode.SelectRead))
                    {
                        byte[] buff = new byte[1];
                        if (await tcpClient.Client.ReceiveAsync(new ArraySegment<byte>(buff), SocketFlags.Peek) == 0)
                        {
                            return false; // Remote closed
                        }
                        return true;
                    }
                    return true; // Not readable but still connected = idle
                }
                return false;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug checking server connection: " + ex);
                return false;
            }
        }

        // Pings the server and reconnects if we're not already connected
        public static async void CheckStatus()
        {
            try
            {
                bool pinged = await PingHostAsync(server, port);
                if (pinged && !Connected)
                {
                    await EstablishConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug checking server status: " + ex);
            }
        }

        // Tries to connect if not already connected, updates the UI status after
        public static async void AttemptConnect()
        {
            try
            {
                if (!Connected)
                {
                    await EstablishConnectionAsync();
                    UpdateConnectionStatus(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Could not establish connection: " + ex);
            }
        }

        // Quick TCP ping — tries to open a connection within the timeout.
        // Returns true if the server is reachable, false otherwise.
        public static async Task<bool> PingHostAsync(string host, int port, int timeout = 1000)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(host, port);
                    var delayTask = Task.Delay(timeout);

                    // Whichever finishes first wins — if the delay wins, the server is too slow
                    var completedTask = await Task.WhenAny(connectTask, delayTask);
                    return completedTask == connectTask && tcpClient.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        // Sends a single packet to the server with a 4-byte length prefix.
        // Uses a semaphore so only one write happens at a time (prevents garbled data).
        public static async Task SendDataAsync(byte[] data)
        {
            await writeSemaphore.WaitAsync();
            try
            {
                if (sslStream != null && sslStream.IsAuthenticated && sslStream.CanWrite)
                {
                    // Build the wire format: [4-byte length][payload]
                    byte[] lengthPrefix = BitConverter.GetBytes(data.Length);

                    byte[] message = new byte[lengthPrefix.Length + data.Length];
                    Buffer.BlockCopy(lengthPrefix, 0, message, 0, lengthPrefix.Length);
                    Buffer.BlockCopy(data, 0, message, lengthPrefix.Length, data.Length);

                    await sslStream.WriteAsync(message, 0, message.Length);
                    await sslStream.FlushAsync();
                }
                else
                {
                    Plugin.PluginLog.Debug("Debug: SSL/TLS stream is not authenticated or writable.");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug sending data: " + ex);
            }
            finally
            {
                writeSemaphore.Release();
            }
        }

        // Sends multiple packets in one go — much faster than sending them one at a time
        // because we only acquire the semaphore once and flush once.
        // All packets are combined into a single byte array before writing.
        /// <summary>
        /// Sends multiple packets in a single batch under one semaphore acquire and one flush.
        /// Much faster than calling SendDataAsync for each packet individually.
        /// </summary>
        public static async Task SendBatchAsync(IList<byte[]> packets)
        {
            if (packets == null || packets.Count == 0) return;

            await writeSemaphore.WaitAsync();
            try
            {
                if (sslStream != null && sslStream.IsAuthenticated && sslStream.CanWrite)
                {
                    // Figure out how big the combined buffer needs to be
                    int totalSize = 0;
                    foreach (var data in packets)
                        totalSize += 4 + data.Length; // 4 bytes for each length prefix

                    // Pack all packets into one buffer: [len1][data1][len2][data2]...
                    byte[] combined = new byte[totalSize];
                    int offset = 0;
                    foreach (var data in packets)
                    {
                        byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
                        Buffer.BlockCopy(lengthPrefix, 0, combined, offset, 4);
                        offset += 4;
                        Buffer.BlockCopy(data, 0, combined, offset, data.Length);
                        offset += data.Length;
                    }

                    // One write, one flush for everything
                    await sslStream.WriteAsync(combined, 0, combined.Length);
                    await sslStream.FlushAsync();
                }
                else
                {
                    Plugin.PluginLog.Debug("Debug: SSL/TLS stream is not authenticated or writable.");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug sending batch data: " + ex);
            }
            finally
            {
                writeSemaphore.Release();
            }
        }
    }
}

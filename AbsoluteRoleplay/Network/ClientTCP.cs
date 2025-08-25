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
    public class ClientTCP
    {
        private static readonly SemaphoreSlim writeSemaphore = new SemaphoreSlim(1, 1);
        public static bool Connected;
        public static TcpClient clientSocket;
        public static SslStream sslStream;
        private static byte[] recBuffer = new byte[8192];
        private static readonly string server = "jointest.infinite-roleplay.net";
        private static readonly int port = 53922;
        public static Plugin Plugin;

        // Ensure all access to recBuffer is on the same thread and always copy before use.
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
                    // Stream was disposed, ignore.
                    return;
                }

                if (bytesRead <= 0)
                {
                    Plugin.PluginLog.Debug("No data received or connection closed by the server.");
                    Disconnect();
                    return;
                }

                // Defensive: Always copy the buffer before passing to handler.
                byte[] newBytes = new byte[bytesRead];
                Buffer.BlockCopy(recBuffer, 0, newBytes, 0, bytesRead);

                // Optionally: Validate data here if it will be used for native resources.

                // Process the received data
                try
                {
                    ClientHandleData.HandleData(newBytes);
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Exception in HandleData: " + ex);
                }

                // Continue reading more data asynchronously from the stream
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

        // Asynchronously receive data from the server
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
                        // Stream was disposed, exit loop.
                        break;
                    }

                    if (length <= 0)
                    {
                        Plugin.PluginLog.Debug("Server closed the connection.");
                        Disconnect();
                        break;
                    }

                    Plugin.PluginLog.Debug($"Received {length} bytes from the server.");

                    // Defensive: Always copy the buffer before passing to handler.
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
                        byte[] buffer = new byte[1];
                        int bytesRead = await _tcpClient.Client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.Peek);
                        Plugin.PluginLog.Debug($"Bytes peeked: {bytesRead}");

                        if (bytesRead == 0)
                        {
                            Plugin.PluginLog.Debug("No data received (0 bytes), connection likely closed.");
                            return disconnected;
                        }
                        return connected;
                    }

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

        public static async Task EstablishConnectionAsync()
        {
            try
            {
                clientSocket = new TcpClient();
                await clientSocket.ConnectAsync(server, port);

                sslStream = new SslStream(clientSocket.GetStream(), false, ValidateServerCertificate);

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
                    MainPanel.login = MainPanel.CurrentElement();
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

        public static bool IsConnected()
        {
            return Task.Run(async () => await IsConnectedToServerAsync(clientSocket)).GetAwaiter().GetResult();
        }

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
                            return false;
                        }
                        return true;
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug checking server connection: " + ex);
                return false;
            }
        }

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

        public static async Task<bool> PingHostAsync(string host, int port, int timeout = 1000)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(host, port);
                    var delayTask = Task.Delay(timeout);

                    var completedTask = await Task.WhenAny(connectTask, delayTask);
                    return completedTask == connectTask && tcpClient.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        public static async Task SendDataAsync(byte[] data)
        {
            await writeSemaphore.WaitAsync();
            try
            {
                if (sslStream != null && sslStream.IsAuthenticated && sslStream.CanWrite)
                {
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
    }
}

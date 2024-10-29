using System;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AbsoluteRoleplay;
using System.IO;
using System.Net.Http;
using AbsoluteRoleplay.Windows;
using static AbsoluteRoleplay.Defines;
using System.Collections.Generic;
using System.Numerics;

namespace Networking
{
    public class ClientTCP
    {
        public static bool Connected;
        public static TcpClient clientSocket;
        public static SslStream sslStream;
        private static byte[] recBuffer = new byte[8192];
        private static readonly string server = "jointest.infinite-roleplay.net"; // Replace with your server IP if necessary
        private static readonly int port = 53899; // Ensure this matches the server port
        public static Plugin plugin;
        // Start receiving data from the server asynchronously
        
        private static void OnReceiveData(IAsyncResult result)
        {
            try
            {
                // Retrieve the SslStream from the AsyncState
                SslStream sslStream = (SslStream)result.AsyncState;

                // Complete the asynchronous read operation
                int bytesRead = sslStream.EndRead(result);
                if (bytesRead <= 0)
                {
                    plugin.logger.Error("No data received or connection closed by the server.");
                    Disconnect();
                    return;
                }

                // Log the received data length

                // Create a buffer to hold the new bytes
                byte[] newBytes = new byte[bytesRead];
                Array.Copy(recBuffer, newBytes, bytesRead);

                // Process the received data
                ClientHandleData.HandleData(newBytes);

                // Continue reading more data asynchronously from the stream
                sslStream.BeginRead(recBuffer, 0, recBuffer.Length, OnReceiveData, sslStream);
            }
            catch (IOException ioEx)
            {
                plugin.logger.Error("IO error during data reception: " + ioEx.Message);
                Disconnect();
            }
            catch (ObjectDisposedException ex)
            {
                plugin.logger.Error("SslStream has been disposed: " + ex.Message);
                Disconnect();
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error during data reception: {ex.Message}");
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
                    try
                    {
                        // Read the data asynchronously from the SSL stream
                        int length = await sslStream.ReadAsync(recBuffer, 0, recBuffer.Length);

                        // Check if no data is received, which could indicate the connection is closed
                        if (length <= 0)
                        {
                            plugin.logger.Error("Server closed the connection.");
                            Disconnect();
                            break;
                        }

                        // Log the length of data received for debugging purposes
                        plugin.logger.Error($"Received {length} bytes from the server.");

                        // Create a new byte array to store the actual data read from the buffer
                        var newBytes = new byte[length];
                        Array.Copy(recBuffer, newBytes, length);

                        // Process the data received by passing it to the client-side handler
                        ClientHandleData.HandleData(newBytes);
                    }
                    catch (IOException ioEx)
                    {
                        plugin.logger.Error("IO error during data reception: " + ioEx.Message);
                        Disconnect();
                        break;
                    }
                    catch (ObjectDisposedException ex)
                    {
                        plugin.logger.Error("SslStream has been disposed: " + ex.Message);
                        Disconnect();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error("Error receiving data: " + ex.ToString());
                Disconnect();
            }
        }


      
        public static void UpdateConnectionStatus(TcpClient client)
        {

            if(client.Connected)
            {
                MainPanel.serverStatusColor = new System.Numerics.Vector4(0, 255, 0, 255);
                MainPanel.serverStatus = "Connected";
            }
            else
            {
                MainPanel.serverStatusColor = new System.Numerics.Vector4(255, 0, 0, 255);
                MainPanel.serverStatus = "Disconnected";
            }
        }

        // Get the current connection status
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
                    //plugin.logger.Error($"Poll status - Readable: {isSocketReadable}, Writable: {isSocketWritable}");

                    if (isSocketReadable)
                    {
                        byte[] buffer = new byte[1];
                        int bytesRead = await _tcpClient.Client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.Peek);
                        plugin.logger.Error($"Bytes peeked: {bytesRead}");

                        if (bytesRead == 0)
                        {
                            plugin.logger.Error("No data received (0 bytes), connection likely closed.");

                            return disconnected;
                        }
                        return connected;
                    }

                    if (isSocketWritable && !isSocketReadable)
                    {
                        plugin.logger.Error("Connected");
                        return connected;
                    }

                    plugin.logger.Error("Socket is neither readable nor writable, returning Disconnected.");
                    return disconnected;
                }

                plugin.logger.Error("TcpClient is null or not connected.");
                return disconnected;
            }
            catch (SocketException ex)
            {
                plugin.logger.Error($"SocketException during connection check: {ex.Message}");
                return disconnected;
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Exception during connection check: {ex.Message}");
                return disconnected;
            }
        }



        public static void StartReceiving()
        {
            // Ensure sslStream is authenticated and can read
            if (sslStream != null && sslStream.CanRead)
            {
                sslStream.BeginRead(recBuffer, 0, recBuffer.Length, OnReceiveData, sslStream);
                //plugin.logger.Error("Started reading from SSL/TLS stream.
            }
            else
            {
                plugin.logger.Error("SSL/TLS stream is not readable after handshake.");
            }
        }

        // Establish the actual connection to the server using SSL/TLS
        public static async Task EstablishConnectionAsync()
        {
            try
            {
                clientSocket = new TcpClient();
                //plugin.logger.Error("Attempting to connect to server...");
                await clientSocket.ConnectAsync(server, port);
               // plugin.logger.Error("Connected to server.");

                // Initialize SslStream and authenticate with the server
                sslStream = new SslStream(clientSocket.GetStream(), false, ValidateServerCertificate);

                // Perform the SSL handshake
                sslStream.AuthenticateAsClient(server, null, SslProtocols.Tls12 | SslProtocols.Tls13, false);

                // Check if the stream is authenticated and writable
                if (sslStream.IsAuthenticated && sslStream.CanRead && sslStream.CanWrite)
                {
                    //plugin.logger.Error("SSL/TLS handshake completed and stream is ready for reading.");
                    StartReceiving();  // Call to start reading data
                    
                    
                }
                else
                {
                    plugin.logger.Error("SSL/TLS stream is not authenticated or readable.");
                    Disconnect();
                    plugin.CloseAllWindows();
                    plugin.OpenMainPanel();
                    MainPanel.login = MainPanel.CurrentElement();
                }

            }
            catch (AuthenticationException authEx)
            {
                plugin.logger.Error("SSL/TLS authentication failed: " + authEx.Message);
                if (authEx.InnerException != null)
                    plugin.logger.Error("Inner exception: " + authEx.InnerException.Message);
                Disconnect();
            }
            catch (SocketException sockEx)
            {
                plugin.logger.Error("Socket error during connection: " + sockEx.Message);
                Disconnect();
            }
            catch (Exception ex)
            {
                plugin.logger.Error("Connection error: " + ex.ToString());
                Disconnect();
            }
        }




        // Validate the server certificate (can be customized for production)
        public static bool ValidateServerCertificate(
        object sender, X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("SSL certificate error: " + sslPolicyErrors);
            return false; // Set to true for testing only to bypass errors
        }


        // Disconnect from the server and clean up resources
        public static void Disconnect()
        {
            Connected = false;
            sslStream?.Close();
            sslStream?.Dispose();
            clientSocket?.Close();
            clientSocket?.Dispose();
            plugin.logger.Error("Disconnected from server.");
        }
    

        // Check if the client is currently connected to the server
        public static bool IsConnected()
        {
            return Task.Run(async () => await IsConnectedToServerAsync(clientSocket)).GetAwaiter().GetResult();
        }

        // Asynchronously check if the TCP client is still connected to the server
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
                plugin.logger.Error("Error checking server connection: " + ex.ToString());
                return false;
            }
        }

        // Check the connection status of the server and reconnect if necessary
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
                plugin.logger.Error("Error checking server status: " + ex.ToString());
            }
        }

        // Asynchronously attempt to connect to the server
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
                plugin.logger.Error("Could not establish connection: " + ex.ToString());
            }
        }

        // Ping the server to check if it is reachable
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

        // Send data to the server asynchronously
        public static async Task SendDataAsync(byte[] data)
        {
            try
            {
                if (sslStream != null && sslStream.IsAuthenticated && sslStream.CanWrite)
                {
                    // Prepare the message length (4 bytes for an int)
                    byte[] lengthPrefix = BitConverter.GetBytes(data.Length);

                    // Combine length prefix and actual data
                    byte[] message = new byte[lengthPrefix.Length + data.Length];
                    Array.Copy(lengthPrefix, 0, message, 0, lengthPrefix.Length);
                    Array.Copy(data, 0, message, lengthPrefix.Length, data.Length);

                    // Send the message
                    await sslStream.WriteAsync(message, 0, message.Length);
                    await sslStream.FlushAsync();
                    //plugin.logger.Error($"Data sent to the server successfully. Total length: {message.Length}");
                }
                else
                {
                    plugin.logger.Error("Error: SSL/TLS stream is not authenticated or writable.");
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error("Error sending data: " + ex.ToString());
            }
        }



    }
}

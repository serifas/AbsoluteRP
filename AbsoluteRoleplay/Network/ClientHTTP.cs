using AbsoluteRoleplay;
using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Networking;
public class ClientHTTP
{
    private static HttpClient httpClient = new HttpClient();
    public static Plugin plugin; // Ensure this is initialized properly
    // WebSocket client declaration
    public static ClientWebSocket _client;
    public static string uri = "wss://infinite-roleplay.net/ws";
    // Ensure _client is initialized, especially if it becomes null or closed
    public static void EnsureWebSocketInitialized()
    {
        if (_client == null || _client.State == WebSocketState.Closed || _client.State == WebSocketState.Aborted)
        {
            _client?.Dispose();  // Dispose any existing client
            _client = new ClientWebSocket();  // Reinitialize the WebSocket client
            plugin?.logger?.Error("WebSocket client reinitialized.");
        }
    }
    public static async Task DisconnectWebSocketAsync()
    {
        if (_client == null)
        {
            plugin.logger.Error("WebSocket client is null, nothing to disconnect.");
            return;
        }

        try
        {
            if (_client.State == WebSocketState.Open || _client.State == WebSocketState.CloseReceived)
            {
                // Gracefully close the WebSocket connection
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing connection", CancellationToken.None);
                plugin.logger.Error("WebSocket connection closed by the client.");
            }
            else if (_client.State == WebSocketState.Connecting)
            {
                // Abort if the WebSocket is in a connecting state
                plugin.logger.Error("Aborting WebSocket connection...");
                _client.Abort();
            }
        }
        catch (Exception ex)
        {
            plugin.logger.Error($"Error while closing WebSocket: {ex.Message}");
        }
        finally
        {
            // Dispose of the WebSocket to clean up resources
            if (_client != null)
            {
                _client.Dispose();
                _client = null;  // Set to null so that it can be reinitialized later
                plugin.logger.Error("WebSocket client disposed.");
            }
        }
    }
    public async Task ReconnectWebSocketAsync()
    {
        // First, disconnect if necessary
        await DisconnectWebSocketAsync();

        // Wait a moment before reconnecting
        await Task.Delay(2000);

        // Reinitialize and reconnect the WebSocket
        EnsureWebSocketInitialized();
        bool isConnected = await ConnectWebSocketAsync();

        if (isConnected)
        {
            plugin.logger.Error("Successfully reconnected to the WebSocket server.");
        }
        else
        {
            plugin.logger.Error("Failed to reconnect to the WebSocket server.");
        }
    }

    public static async Task<bool> ConnectWebSocketAsync()
    {
        try
        {
            EnsureWebSocketInitialized();

            if (_client == null)
            {
                plugin?.logger?.Error("WebSocket client is null after initialization.");
                return false;
            }

            plugin?.logger?.Error($"Attempting WebSocket connection to {uri}...");
            await _client.ConnectAsync(new Uri(uri), CancellationToken.None);

            if (_client.State == WebSocketState.Open)
            {
                plugin?.logger?.Error("WebSocket connection established.");

                // Start receiving data once connected
                _ = Task.Run(() => DataReceiver.ReceivePacketsAsync());  // Start listening for packets

                return true;
            }
            else
            {
                plugin?.logger?.Error($"WebSocket connection failed. State: {_client.State}");
                return false;
            }
        }
        catch (WebSocketException wsEx)
        {
            plugin?.logger?.Error($"WebSocketException: {wsEx.Message}");
            return false;
        }
        catch (Exception ex)
        {
            plugin?.logger?.Error($"General Exception: {ex.Message}");
            return false;
        }
    }




    // Send data via WebSocket
    public static async Task SendDataAsync(string data)
    {
        if (_client == null || _client.State != WebSocketState.Open)
        {
            plugin.logger.Error("WebSocket is not connected.");
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(data);
        await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        plugin.logger.Error($"Sent data: {data}");
    }

    // Receive data from WebSocket
    public static async Task ReceiveDataAsync()
    {
        var buffer = new byte[1024];
        while (_client.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                plugin.logger.Error("WebSocket connection closed.");
            }
            else
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                plugin.logger.Error($"Received data: {message}");
            }
        }
    }

    public static Tuple<bool, string> GetWebSocketState()
    {
        if (_client == null)
        {
            return Tuple.Create(false, "WebSocket is not initialized.");
        }

        return _client.State switch
        {
            WebSocketState.None => Tuple.Create(false, "WebSocket is not initialized."),
            WebSocketState.Connecting => Tuple.Create(false, "Connecting to the server..."),
            WebSocketState.Open => Tuple.Create(true, "Connected to the server."),
            WebSocketState.Closed => Tuple.Create(false, "Connection closed."),
            WebSocketState.Aborted => Tuple.Create(false, "Connection aborted."),
            _ => Tuple.Create(false, "Unknown state.")
        };
    }

    // HTTP status check
    public static async Task<string> CheckStatusAsync(string serverUrl)
    {
        try
        {
            var response = await httpClient.GetAsync($"{serverUrl}/status");
            return response.IsSuccessStatusCode ? "Connected" : "Disconnected";
        }
        catch (Exception ex)
        {
            plugin.logger.Error($"Failed to check status: {ex.Message}");
            return "Error";
        }
    }
}


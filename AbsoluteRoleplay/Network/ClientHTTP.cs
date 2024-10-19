using AbsoluteRoleplay;
using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class ClientHTTP
{
    private static ClientWebSocket _client = new ClientWebSocket();
    private static HttpClient httpClient = new HttpClient();
    public static Plugin plugin;// Ensure this is https
    public static void CheckConnectionStatus()
    {
        string status = GetWebSocketState().Item2;
        plugin.logger.Error($"Current WebSocket State: {status}");
    }

    // Connect to WebSocket with SSL (wss)
    public static async Task<bool> ConnectWebSocketAsync(string uri)
    {
        try
        {
            plugin.logger.Error(uri);
            uri = uri.Replace("ws://", "wss://");

            await _client.ConnectAsync(new Uri(uri), CancellationToken.None);
            plugin.logger.Error("WebSocket connection established.");

            // Print connection status after connecting
            CheckConnectionStatus();

            return _client.State == WebSocketState.Open;
        }
        catch (Exception ex)
        {
            plugin.logger.Error($"Failed to connect WebSocket: {ex.Message}");
            return false;
        }
    }


    // Send data via WebSocket
    public static async Task SendDataAsync(string data)
    {
        if (_client.State != WebSocketState.Open)
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
            _ =>    Tuple.Create(false, "Unknown state.")
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

using HttpClientExtended.Helpers;
using HttpClientExtended.Interfaces;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClientExtended
{
    //TODO: make this abstract
    public class ClientWebSocketConnection : IDisposable
    {
        protected readonly IWebSocketLogger _logger;
        protected readonly Uri _baseUrl;
        protected readonly HttpConnection _httpConnection;
        protected readonly ClientWebSocket _clientWebSocket;
        //TODO: this should be marked virtual 
        public const int MaxBufferLength = 15000;
        public WebSocketState State { get => _clientWebSocket.State; }
        public ClientWebSocketOptions WebSocketOptions { get => _clientWebSocket.Options; }
        public ClientWebSocketConnection(IWebSocketLogger logger, HttpConnection connection)
        {
            _logger = logger;
            _httpConnection = connection;
            _clientWebSocket = new ClientWebSocket();

            var requestHeaders = _httpConnection.RequestHeaders;
            _clientWebSocket.Options.SetRequestHeader("User-Agent", requestHeaders.UserAgent.ToString());
            _clientWebSocket.Options.SetRequestHeader("Cache-Control", requestHeaders.CacheControl.ToString());
        }
        protected virtual async Task ConnectAsync(Uri baseUrl)
            => await _clientWebSocket.ConnectAsync(baseUrl, CancellationToken.None);

        protected virtual async Task CloseAsync(WebSocketCloseStatus status, string description)
            => await _clientWebSocket.CloseAsync(status, description, CancellationToken.None);

        public virtual async Task SendAsync(string payload)
        {
            var buffer = Encoding.ASCII.GetBytes(payload);

            _logger.WriteLine(Enums.WebSocketMessageType.Sent, payload, buffer.Length, TimeStamp.Now());
            
            await _clientWebSocket.SendAsync(buffer, WebSocketMessageType.Binary, false, CancellationToken.None);
        }
        public virtual async Task<string> ReceiveAsync()
        {
            var buffer = new byte[MaxBufferLength];
            
            await _clientWebSocket.ReceiveAsync(buffer, CancellationToken.None);
            
            var resultString = Encoding.Default.GetString(buffer);

            _logger.WriteLine(Enums.WebSocketMessageType.Recieved, $"{Uri.UnescapeDataString(resultString.Trim('\0'))}\n", resultString.Length, TimeStamp.Now());

            return resultString.Trim('\0');
        }
        public void Dispose()
        {
            _clientWebSocket.Dispose();
        }
    }
}

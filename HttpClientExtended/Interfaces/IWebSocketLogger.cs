using HttpClientExtended.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace HttpClientExtended.Interfaces
{
    public interface IWebSocketLogger
    {
        void Write(WebSocketMessageType webSocketMessageType, string data, int length, int time);
        void WriteLine(WebSocketMessageType webSocketMessageType, string data, int length, int time);
    }
}

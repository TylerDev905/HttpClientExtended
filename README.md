# HttpClientExtended
A HttpClient that provides support for cookies, logging, request/response model json serilization and support for client web sockets.

[![Build status](https://ci.appveyor.com/api/projects/status/x6d33gkblthvve23/branch/master?svg=true)](https://ci.appveyor.com/project/TylerH/httpclientextended/branch/master)

### Example login service - used for posting login credentials via POST request
```CSharp
public class LoginService : HttpClientBaseService
    {
        public LoginService(IHttpConnectionLogger logger, HttpConnection connection) : base(logger, connection) { }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var formUrlEncodedContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("tz", "-5")
            });

            _logger.WriteLine($"Attempting to login as {username}");
            var result =  await _httpConnection.PostAsync("/php/login.php", formUrlEncodedContent);

            var isLoggedIn = result.Contains(username) 
                && result.Contains("login_complete: true") ? true : false;

            return isLoggedIn;
        }
    }
```

### Using the login service
```CSharp
        var logger = new ConsoleLogger();

          using (var connection = new HttpConnection(logger, new Uri("https://www.mywebsite.com/")))
          {
              var loginService = new LoginService(logger, connection);

              var isLoggedIn = await loginService.LoginAsync(_username, _password);

              if (isLoggedIn)
              {
                  // do things here
              }
          }
```

### Creating a web socket connection to communicate with a chat room
```CSharp
public class WebSocketConnection : ClientWebSocketConnection
    {
        private int _uid { get; set; }
        
        public List<ReceivedMessage> ReceivedMessages { get; set; }

        public WebSocketConnection(IWebSocketLogger logger, HttpConnection connection) : base(logger, connection) { }
        
        public async Task ConnectAsync(int socketServerId)
        {
            var socketUrl = $"wss://chatServer{socketServerId}.mywebsite.com/chat";

            await ConnectAsync(new Uri(socketUrl));

            var cookies = _httpConnection.Cookies;

            await SendAsync($"1 0 0 1025 0 1/{cookies["authToken"]}");

            var result = await ReceiveAsync();

            _uid = int.Parse(result.Split(' ')[2]);
        }
        public async Task JoinPublicChatChannelAsync(int roomUid)
            => await SendAsync($"51 {_uid} 0 {roomUid} 9");

        public async Task SendPublicChatMessageAsync(int roomUid, string message)
            => await SendAsync($"50 {_uid} {roomUid} 0 0 {Uri.EscapeDataString(message)}");

        public async Task LeavePublicChatChannelAsync(int roomUid)
            => await SendAsync($"51 {_uid} 0 {roomUid} 2");
        
        public async Task JoinPrivateChatChannelAsync(int roomUid)
            => await SendAsync($"57 {_uid} 0 1 {roomUid}");

        public async Task SendPrivateChatMessageAsync(int roomUid, string message)
        {
            await SendAsync($"75 {_uid} 0 {roomUid} 0");

            await SendAsync($"3 {roomUid} {_uid} 0 0 {Uri.EscapeDataString(message)}");
        }
        public async Task LeavePrivateChatChannelAsync(int roomUid) 
            => await SendAsync($"57 {_uid} 0 2 {roomUid}");

        public async Task StayAwakeAsync()
            => await SendAsync("0 0 0 0 0");

        public override Task SendAsync(string payload)
            => base.SendAsync($"{payload}\n\0");

        public async Task ListenAsync()
        {
            ReceivedMessages = new List<ReceivedMessage>();

            while(_clientWebSocket.State != WebSocketState.Closed)
            {
                var response = await ReceiveAsync();

                if (!string.IsNullOrEmpty(response))
                {
                    var args = response.Split(' ');

                    var hasCommand = Regex.IsMatch(args[0], "(0-9{6,8})");

                    for(var i = 0; i < args.Length; i++)
                    {
                        args[i] = Uri.UnescapeDataString(Regex.Unescape(args[i]));
                    }

                    if (args.Length > 0 && hasCommand)
                    {
                        var commandId = Convert.ToInt32(args[0], 16);

                        ReceivedMessage recievedMessage = new ReceivedMessage();

                        switch (commandId)
                        {
                            case 0x00003155:
                                recievedMessage = JsonSerializer.Deserialize<AccountDetails>(args[5]);
                                break;
                            case 0x00025281:
                                recievedMessage = JsonSerializer.Deserialize<ServerDetails>(args[5]);
                                break;
                        }

                        recievedMessage.CommandArgs = args;
                        recievedMessage.TimeStamp = TimeStamp.Now();
                        ReceivedMessages.Add(recievedMessage);
                    }
                }
            }
        }
    }
```

### Using the websocoket connection to send messages to the chat
```CSharp
if (isLoggedIn)
{

    using(var webSocketConnection = new WebSocketConnection(logger, httpConnection))
    {
        // connect to the chat server 57
        await webSocketConnection.ConnectAsync(57);
        
        await webSocketConnection.JoinPublicChatChannelAsync(_roomUId);

        await webSocketConnection.SendPublicChatMessageAsync(_roomUId, "Hey users in chat how are you?");
    }   
}
```

### Example of using the HttpClientExtended library to record an hls stream to a video file.
```CSharp
public class StreamRecordingService : HttpClientBaseService
    {
        private readonly Guid _streamSessionId;
        private bool _isRecording { get; set; } = true;
        public StreamRecordingService(IHttpConnectionLogger logger, HttpConnection connection) : base(logger, connection) 
        {
            _streamSessionId = Guid.NewGuid();
        }
        public async Task RecordStreamAsync(RoomDetails roomDetails, string outputDirectory)
        {
            var hlsSource = roomDetails.HlsSource;
            
            var playList = await _httpConnection.GetAsync(hlsSource.AbsolutePath);

            var chunkListFileName = Regex.Matches(playList, "chunklist_(.{1,}).m3u8")
                .Cast<Match>()
                .Select(m => m.Groups[0].Value)
                .Last();

            var mediaRoute = $"{hlsSource.Segments[0]}{hlsSource.Segments[1]}{hlsSource.Segments[2]}";

            var fileName = @$"{outputDirectory}\{roomDetails.BroadcasterUsername}-{_streamSessionId}.ts";

            using (var fileStream = new FileStream(fileName, FileMode.Create))
            {
                var lastSegment = string.Empty;

                while (_isRecording)
                {
                    var chunkListRelativePath = $"{mediaRoute}{chunkListFileName}";
                    var chunkList = await _httpConnection.GetAsync(chunkListRelativePath);

                    var videoSegmentFileName = Regex.Matches(chunkList, "stream_(.{1,})\\.ts")
                        .Cast<Match>()
                        .Select(m => m.Groups[0].Value)
                        .Last();

                    if (lastSegment != videoSegmentFileName)
                    {
                        lastSegment = videoSegmentFileName;

                        var videoSegmentRelativePath = $"{mediaRoute}{videoSegmentFileName}";

                        var videoStream = await _httpConnection.GetStreamAsync(videoSegmentRelativePath);

                        _logger.WriteLine($"Saving video segment {videoSegmentFileName}");

                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            videoStream.CopyTo(memoryStream);
                            fileStream.Write(memoryStream.ToArray());
                        }
                    }

                    await Task.Delay(500);
                }
            }
            
        }
        public void StopRecording()
        {
            _logger.WriteLine("Recording has been stopped");
            _isRecording = false;
        }
    }
```

### Example of a console logger class

```CSharp
public class ConsoleLogger : IHttpConnectionLogger, IWebSocketLogger
    {
        public void Write(string message)
        {
            Console.Write(message);
        }
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public void Write(WebSocketMessageType webSocketMessageType, string data, int length, int time)
        {
            if (webSocketMessageType == WebSocketMessageType.Sent)
            {
                Console.WriteLine($"Direction: Sent");
            }
            else
            {
                Console.WriteLine($"Direction: Received");
            }

            Console.Write($"Time: {time} Length: {length} Payload: {data}");
        }

        public void WriteLine(WebSocketMessageType webSocketMessageType, string data, int length, int time)
        {
            if (webSocketMessageType == WebSocketMessageType.Sent)
            {
                Console.WriteLine($"Direction: Sent");
            }
            else
            {
                Console.WriteLine($"Direction: Received");
            }

            Console.WriteLine($"Time: {time}\n Length: {length}\n Payload: {data}\n");
        }
    }
```

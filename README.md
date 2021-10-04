# HttpClientExtended
A HttpClient that provides support for cookies, request/response model json serilization and support for client web sockets.


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
        protected int _uid { get; set; }
        
        public List<ReceivedMessage> ReceivedMessages { get; set; }

        public WebSocketConnection(IWebSocketLogger logger, HttpConnection connection) : base(logger, connection) { }
        
        public async Task ConnectAysnc(int socketServerId)
        {
            var socketUrl = $"wss://chatServer{socketServerId}.mywebsite.com/chat";

            await ConnectAsync(new Uri(socketUrl));

            var cookies = _httpConnection.Cookies;

            await SendAsync($"1 0 0 1025 0 1/{cookies["username"]}:{cookies["passcode"]}");

            var result = await ReceiveAsync();

            _uid = int.Parse(result.Split(' ')[2]);
        }
        public async Task JoinPublicChatChannelAysnc(int roomUid)
            => await SendAsync($"51 {_uid} 0 {roomUid} 9");

        public async Task SendPublicChatMessageAysnc(int roomUid, string message)
            => await SendAsync($"50 {_uid} {roomUid} 0 0 {Uri.EscapeDataString(message)}");

        public async Task LeavePublicChatChannelAysnc(int roomUid)
            => await SendAsync($"51 {_uid} 0 {roomUid} 2");
        
        public async Task JoinPrivateChatChannelAysnc(int roomUid)
            => await SendAsync($"57 {_uid} 0 1 {roomUid}");

        public async Task SendPrivateChatMessageAysnc(int roomUid, string message)
        {
            await SendAsync($"75 {_uid} 0 {roomUid} 0");

            await SendAsync($"3 {roomUid} {_uid} 0 0 {Uri.EscapeDataString(message)}");
        }
        public async Task LeavePrivateChatChannelAysnc(int roomUid) 
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
        await webSocketConnection.ConnectAysnc(57);
        
        await webSocketConnection.JoinPrivateChatChannelAysnc(_roomUId);

        await webSocketConnection.SendPrivateChatMessageAysnc(_roomUId, "Hey users in chat how are you?");
    }   
}
```



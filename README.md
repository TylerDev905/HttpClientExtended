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

          using (var connection = new HttpConnection(logger, new Uri("https://www.mywebsite/")))
          {
              var loginService = new LoginService(logger, connection);

              var isLoggedIn = await loginService.LoginAsync(_username, _password);

              if (isLoggedIn)
              {
                  // do things here
              }
          }
```



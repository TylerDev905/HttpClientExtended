using HttpClientExtended.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HttpClientExtended
{
    public class HttpConnection : IDisposable
    {
        protected readonly IHttpConnectionLogger _logger;
        protected readonly Uri _baseUrl;
        protected readonly HttpClient _httpClient;
        protected readonly HttpClientHandler _httpClientHandler;
        protected readonly CookieContainer _cookieContainer;
        public HttpRequestHeaders RequestHeaders 
        { 
            get => _httpClient.DefaultRequestHeaders; 
        }
        public HttpConnection(IHttpConnectionLogger logger, Uri baseUrl, bool ignoreSSLCertErrors = false)
        {
            _baseUrl = baseUrl;
            _logger = logger;
            _cookieContainer = new CookieContainer();
            _httpClientHandler = new HttpClientHandler()
            {
                CookieContainer = _cookieContainer,
                AllowAutoRedirect = true
            };
            // Use this when in a local environment
            if (ignoreSSLCertErrors)
            {
                _httpClientHandler.ServerCertificateCustomValidationCallback
                    = (httpRequestMessage, cert, cetChain, policyErrors) => true;
            }
            _httpClient = new HttpClient(_httpClientHandler, false);
            _httpClient.BaseAddress = baseUrl;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9");
        }

        public Dictionary<string, string> Cookies
        { 
            get => GetCookies(); 
            set => SetCookies(value); 
        }

        private Dictionary<string, string> GetCookies()
        {
            var cookieCollection = _cookieContainer.GetCookies(_baseUrl);

            var cookieDictionary = new Dictionary<string, string>();

            foreach (var cookie in cookieCollection)
            {
                var split = cookie.ToString().Split('=');
                cookieDictionary.Add(split[0], split[1]);
            }

            return cookieDictionary;
        }
        private void SetCookies(Dictionary<string, string> cookiesDictionary)
        {
            var cookies = new List<string>();

            foreach(var cookie in cookiesDictionary)
            {
                cookies.Add($"{cookie.Key}={cookie.Value}");
            }

            _cookieContainer.SetCookies(_baseUrl, string.Join(',', cookies));
        }
        public async Task<Stream> GetStreamAsync(string relativeUrl)
            => await _httpClient.GetStreamAsync(relativeUrl);

        public async Task<string> GetAsync(string relativeUrl)
        {
            var response = await _httpClient.GetAsync(relativeUrl);

            var content = await response.Content.ReadAsStringAsync();

            _httpClient.DefaultRequestHeaders.Referrer = new Uri($"{_baseUrl.OriginalString}{relativeUrl}");

            _logger.WriteLine($"{JsonSerializer.Serialize(response)}\n");
            return content;
        }
        public async Task<TReceive> GetAsJsonAsync<TReceive>(string relativeUrl)
        {
            var responseString = await GetAsync(relativeUrl);
            return JsonSerializer.Deserialize<TReceive>(responseString);
        }
        public async Task<string> PostAsync(string relativeUrl, FormUrlEncodedContent formUrlEncodedContent)
        {
            var response = await _httpClient.PostAsync(relativeUrl, formUrlEncodedContent);
            _logger.WriteLine($"{JsonSerializer.Serialize(response)}\n");
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<TReceive> PostAsync<TReceive>(string relativeUrl, FormUrlEncodedContent formUrlEncodedContent)
        {
            var responseString = await PostAsync(relativeUrl, formUrlEncodedContent);
            return JsonSerializer.Deserialize<TReceive>(responseString);
        }
        public async Task<string> PostAsJsonAsync<TSend>(string relativeUrl, TSend model)
        {
            var json = JsonSerializer.Serialize(model);
            var response = await _httpClient.PostAsync(relativeUrl, new StringContent(json, Encoding.UTF8, "application/json"));
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<TReceive> PostAsJsonAsync<TSend, TReceive>(string relativeUrl, TSend model)
        {
            var responseString = await PostAsJsonAsync(relativeUrl, model);
            return JsonSerializer.Deserialize<TReceive>(responseString);
        }
        public void Dispose()
        {
            _httpClientHandler.Dispose();
            _httpClient.Dispose();
        }
    }
}

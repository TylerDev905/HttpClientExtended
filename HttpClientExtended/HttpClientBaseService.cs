using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net;
using HttpClientExtended.Interfaces;

namespace HttpClientExtended
{
    public abstract class HttpClientBaseService
    {
        protected readonly IHttpConnectionLogger _logger;
        protected readonly HttpConnection _httpConnection;
        public HttpClientBaseService(IHttpConnectionLogger logger, HttpConnection connection)
        {
            _logger = logger;
            _httpConnection = connection;
        }
    }
}

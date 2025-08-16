using Microsoft.EntityFrameworkCore;
using FGate.Data;
using FGate.Data.Entities;
using System.Diagnostics;   
using System.Text.Json;
using Microsoft.Extensions.Primitives;   

namespace FGate.Services
{
    public class DbProxyRequestLogger : IProxyRequestLogger
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbProxyRequestLogger> _logger;
        private readonly IEndpointCategorizer _endpointCategorizer;     

        public DbProxyRequestLogger(
            IDbContextFactory<ProxyRaliDbContext> dbContextFactory,
            ILogger<DbProxyRequestLogger> logger,
            IEndpointCategorizer endpointCategorizer)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _endpointCategorizer = endpointCategorizer;
        }

        public async Task LogRequestAsync(HttpContext context, Func<Task> next)
        {
            var request = context.Request;
            var requestTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            request.EnableBuffering();
            string requestBodyPreview = "N/A";
            long? requestBodySizeBytes = request.ContentLength;

            if (request.ContentLength > 0 && request.Body.CanRead)
            {
                using (var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                {
                    var fullBody = await reader.ReadToEndAsync();
                    requestBodyPreview = fullBody.Length > 500 ? fullBody.Substring(0, 500) + "..." : fullBody;     
                }
                request.Body.Position = 0;      
            }

            string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "N/A";
            string method = request.Method;
            string path = request.Path.Value ?? string.Empty;
            string queryString = request.QueryString.HasValue ? request.QueryString.Value : null;
            string requestHeaders = SerializeHeaders(request.Headers);
            string userAgent = request.Headers["User-Agent"].FirstOrDefault();

            int? tokenIdUsed = null;
            bool? wasTokenValid = null;
            if (context.Items.TryGetValue("TokenValidationResult_TokenId", out var tokenIdObj) && tokenIdObj is int tId)
            {
                tokenIdUsed = tId;
            }
            if (context.Items.TryGetValue("TokenValidationResult_IsValid", out var isValidObj) && isValidObj is bool isValid)
            {
                wasTokenValid = isValid;
            }


            var originalResponseBodyStream = context.Response.Body;
            using var responseBodyMemoryStream = new MemoryStream();
            context.Response.Body = responseBodyMemoryStream;

            string? proxyError = null;
            string? backendTarget = null;       

            try
            {
                await next.Invoke();           
            }
            catch (Exception ex)
            {
                proxyError = $"Excepción en el pipeline: {ex.Message}";
                context.Response.Body = originalResponseBodyStream;
                throw;           
            }
            finally               
            {
                stopwatch.Stop();
                var durationMs = (int)stopwatch.ElapsedMilliseconds;

                responseBodyMemoryStream.Position = 0;
                string responseBodyPreview = "N/A";
                long? responseBodySizeBytes = responseBodyMemoryStream.Length > 0 ? responseBodyMemoryStream.Length : null;

                if (responseBodySizeBytes > 0)
                {
                    using (var reader = new StreamReader(responseBodyMemoryStream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true, bufferSize: 1024))
                    {
                        char[] buffer = new char[500];
                        int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                        responseBodyPreview = new string(buffer, 0, charsRead);
                        if (responseBodySizeBytes > 500) responseBodyPreview += "...";
                    }
                    responseBodyMemoryStream.Position = 0;       
                }

                if (context.Response.HasStarted == false || responseBodyMemoryStream.Length > 0)         
                {
                    try
                    {
                        await responseBodyMemoryStream.CopyToAsync(originalResponseBodyStream);
                    }
                    catch (ObjectDisposedException odEx)
                    {
                    }
                    catch (Exception ex)
                    {
                    }
                }
                context.Response.Body = originalResponseBodyStream;     


                var endpointGroupCategorization = _endpointCategorizer.GetEndpointGroupForPath(path);
                string endpointGroupAccessed = endpointGroupCategorization?.GroupName ?? "Unknown";

                var reverseProxyFeature = context.Features.Get<Yarp.ReverseProxy.Model.IReverseProxyFeature>();
                backendTarget = reverseProxyFeature?.ProxiedDestination?.Model?.Config?.Address;


                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var logEntry = new RequestLog
                {
                    RequestId = context.TraceIdentifier,     
                    TimestampUtc = requestTime,
                    ClientIpAddress = clientIp,
                    HttpMethod = method,
                    RequestPath = path,
                    QueryString = queryString,
                    RequestHeaders = requestHeaders,
                    RequestBodyPreview = requestBodyPreview,
                    RequestSizeBytes = requestBodySizeBytes,
                    TokenIdUsed = tokenIdUsed,
                    WasTokenValid = wasTokenValid,
                    EndpointGroupAccessed = endpointGroupAccessed,
                    BackendTargetUrl = backendTarget,
                    ResponseStatusCode = context.Response.StatusCode,
                    ResponseHeaders = SerializeHeaders(context.Response.Headers),
                    ResponseBodyPreview = responseBodyPreview,
                    ResponseSizeBytes = responseBodySizeBytes,
                    DurationMs = durationMs,
                    ProxyProcessingError = proxyError,        
                    UserAgent = userAgent,
                };

                try
                {
                    dbContext.RequestLogs.Add(logEntry);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception dbEx)
                {
                }
            }
        }

        private string SerializeHeaders(IHeaderDictionary headers)
        {
            if (headers == null || !headers.Any()) return null;
            try
            {
                var filteredHeaders = headers
                    .ToDictionary(h => h.Key, h => h.Value.ToString());
                return JsonSerializer.Serialize(filteredHeaders);
            }
            catch (Exception ex)
            {
                return "{\"error\":\"Error serializando encabezados\"}";
            }
        }
    }
}
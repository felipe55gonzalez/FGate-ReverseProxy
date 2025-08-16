using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using FGate.Data.Entities;
using FGate.Services;
using System.Net;

namespace FGate.Middlewares
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public RateLimitingMiddleware(RequestDelegate next, IMemoryCache cache, ILogger<RateLimitingMiddleware> logger, IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
            _scopeFactory = scopeFactory;     
        }
        
        public async Task InvokeAsync(HttpContext context, IEndpointCategorizer endpointCategorizer, IDbContextFactory<ProxyRaliDbContext> dbFactory)
        {
            var endpointResult = endpointCategorizer.GetEndpointGroupForPath(context.Request.Path);
            if (endpointResult == null) { await _next(context); return; }

            await using var dbContext = await dbFactory.CreateDbContextAsync();
            var group = await dbContext.EndpointGroups
                .Include(g => g.RateLimitRule)
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GroupName == endpointResult.GroupName);

            var rule = group?.RateLimitRule;
            if (rule == null) { await _next(context); return; }

            var clientIp = context.Connection.RemoteIpAddress;
            if (clientIp == null) { await _next(context); return; }

            var cacheKey = $"{rule.RuleId}_{clientIp}";
            var entry = _cache.Get<RateLimitEntry>(cacheKey);

            if (entry != null && entry.Count >= rule.RequestLimit)
            {
                if (!entry.IsBlocked)
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        await notificationService.TriggerAlertAsync(
                            level: AlertLevel.Warning,
                            title: "Límite de Tasa Excedido",
                            details: new { rule.RuleName, clientIp = clientIp.ToString(), rule.RequestLimit, rule.PeriodSeconds }
                        );
                    }
                    entry.IsBlocked = true;
                }

                _logger.LogWarning("Límite de tasa excedido para la IP {Ip} en la regla {RuleName}", clientIp, rule.RuleName);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                await context.Response.WriteAsync("Rate limit exceeded.");
                return;
            }

            var newCount = (entry?.Count ?? 0) + 1;
            var newEntry = new RateLimitEntry { Count = newCount, IsBlocked = false };

            var expiration = entry == null ? (TimeSpan?)TimeSpan.FromSeconds(rule.PeriodSeconds) : null;
            if (expiration.HasValue)
            {
                _cache.Set(cacheKey, newEntry, expiration.Value);
            }

            await _next(context);
        }

        private class RateLimitEntry
        {
            public int Count { get; set; }
            public bool IsBlocked { get; set; }
        }
    }
}
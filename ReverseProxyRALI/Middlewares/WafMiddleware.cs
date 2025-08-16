using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using FGate.Data.Entities;
using FGate.Services;
using System.Net;
using System.Text.RegularExpressions;

namespace FGate.Middlewares
{
    public class WafMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly IAuditLogger _auditLogger;
        private readonly ILogger<WafMiddleware> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public WafMiddleware(RequestDelegate next, IMemoryCache cache, IAuditLogger auditLogger, ILogger<WafMiddleware> logger, IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _cache = cache;
            _auditLogger = auditLogger;
            _logger = logger;
            _scopeFactory = scopeFactory;     
        }

        public async Task InvokeAsync(HttpContext context, IEndpointCategorizer endpointCategorizer, IDbContextFactory<ProxyRaliDbContext> dbFactory)
        {
            var endpointResult = endpointCategorizer.GetEndpointGroupForPath(context.Request.Path);
            if (endpointResult?.GroupName == null)
            {
                await _next(context);
                return;
            }

            var cacheKey = $"waf_rules_{endpointResult.GroupName}";
            if (!_cache.TryGetValue(cacheKey, out List<WafRule>? rules))
            {
                await using var dbContext = await dbFactory.CreateDbContextAsync();
                rules = await dbContext.EndpointGroups
                    .Where(g => g.GroupName == endpointResult.GroupName)
                    .SelectMany(g => g.EndpointGroupWafRules.Select(gr => gr.WafRule))
                    .Where(r => r.IsEnabled).AsNoTracking().ToListAsync();
                _cache.Set(cacheKey, rules, TimeSpan.FromMinutes(5));
            }

            if (rules == null || !rules.Any())
            {
                await _next(context);
                return;
            }

            var requestUrl = context.Request.GetDisplayUrl();

            foreach (var rule in rules)
            {
                try
                {
                    if (Regex.IsMatch(requestUrl, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100)))
                    {
                        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "N/A";

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                            await notificationService.TriggerAlertAsync(
                                level: AlertLevel.Warning,
                                title: $"WAF: Regla '{rule.RuleName}' activada",
                                details: new { rule.RuleName, rule.Pattern, clientIp, requestUrl, ActionTaken = rule.Action }
                            );
                        }

                        await _auditLogger.LogEventAsync(
                            entityType: "WafViolation", entityId: rule.RuleId.ToString(), action: "Triggered",
                            userId: context.User.Identity?.Name, affectedComponent: "WafMiddleware", clientIpAddress: clientIp,
                            newValues: new { RuleName = rule.RuleName, RequestUrl = requestUrl, ActionTaken = rule.Action }
                        );

                        if (rule.Action == "Block")
                        {
                            _logger.LogWarning("WAF: Petición bloqueada desde la IP {Ip} por la regla '{RuleName}' en la URL: {Url}", clientIp, rule.RuleName, requestUrl);
                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            await context.Response.WriteAsync("Forbidden: Your request was blocked by the security firewall.");
                            return;
                        }
                    }
                }
                catch (RegexMatchTimeoutException) {    }
            }

            await _next(context);
        }
    }
}
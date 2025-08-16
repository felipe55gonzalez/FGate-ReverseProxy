using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Areas.Admin.Models;
using FGate.Data.Entities;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class TokenAnalyticsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;

        public TokenAnalyticsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IActionResult> Index()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var tokenSummaries = await context.ApiTokens
                .Select(token => new TokenUsageSummaryViewModel
                {
                    TokenId = token.TokenId,
                    Description = token.Description ?? "Sin Descripción",
                    OwnerName = token.OwnerName,
                    IsEnabled = token.IsEnabled,
                    TotalRequests = token.RequestLogs.Count(),
                    LastUsedUtc = token.RequestLogs.Any() ? token.RequestLogs.Max(rl => rl.TimestampUtc) : (DateTime?)null
                })
                .OrderByDescending(s => s.LastUsedUtc)
                .ToListAsync();

            return View(tokenSummaries);
        }

        public async Task<IActionResult> Details(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var token = await context.ApiTokens.FindAsync(id);

            if (token == null)
            {
                return NotFound();
            }

            var requestLogs = await context.RequestLogs
                .Where(rl => rl.TokenIdUsed == id)
                .AsNoTracking()
                .ToListAsync();

            var usageByEndpointData = requestLogs
                .Where(rl => !string.IsNullOrEmpty(rl.EndpointGroupAccessed))
                .GroupBy(rl => rl.EndpointGroupAccessed)
                .Select(g => new { name = g.Key, value = g.Count() })
                .OrderByDescending(x => x.value)
                .ToList();

            var usageByEndpointChart = new
            {
                tooltip = new { trigger = "item" },
                legend = new { orient = "vertical", left = "left" },
                series = new[]
                {
                    new {
                        name = "Peticiones",
                        type = "pie",
                        radius = "50%",
                        data = usageByEndpointData
                    }
                }
            };

            var statusCodesData = requestLogs
                .GroupBy(rl => rl.ResponseStatusCode)
                .Select(g => new { StatusCode = g.Key.ToString(), Count = g.Count() })
                .OrderBy(x => x.StatusCode)
                .ToList();

            var statusCodesChart = new
            {
                xAxis = new { type = "category", data = statusCodesData.Select(d => d.StatusCode).ToList() },
                yAxis = new { type = "value" },
                series = new[] { new { data = statusCodesData.Select(d => d.Count).ToList(), type = "bar" } },
                tooltip = new { trigger = "axis" }
            };

            var viewModel = new TokenUsageDetailViewModel
            {
                TokenId = token.TokenId,
                Description = token.Description ?? "Sin Descripción",
                OwnerName = token.OwnerName,
                IsEnabled = token.IsEnabled,
                TokenValue = token.TokenValue,
                TotalRequests = requestLogs.Count,
                FirstUsedUtc = requestLogs.Any() ? requestLogs.Min(rl => rl.TimestampUtc) : null,
                LastUsedUtc = requestLogs.Any() ? requestLogs.Max(rl => rl.TimestampUtc) : null,
                UsageByEndpointChartJson = AnalyticsViewModel.SerializeEChartData(usageByEndpointChart),
                StatusCodesChartJson = AnalyticsViewModel.SerializeEChartData(statusCodesChart),
                RecentRequests = requestLogs
                                    .OrderByDescending(rl => rl.TimestampUtc)
                                    .Take(15)
                                    .Select(rl => new RecentRequestLog
                                    {
                                        TimestampUtc = rl.TimestampUtc,
                                        HttpMethod = rl.HttpMethod,
                                        RequestPath = rl.RequestPath,
                                        ResponseStatusCode = rl.ResponseStatusCode,
                                        DurationMs = rl.DurationMs
                                    }).ToList()
            };

            return View(viewModel);
        }
    }
}
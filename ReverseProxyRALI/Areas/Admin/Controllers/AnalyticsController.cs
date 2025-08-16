
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Areas.Admin.Models;
using FGate.Data.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class AnalyticsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;

        public AnalyticsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IActionResult> Index()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);

            var hourlyData = await context.HourlyTrafficSummaries
                .Where(s => s.HourUtc >= twentyFourHoursAgo)
                .OrderBy(s => s.HourUtc)
                .GroupBy(s => s.HourUtc)
                .Select(g => new
                {
                    Hour = g.Key,
                    TotalRequests = g.Sum(s => s.RequestCount ?? 0),
                    Total4xx = g.Sum(s => s.ErrorCount4xx ?? 0),
                    Total5xx = g.Sum(s => s.ErrorCount5xx ?? 0),
                    AvgLatency = g.Average(s => s.AverageDurationMs ?? 0)
                })
                .ToListAsync();

            var requestLogsData = await context.RequestLogs
                .Where(r => r.TimestampUtc >= twentyFourHoursAgo)
                .Select(r => new { r.HttpMethod, r.RequestPath, r.DurationMs, r.EndpointGroupAccessed })
                .ToListAsync();

            var hourlyTrafficChart = new
            {
                xAxis = new { type = "category", data = hourlyData.Select(d => d.Hour.ToLocalTime().ToString("HH:00")).ToList() },
                yAxis = new { type = "value" },
                series = new[] { new { data = hourlyData.Select(d => d.TotalRequests).ToList(), type = "line", smooth = true, name = "Solicitudes" } },
                tooltip = new { trigger = "axis" }
            };

            var errorsChart = new
            {
                legend = new { data = new[] { "Errores 4xx", "Errores 5xx" } },
                xAxis = new { type = "category", data = hourlyData.Select(d => d.Hour.ToLocalTime().ToString("HH:00")).ToList() },
                yAxis = new { type = "value" },
                series = new[]
                {
                    new { name = "Errores 4xx", type = "bar", stack = "errors", data = hourlyData.Select(d => d.Total4xx).ToList() },
                    new { name = "Errores 5xx", type = "bar", stack = "errors", data = hourlyData.Select(d => d.Total5xx).ToList() }
                },
                tooltip = new { trigger = "axis" }
            };

            var latencyChart = new
            {
                xAxis = new { type = "category", data = hourlyData.Select(d => d.Hour.ToLocalTime().ToString("HH:00")).ToList() },
                yAxis = new { type = "value", axisLabel = new { formatter = "{value} ms" } },
                series = new[] { new { data = hourlyData.Select(d => Math.Round(d.AvgLatency, 2)).ToList(), type = "line", name = "Latencia (ms)" } },
                tooltip = new { trigger = "axis" }
            };

            var httpMethodsData = requestLogsData
                .GroupBy(r => r.HttpMethod)
                .Select(g => new { name = g.Key, value = g.Count() })
                .OrderByDescending(d => d.value)
                .ToList();

            var httpMethodsChart = new
            {
                tooltip = new { trigger = "item" },
                legend = new { top = "5%", left = "center" },
                series = new[]
                {
                    new {
                        name = "Métodos HTTP",
                        type = "pie",
                        radius = new[] { "40%", "70%" },
                        avoidLabelOverlap = false,
                        label = new { show = false, position = "center" },
                        emphasis = new { label = new { show = true, fontSize = 20, fontWeight = "bold" } },
                        labelLine = new { show = false },
                        data = httpMethodsData
                    }
                }
            };

            var slowestEndpointsData = requestLogsData
                .GroupBy(r => r.RequestPath)
                .Select(g => new { Path = g.Key, AvgDuration = g.Average(r => r.DurationMs) })
                .OrderByDescending(x => x.AvgDuration)
                .Take(5)
                .ToList();

            var slowestEndpointsChart = new
            {
                xAxis = new { type = "category", data = slowestEndpointsData.Select(d => d.Path).ToList(), axisLabel = new { interval = 0, rotate = 20 } },
                yAxis = new { type = "value", name = "ms" },
                series = new[] { new { data = slowestEndpointsData.Select(d => Math.Round(d.AvgDuration, 2)).ToList(), type = "bar" } },
                tooltip = new { trigger = "axis" }
            };

            var trafficByGroupData = requestLogsData
                .Where(r => r.EndpointGroupAccessed != null)
                .GroupBy(r => r.EndpointGroupAccessed)
                .Select(g => new { Group = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            var trafficByGroupChart = new
            {
                xAxis = new { type = "value" },
                yAxis = new { type = "category", data = trafficByGroupData.Select(d => d.Group).Reverse().ToList() },
                series = new[] { new { data = trafficByGroupData.Select(d => d.Count).Reverse().ToList(), type = "bar" } },
                tooltip = new { trigger = "axis" }
            };

            var viewModel = new AnalyticsViewModel
            {
                HourlyTrafficChartJson = AnalyticsViewModel.SerializeEChartData(hourlyTrafficChart),
                ErrorsChartJson = AnalyticsViewModel.SerializeEChartData(errorsChart),
                LatencyChartJson = AnalyticsViewModel.SerializeEChartData(latencyChart),
                HttpMethodsChartJson = AnalyticsViewModel.SerializeEChartData(httpMethodsChart),
                SlowestEndpointsChartJson = AnalyticsViewModel.SerializeEChartData(slowestEndpointsChart),
                TrafficByGroupChartJson = AnalyticsViewModel.SerializeEChartData(trafficByGroupChart)
            };

            return View(viewModel);
        }
    }
}
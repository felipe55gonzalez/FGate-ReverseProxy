using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Areas.Admin.Models;
using FGate.Data.Entities;
using FGate.Services;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class HomeController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ICacheManagementService _cacheService;

        public HomeController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ICacheManagementService cacheService)
        {
            _dbContextFactory = dbContextFactory;
            _cacheService = cacheService;
        }

        public async Task<IActionResult> Index()
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);

            var hourlyData = await dbContext.HourlyTrafficSummaries
                .Where(s => s.HourUtc >= twentyFourHoursAgo)
                .OrderBy(s => s.HourUtc)
                .GroupBy(s => s.HourUtc)
                .Select(g => new
                {
                    Hour = g.Key,
                    TotalRequests = g.Sum(s => s.RequestCount ?? 0)
                })
                .ToListAsync();

            var mainTrafficChart = new
            {
                xAxis = new { type = "category", data = hourlyData.Select(d => d.Hour.ToLocalTime().ToString("HH:00")).ToList() },
                yAxis = new { type = "value" },
                series = new[] { new {
                    data = hourlyData.Select(d => d.TotalRequests).ToList(),
                    type = "line",
                    smooth = true,
                    areaStyle = new { }       
                }},
                tooltip = new { trigger = "axis" }
            };

            var viewModel = new DashboardViewModel
            {
                EndpointGroupCount = await dbContext.EndpointGroups.CountAsync(),
                ApiTokenCount = await dbContext.ApiTokens.CountAsync(),
                BlockedIpCount = await dbContext.BlockedIps.CountAsync(),
                TotalRequests24h = hourlyData.Sum(d => d.TotalRequests),
                LastCacheRefreshMessage = TempData["CacheRefreshMessage"] as string,
                MainTrafficChartJson = AnalyticsViewModel.SerializeEChartData(mainTrafficChart)
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshCache()
        {
            await _cacheService.RefreshAllAsync();
            TempData["ToastMessage"] = $"Caché refrescada exitosamente a las {DateTime.Now:HH:mm:ss}.";
            return RedirectToAction("Index");
        }
    }
}
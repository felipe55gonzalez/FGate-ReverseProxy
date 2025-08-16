using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class AlertsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;

        public AlertsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IActionResult> Index()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var alerts = await context.SystemAlerts
                .OrderByDescending(a => a.TimestampUtc)
                .Take(100)          
                .ToListAsync();

            return View(alerts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var alert = await context.SystemAlerts.FindAsync(id);
            if (alert != null && !alert.IsRead)
            {
                alert.IsRead = true;
                await context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            await context.SystemAlerts
                .Where(a => !a.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsRead, true));

            return RedirectToAction(nameof(Index));
        }
    }
}
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
    public class BlockedIpsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly IAuditLogger _auditLogger;
        private readonly INotificationService _notificationService;

        public BlockedIpsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, IAuditLogger auditLogger, INotificationService notificationService)
        {
            _dbContextFactory = dbContextFactory;
            _auditLogger = auditLogger;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(string searchString)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.BlockedIps.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(b => b.IpAddress.Contains(searchString) || b.Reason.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            var blockedIps = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
            return View(blockedIps);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlockedIpViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();

                var existingIp = await context.BlockedIps.FirstOrDefaultAsync(b => b.IpAddress == viewModel.IpAddress);
                if (existingIp != null)
                {
                    ModelState.AddModelError("IpAddress", "Esta dirección IP ya se encuentra bloqueada.");
                    return View(viewModel);
                }

                var newBlockedIp = new BlockedIp
                {
                    IpAddress = viewModel.IpAddress,
                    Reason = viewModel.Reason,
                    BlockedUntil = viewModel.BlockedUntil,
                    CreatedAt = DateTime.UtcNow
                };

                context.BlockedIps.Add(newBlockedIp);
                await context.SaveChangesAsync();

                await _auditLogger.LogEventAsync(
                    entityType: "BlockedIp",
                    entityId: newBlockedIp.BlockedIpId.ToString(),
                    action: "Create",
                    userId: User.Identity?.Name,
                    newValues: new { newBlockedIp.IpAddress, newBlockedIp.Reason, newBlockedIp.BlockedUntil }
                );

                await _notificationService.TriggerAlertAsync(
                    level: AlertLevel.Critical,
                    title: $"IP Bloqueada Manualmente: {newBlockedIp.IpAddress}",
                    details: new { newBlockedIp.IpAddress, newBlockedIp.Reason, User = User.Identity?.Name }
                );

                return RedirectToAction(nameof(Index));
            }
            return View(viewModel);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var blockedIp = await context.BlockedIps.FirstOrDefaultAsync(m => m.BlockedIpId == id);
            if (blockedIp == null) return NotFound();

            return View(blockedIp);
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var blockedIp = await context.BlockedIps.FindAsync(id);
            if (blockedIp != null)
            {
                await _auditLogger.LogEventAsync(
                    entityType: "BlockedIp",
                    entityId: id.ToString(),
                    action: "Delete",       
                    userId: User.Identity?.Name,
                    oldValues: new { blockedIp.IpAddress, blockedIp.Reason }
                );

                context.BlockedIps.Remove(blockedIp);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Dirección IP desbloqueada exitosamente.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;
using FGate.Services;
using System.Threading.Tasks;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class BackendDestinationsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly IAuditLogger _auditLogger;

        public BackendDestinationsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, IAuditLogger auditLogger)
        {
            _dbContextFactory = dbContextFactory;
            _auditLogger = auditLogger;
        }

        public async Task<IActionResult> Index(string searchString)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.BackendDestinations.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(d => d.FriendlyName.Contains(searchString) || d.Address.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            var destinations = await query.OrderBy(d => d.FriendlyName).ToListAsync();
            return View(destinations);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Address,FriendlyName,IsEnabled,HealthCheckPath")] BackendDestination backendDestination)
        {
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                context.Add(backendDestination);
                await context.SaveChangesAsync();

                await _auditLogger.LogEventAsync(
                    entityType: "BackendDestination",
                    entityId: backendDestination.DestinationId.ToString(),
                    action: "Create",
                    userId: User.Identity?.Name,
                    newValues: new { backendDestination.FriendlyName, backendDestination.Address, backendDestination.IsEnabled }
                );

                return RedirectToAction(nameof(Index));
            }
            return View(backendDestination);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var backendDestination = await context.BackendDestinations.FindAsync(id);
            if (backendDestination == null) return NotFound();
            
            return View(backendDestination);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DestinationId,Address,FriendlyName,IsEnabled,HealthCheckPath,CreatedAt")] BackendDestination backendDestination)
        {
            if (id != backendDestination.DestinationId) return NotFound();

            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var oldDestination = await context.BackendDestinations.AsNoTracking().FirstOrDefaultAsync(d => d.DestinationId == id);
                if (oldDestination == null) return NotFound();

                var oldValues = new { oldDestination.FriendlyName, oldDestination.Address, oldDestination.IsEnabled, oldDestination.HealthCheckPath };
                var newValues = new { backendDestination.FriendlyName, backendDestination.Address, backendDestination.IsEnabled, backendDestination.HealthCheckPath };

                try
                {
                    backendDestination.UpdatedAt = DateTime.UtcNow;
                    context.Update(backendDestination);
                    await context.SaveChangesAsync();

                    await _auditLogger.LogEventAsync(
                        entityType: "BackendDestination",
                        entityId: id.ToString(),
                        action: "Edit",
                        userId: User.Identity?.Name,
                        oldValues: oldValues,
                        newValues: newValues
                    );
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Log error, check if exists, etc.
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(backendDestination);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var backendDestination = await context.BackendDestinations.FirstOrDefaultAsync(m => m.DestinationId == id);
            if (backendDestination == null) return NotFound();

            return View(backendDestination);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var backendDestination = await context.BackendDestinations.FindAsync(id);
            if (backendDestination != null)
            {
                await _auditLogger.LogEventAsync(
                    entityType: "BackendDestination",
                    entityId: id.ToString(),
                    action: "Delete",
                    userId: User.Identity?.Name,
                    oldValues: new { backendDestination.FriendlyName, backendDestination.Address }
                );

                context.BackendDestinations.Remove(backendDestination);
                await context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
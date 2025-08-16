using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;
using FGate.Services;
using System;
using System.Threading.Tasks;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class AllowedCorsOriginsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly IAuditLogger _auditLogger;

        public AllowedCorsOriginsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, IAuditLogger auditLogger)
        {
            _dbContextFactory = dbContextFactory;
            _auditLogger = auditLogger;
        }

        public async Task<IActionResult> Index()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var origins = await context.AllowedCorsOrigins.OrderBy(o => o.OriginUrl).ToListAsync();
            return View(origins);
        }

        public IActionResult Create()
        {
            var model = new AllowedCorsOrigin { IsEnabled = true };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("OriginUrl,Description,IsEnabled")] AllowedCorsOrigin allowedCorsOrigin)
        {
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                allowedCorsOrigin.CreatedAt = DateTime.UtcNow;
                allowedCorsOrigin.UpdatedAt = DateTime.UtcNow;

                context.Add(allowedCorsOrigin);
                await context.SaveChangesAsync();

                await _auditLogger.LogEventAsync("AllowedCorsOrigin", allowedCorsOrigin.OriginId.ToString(), "Create", User.Identity.Name, newValues: allowedCorsOrigin);
                TempData["ToastMessage"] = "Origen CORS creado exitosamente.";

                return RedirectToAction(nameof(Index));
            }
            return View(allowedCorsOrigin);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var allowedCorsOrigin = await context.AllowedCorsOrigins.FindAsync(id);
            if (allowedCorsOrigin == null) return NotFound();

            return View(allowedCorsOrigin);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OriginId,OriginUrl,Description,IsEnabled,CreatedAt")] AllowedCorsOrigin allowedCorsOrigin)
        {
            if (id != allowedCorsOrigin.OriginId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    await using var context = await _dbContextFactory.CreateDbContextAsync();
                    var originalOrigin = await context.AllowedCorsOrigins.AsNoTracking().FirstOrDefaultAsync(o => o.OriginId == id);

                    allowedCorsOrigin.UpdatedAt = DateTime.UtcNow;
                    context.Update(allowedCorsOrigin);
                    await context.SaveChangesAsync();

                    await _auditLogger.LogEventAsync("AllowedCorsOrigin", id.ToString(), "Edit", User.Identity.Name, oldValues: originalOrigin, newValues: allowedCorsOrigin);
                    TempData["ToastMessage"] = "Origen CORS actualizado exitosamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(allowedCorsOrigin);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var allowedCorsOrigin = await context.AllowedCorsOrigins.FindAsync(id);
            if (allowedCorsOrigin != null)
            {
                await _auditLogger.LogEventAsync("AllowedCorsOrigin", id.ToString(), "Delete", User.Identity.Name, oldValues: allowedCorsOrigin);
                context.AllowedCorsOrigins.Remove(allowedCorsOrigin);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Origen CORS eliminado exitosamente.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
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
    public class SettingsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly IAuditLogger _auditLogger;

        public SettingsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, IAuditLogger auditLogger)
        {
            _dbContextFactory = dbContextFactory;
            _auditLogger = auditLogger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var retentionSetting = await context.ProxyConfigurations
                .FirstOrDefaultAsync(c => c.ConfigurationKey == "LogRetentionDays");

            var model = new SettingsViewModel();
            if (retentionSetting != null && int.TryParse(retentionSetting.ConfigurationValue, out int days))
            {
                model.LogRetentionDays = days;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var retentionSetting = await context.ProxyConfigurations
                .FirstOrDefaultAsync(c => c.ConfigurationKey == "LogRetentionDays");

            string oldValue = retentionSetting?.ConfigurationValue ?? "N/A";

            if (retentionSetting == null)
            {
                // Si no existe, lo creamos
                retentionSetting = new ProxyConfiguration
                {
                    ConfigurationKey = "LogRetentionDays",
                    ConfigurationValue = model.LogRetentionDays.ToString()
                };
                context.ProxyConfigurations.Add(retentionSetting);
            }
            else
            {
                // Si ya existe, lo actualizamos
                retentionSetting.ConfigurationValue = model.LogRetentionDays.ToString();
                retentionSetting.UpdatedAt = DateTime.UtcNow;
                context.ProxyConfigurations.Update(retentionSetting);
            }

            await context.SaveChangesAsync();

            await _auditLogger.LogEventAsync(
                entityType: "SystemSettings",
                entityId: "LogRetentionDays",
                action: "Edit",
                userId: User.Identity?.Name,
                oldValues: new { Days = oldValue },
                newValues: new { Days = model.LogRetentionDays }
            );

            TempData["ToastMessage"] = "Configuración guardada exitosamente.";

            return View(model);
        }
    }
}
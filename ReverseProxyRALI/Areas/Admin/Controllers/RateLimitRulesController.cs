using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class RateLimitRulesController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;

        public RateLimitRulesController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IActionResult> Index()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var rules = await context.RateLimitRules.OrderBy(r => r.RuleName).ToListAsync();
            return View(rules);
        }

        public IActionResult Create() => View(new RateLimitRule { PeriodSeconds = 60, RequestLimit = 100 });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RateLimitRule rule)
        {
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                context.Add(rule);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Regla creada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(rule);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var rule = await context.RateLimitRules.FindAsync(id);
            if (rule == null) return NotFound();
            return View(rule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RateLimitRule rule)
        {
            if (id != rule.RuleId) return NotFound();
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                context.Update(rule);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Regla actualizada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(rule);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var rule = await context.RateLimitRules.FindAsync(id);
            if (rule != null)
            {
                context.RateLimitRules.Remove(rule);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Regla eliminada exitosamente.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
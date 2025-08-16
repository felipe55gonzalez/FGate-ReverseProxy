using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class WafRulesController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;

        public WafRulesController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IActionResult> Index()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var rules = await context.WafRules.OrderBy(r => r.RuleName).ToListAsync();
            return View(rules);
        }

        public IActionResult Create() => View(new WafRule());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WafRule rule)
        {
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                context.Add(rule);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Regla WAF creada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(rule);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var rule = await context.WafRules.FindAsync(id);
            if (rule == null) return NotFound();
            return View(rule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WafRule rule)
        {
            if (id != rule.RuleId) return NotFound();
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                context.Update(rule);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Regla WAF actualizada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(rule);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var rule = await context.WafRules.FindAsync(id);
            if (rule != null)
            {
                context.WafRules.Remove(rule);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Regla WAF eliminada exitosamente.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
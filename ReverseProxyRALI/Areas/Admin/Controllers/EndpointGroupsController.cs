using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FGate.Areas.Admin.Models;
using FGate.Data.Entities;
using FGate.Services;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class EndpointGroupsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly IAuditLogger _auditLogger;

        public EndpointGroupsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, IAuditLogger auditLogger)
        {
            _dbContextFactory = dbContextFactory;
            _auditLogger = auditLogger;
        }

        public async Task<IActionResult> Index(string searchString)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.EndpointGroups
                .Include(g => g.EndpointGroupDestinations)
                .ThenInclude(gd => gd.Destination)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(g => g.GroupName.Contains(searchString) || g.PathPattern.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            var groups = await query.OrderBy(g => g.MatchOrder).ToListAsync();
            return View(groups);
        }

        public async Task<IActionResult> Create()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var viewModel = new EndpointGroupViewModel
            {
                Destinations = await context.BackendDestinations.Select(d => new BackendDestinationViewModel
                {
                    DestinationId = d.DestinationId,
                    Address = d.Address,
                    FriendlyName = d.FriendlyName,
                    IsAssigned = false
                }).ToListAsync()
            };
            await PopulateRateLimitRulesDropDownList();
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EndpointGroupViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var newGroup = new EndpointGroup
                {
                    GroupName = viewModel.GroupName,
                    Description = viewModel.Description,
                    PathPattern = viewModel.PathPattern,
                    MatchOrder = viewModel.MatchOrder,
                    ReqToken = viewModel.ReqToken,
                    IsInMaintenanceMode = viewModel.IsInMaintenanceMode,
                    RateLimitRuleId = viewModel.RateLimitRuleId
                };

                foreach (var dest in viewModel.Destinations.Where(d => d.IsAssigned))
                {
                    newGroup.EndpointGroupDestinations.Add(new EndpointGroupDestination { DestinationId = dest.DestinationId });
                }

                context.EndpointGroups.Add(newGroup);
                await context.SaveChangesAsync();

                await _auditLogger.LogEventAsync("EndpointGroup", newGroup.GroupId.ToString(), "Create", User.Identity.Name, newValues: newGroup);

                return RedirectToAction(nameof(Index));
            }
            await PopulateRateLimitRulesDropDownList(viewModel.RateLimitRuleId);
            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var group = await context.EndpointGroups
                .Include(g => g.EndpointGroupDestinations)
                .Include(g => g.EndpointGroupWafRules)     
                .FirstOrDefaultAsync(g => g.GroupId == id);

            if (group == null) return NotFound();

            var allDestinations = await context.BackendDestinations.ToListAsync();
            var allWafRules = await context.WafRules.Where(r => r.IsEnabled).ToListAsync();

            var viewModel = new EndpointGroupViewModel
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                Description = group.Description,
                PathPattern = group.PathPattern,
                MatchOrder = group.MatchOrder,
                ReqToken = group.ReqToken,
                IsInMaintenanceMode = group.IsInMaintenanceMode,
                RateLimitRuleId = group.RateLimitRuleId,
                Destinations = allDestinations.Select(d => new BackendDestinationViewModel
                {
                    DestinationId = d.DestinationId,
                    Address = d.Address,
                    FriendlyName = d.FriendlyName,
                    IsAssigned = group.EndpointGroupDestinations.Any(gd => gd.DestinationId == d.DestinationId)
                }).ToList(),
                WafRuleAssignments = allWafRules.Select(rule => new WafRuleAssignmentViewModel
                {
                    RuleId = rule.RuleId,
                    RuleName = rule.RuleName,
                    Description = rule.Description,
                    IsAssigned = group.EndpointGroupWafRules.Any(gr => gr.WafRuleId == rule.RuleId)
                }).ToList()
            };

            await PopulateRateLimitRulesDropDownList(group.RateLimitRuleId);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EndpointGroupViewModel viewModel)
        {
            if (id != viewModel.GroupId) return NotFound();

            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var groupToUpdate = await context.EndpointGroups
                    .Include(g => g.EndpointGroupWafRules)       
                    .FirstOrDefaultAsync(g => g.GroupId == id);

                if (groupToUpdate == null) return NotFound();

                groupToUpdate.GroupName = viewModel.GroupName;
                groupToUpdate.Description = viewModel.Description;
                groupToUpdate.PathPattern = viewModel.PathPattern;
                groupToUpdate.MatchOrder = viewModel.MatchOrder;
                groupToUpdate.ReqToken = viewModel.ReqToken;
                groupToUpdate.IsInMaintenanceMode = viewModel.IsInMaintenanceMode;
                groupToUpdate.RateLimitRuleId = viewModel.RateLimitRuleId;
                groupToUpdate.UpdatedAt = DateTime.UtcNow;

                groupToUpdate.EndpointGroupWafRules.Clear();
                if (viewModel.WafRuleAssignments != null)
                {
                    foreach (var assignment in viewModel.WafRuleAssignments.Where(a => a.IsAssigned))
                    {
                        groupToUpdate.EndpointGroupWafRules.Add(new EndpointGroupWafRule
                        {
                            WafRuleId = assignment.RuleId
                        });
                    }
                }

                await context.SaveChangesAsync();

                await _auditLogger.LogEventAsync("EndpointGroup", id.ToString(), "Edit", User.Identity.Name);
                TempData["ToastMessage"] = "Grupo de Endpoints actualizado exitosamente.";

                return RedirectToAction(nameof(Index));
            }

            await PopulateRateLimitRulesDropDownList(viewModel.RateLimitRuleId);
            return View(viewModel);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var group = await context.EndpointGroups.FirstOrDefaultAsync(m => m.GroupId == id);
            if (group == null) return NotFound();

            return View(group);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var group = await context.EndpointGroups.FindAsync(id);
            if (group != null)
            {
                await _auditLogger.LogEventAsync(
                    entityType: "EndpointGroup",
                    entityId: group.GroupId.ToString(),
                    action: "Delete",
                    userId: User.Identity?.Name,
                    oldValues: new { group.GroupName, group.PathPattern }
                );

                context.EndpointGroups.Remove(group);
                await context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateRateLimitRulesDropDownList(object? selectedRule = null)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var rules = await context.RateLimitRules.OrderBy(r => r.RuleName).ToListAsync();
            ViewBag.RateLimitRuleId = new SelectList(rules, "RuleId", "RuleName", selectedRule);
        }
    }
}
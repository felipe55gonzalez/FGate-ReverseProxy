using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Areas.Admin.Models;
using FGate.Data.Entities;
using FGate.Services;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class ApiTokensController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly IAuditLogger _auditLogger;

        public ApiTokensController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, IAuditLogger auditLogger)
        {
            _dbContextFactory = dbContextFactory;
            _auditLogger = auditLogger;
        }

        public async Task<IActionResult> Index(string searchString)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.ApiTokens.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(t => t.Description.Contains(searchString) || t.OwnerName.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            var tokens = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
            return View(tokens);
        }

        public async Task<IActionResult> Create()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var viewModel = new ApiTokenViewModel
            {
                IsEnabled = true,
                Permissions = await context.EndpointGroups.Select(g => new TokenPermissionViewModel
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    IsAssigned = false
                }).ToListAsync()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ApiTokenViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var newToken = new ApiToken
                {
                    TokenValue = "TOKEN_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                    Description = viewModel.Description,
                    OwnerName = viewModel.OwnerName,
                    IsEnabled = viewModel.IsEnabled,
                    DoesExpire = viewModel.DoesExpire,
                    ExpiresAt = viewModel.DoesExpire ? viewModel.ExpiresAt : null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                foreach (var perm in viewModel.Permissions.Where(p => p.IsAssigned))
                {
                    newToken.TokenPermissions.Add(new TokenPermission
                    {
                        GroupId = perm.GroupId,
                        AllowedHttpMethods = perm.AllowedHttpMethods
                    });
                }

                context.ApiTokens.Add(newToken);
                await context.SaveChangesAsync();

                await _auditLogger.LogEventAsync(
                    entityType: "ApiToken",
                    entityId: newToken.TokenId.ToString(),
                    action: "Create",
                    userId: User.Identity?.Name,
                    newValues: new { newToken.Description, newToken.OwnerName, newToken.IsEnabled, newToken.DoesExpire }
                );

                TempData["NewTokenDescription"] = newToken.Description;
                TempData["NewTokenValue"] = newToken.TokenValue;
                TempData["ToastMessage"] = "Token creado exitosamente.";

                return RedirectToAction(nameof(Index));
            }
            
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            viewModel.Permissions = await dbContext.EndpointGroups.Select(g => new TokenPermissionViewModel
            {
                GroupId = g.GroupId,
                GroupName = g.GroupName,
                IsAssigned = false
            }).ToListAsync();

            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var apiToken = await context.ApiTokens.Include(t => t.TokenPermissions).FirstOrDefaultAsync(t => t.TokenId == id);
            if (apiToken == null) return NotFound();

            var allGroups = await context.EndpointGroups.ToListAsync();
            var viewModel = new ApiTokenViewModel
            {
                TokenId = apiToken.TokenId,
                TokenValue = apiToken.TokenValue,
                Description = apiToken.Description,
                OwnerName = apiToken.OwnerName,
                IsEnabled = apiToken.IsEnabled,
                DoesExpire = apiToken.DoesExpire,
                ExpiresAt = apiToken.ExpiresAt,
                Permissions = allGroups.Select(g => new TokenPermissionViewModel
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    IsAssigned = apiToken.TokenPermissions.Any(p => p.GroupId == g.GroupId),
                    AllowedHttpMethods = apiToken.TokenPermissions.FirstOrDefault(p => p.GroupId == g.GroupId)?.AllowedHttpMethods ?? "GET,POST,PUT,DELETE"
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ApiTokenViewModel viewModel)
        {
            if (id != viewModel.TokenId) return NotFound();

            if (ModelState.IsValid)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var tokenToUpdate = await context.ApiTokens.Include(t => t.TokenPermissions).FirstOrDefaultAsync(t => t.TokenId == id);
                if (tokenToUpdate == null) return NotFound();

                var oldValues = new { tokenToUpdate.Description, tokenToUpdate.OwnerName, tokenToUpdate.IsEnabled, tokenToUpdate.DoesExpire };
                var newValues = new { viewModel.Description, viewModel.OwnerName, viewModel.IsEnabled, viewModel.DoesExpire };

                tokenToUpdate.Description = viewModel.Description;
                tokenToUpdate.OwnerName = viewModel.OwnerName;
                tokenToUpdate.IsEnabled = viewModel.IsEnabled;
                tokenToUpdate.DoesExpire = viewModel.DoesExpire;
                tokenToUpdate.ExpiresAt = viewModel.DoesExpire ? viewModel.ExpiresAt : null;
                tokenToUpdate.UpdatedAt = DateTime.UtcNow;

                tokenToUpdate.TokenPermissions.Clear();
                foreach (var perm in viewModel.Permissions.Where(p => p.IsAssigned))
                {
                    tokenToUpdate.TokenPermissions.Add(new TokenPermission
                    {
                        GroupId = perm.GroupId,
                        AllowedHttpMethods = perm.AllowedHttpMethods
                    });
                }
                
                await context.SaveChangesAsync();

                await _auditLogger.LogEventAsync(
                    entityType: "ApiToken",
                    entityId: id.ToString(),
                    action: "Edit",
                    userId: User.Identity?.Name,
                    oldValues: oldValues,
                    newValues: newValues
                );

                TempData["ToastMessage"] = "Token actualizado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(viewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var apiToken = await context.ApiTokens.FindAsync(id);
            if (apiToken != null)
            {
                await _auditLogger.LogEventAsync(
                    entityType: "ApiToken",
                    entityId: id.ToString(),
                    action: "Delete",
                    userId: User.Identity?.Name,
                    oldValues: new { apiToken.Description, apiToken.OwnerName }
                );

                context.ApiTokens.Remove(apiToken);
                await context.SaveChangesAsync();
                TempData["ToastMessage"] = "Token eliminado exitosamente.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
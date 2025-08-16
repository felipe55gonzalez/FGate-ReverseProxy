using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class RequestLogsController : Controller
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;

        public RequestLogsController(IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IActionResult> Index(string? searchQuery, string? httpMethod, string? statusCodeFamily, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            const int pageSize = 20;
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var query = context.RequestLogs.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(r => r.RequestPath.Contains(searchQuery) ||
                                         r.HttpMethod.Contains(searchQuery) ||
                                         r.ClientIpAddress.Contains(searchQuery) ||
                                         r.ResponseStatusCode.ToString().Contains(searchQuery));
            }

            if (!string.IsNullOrEmpty(httpMethod))
            {
                query = query.Where(r => r.HttpMethod == httpMethod);
            }

            if (!string.IsNullOrEmpty(statusCodeFamily))
            {
                switch (statusCodeFamily)
                {
                    case "2xx":
                        query = query.Where(r => r.ResponseStatusCode >= 200 && r.ResponseStatusCode < 300);
                        break;
                    case "4xx":
                        query = query.Where(r => r.ResponseStatusCode >= 400 && r.ResponseStatusCode < 500);
                        break;
                    case "5xx":
                        query = query.Where(r => r.ResponseStatusCode >= 500);
                        break;
                }
            }

            if (startDate.HasValue)
            {
                query = query.Where(r => r.TimestampUtc >= startDate.Value.ToUniversalTime());
            }
            if (endDate.HasValue)
            {
                query = query.Where(r => r.TimestampUtc < endDate.Value.AddDays(1).ToUniversalTime());
            }

            var totalItems = await query.CountAsync();
            var logs = await query.OrderByDescending(r => r.TimestampUtc)
                                  .Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToListAsync();

            ViewData["TotalPages"] = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewData["CurrentPage"] = page;
            ViewData["SearchQuery"] = searchQuery;
            ViewData["CurrentHttpMethod"] = httpMethod;
            ViewData["CurrentStatusCodeFamily"] = statusCodeFamily;
            ViewData["CurrentStartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["CurrentEndDate"] = endDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }
    }
}
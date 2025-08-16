using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;

namespace FGate.ViewComponents
{
    public class UnreadAlertsViewComponent : ViewComponent
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;

        public UnreadAlertsViewComponent(IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var unreadCount = await context.SystemAlerts
                    .CountAsync(a => !a.IsRead);

                return View(unreadCount);         
            }
            return Content(string.Empty);        
        }
    }
}
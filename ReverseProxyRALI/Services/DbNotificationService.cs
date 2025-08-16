using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;
using System.Text.Json;

namespace FGate.Services
{
    public class DbNotificationService : INotificationService
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbNotificationService> _logger;

        public DbNotificationService(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<DbNotificationService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task TriggerAlertAsync(AlertLevel level, string title, object? details = null)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();

                var newAlert = new SystemAlert
                {
                    Level = level.ToString(),
                    Title = title,
                    Details = details != null ? JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = false }) : null,
                    IsRead = false,
                    TimestampUtc = DateTime.UtcNow
                };

                context.SystemAlerts.Add(newAlert);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo guardar la alerta en la base de datos. Título: {Title}", title);
            }
        }
    }
}
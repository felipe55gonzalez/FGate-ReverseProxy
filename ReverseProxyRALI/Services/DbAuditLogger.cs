using Microsoft.EntityFrameworkCore;
using FGate.Data;   
using FGate.Data.Entities;   
using System.Text.Json;     

namespace FGate.Services
{
    public class DbAuditLogger : IAuditLogger
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbAuditLogger> _logger;

        public DbAuditLogger(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<DbAuditLogger> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task LogEventAsync(
            string entityType,
            string entityId,
            string action,
            string? userId = null,
            string? affectedComponent = null,
            object? oldValues = null,
            object? newValues = null,
            string? clientIpAddress = null)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                var auditLogEntry = new AuditLog
                {
                    TimestampUtc = DateTime.Now,
                    UserId = userId,
                    EntityType = entityType,
                    EntityId = entityId,
                    Action = action,
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, new JsonSerializerOptions { WriteIndented = false }) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues, new JsonSerializerOptions { WriteIndented = false }) : null,
                    AffectedComponent = affectedComponent,
                    IpAddress = clientIpAddress
                };

                dbContext.AuditLogs.Add(auditLogEntry);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;

namespace FGate.Services
{
    public class LogCleanupService : IHostedService, IDisposable
    {
        private readonly ILogger<LogCleanupService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer? _timer;

        public LogCleanupService(IServiceScopeFactory scopeFactory, ILogger<LogCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Servicio de Limpieza de Logs Iniciado.");

            var runTime = new TimeSpan(3, 0, 0);
            var timeUntilFirstRun = runTime - DateTime.Now.TimeOfDay;
            if (timeUntilFirstRun < TimeSpan.Zero)
            {
                timeUntilFirstRun = timeUntilFirstRun.Add(TimeSpan.FromDays(1));
            }

            _timer = new Timer(DoWork, null, timeUntilFirstRun, TimeSpan.FromDays(1));

            _logger.LogInformation("Próxima limpieza de logs programada para dentro de {Time}", timeUntilFirstRun);

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("Ejecutando tarea de limpieza de logs...");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ProxyRaliDbContext>();

                int retentionDays = 30;    
                var setting = await dbContext.ProxyConfigurations
                    .AsNoTracking()      
                    .FirstOrDefaultAsync(c => c.ConfigurationKey == "LogRetentionDays");

                if (setting != null && int.TryParse(setting.ConfigurationValue, out int daysFromDb))
                {
                    retentionDays = daysFromDb;
                }

                _logger.LogInformation("Política de retención de logs establecida en {Days} días.", retentionDays);
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                _logger.LogInformation("Eliminando logs anteriores a la fecha: {CutoffDate}", cutoffDate);

                var requestLogsDeleted = await dbContext.RequestLogs
                    .Where(log => log.TimestampUtc < cutoffDate)
                    .ExecuteDeleteAsync();

                _logger.LogInformation("{Count} registros eliminados de RequestLogs.", requestLogsDeleted);

                var auditLogsDeleted = await dbContext.AuditLogs
                    .Where(log => log.TimestampUtc < cutoffDate)
                    .ExecuteDeleteAsync();

                _logger.LogInformation("{Count} registros eliminados de AuditLogs.", auditLogsDeleted);

                _logger.LogInformation("Tarea de limpieza de logs completada exitosamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error durante la tarea de limpieza de logs.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Servicio de Limpieza de Logs Detenido.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FGate.Data;
using FGate.Data.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FGate.Services
{
    public class HourlyTrafficAggregatorService : IHostedService, IDisposable
    {
        private readonly ILogger<HourlyTrafficAggregatorService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer? _timer;
        private readonly TimeSpan _aggregationInterval = TimeSpan.FromHours(1);    
        public HourlyTrafficAggregatorService(
            ILogger<HourlyTrafficAggregatorService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var nextRunTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Local).AddMinutes(1);
            var dueTime = nextRunTime - now;

            if (dueTime <= TimeSpan.Zero)
            {
                dueTime = _aggregationInterval;
                nextRunTime = now.Add(_aggregationInterval);      
            }


            _timer = new Timer(DoWork, null, dueTime, _aggregationInterval);

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("DoWork ejecutando trabajo a las: {UtcNow} UTC", DateTime.UtcNow);

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ProxyRaliDbContext>();
                try
                {
                    var now = DateTime.UtcNow;
                    var processingHourEnd = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
                    var processingHourStart = processingHourEnd.AddHours(-1);

                    _logger.LogInformation("Procesando logs para el intervalo: {Start} UTC a {End} UTC", processingHourStart, processingHourEnd);

                    var alreadyProcessed = await dbContext.HourlyTrafficSummaries
                                                .AnyAsync(s => s.HourUtc == processingHourStart);

                    if (alreadyProcessed && !IsDevelopmentEnvironment())
                    {
                        _logger.LogInformation("El intervalo {Start} ya ha sido procesado. Saltando.", processingHourStart);
                        return;
                    }
                    if (alreadyProcessed && IsDevelopmentEnvironment())
                    {
                        _logger.LogInformation("Modo desarrollo: Eliminando datos antiguos para {Start} antes de reprocesar.", processingHourStart);
                        await dbContext.HourlyTrafficSummaries.Where(s => s.HourUtc == processingHourStart).ExecuteDeleteAsync();
                    }

                    var rawLogs = await dbContext.RequestLogs
                        .Where(rl => rl.TimestampUtc >= processingHourStart && rl.TimestampUtc < processingHourEnd && rl.EndpointGroupAccessed != null)
                        .Select(rl => new
                        {
                            rl.EndpointGroupAccessed,
                            rl.HttpMethod,
                            rl.DurationMs,
                            rl.RequestSizeBytes,
                            rl.ResponseSizeBytes,
                            rl.ClientIpAddress
                        })
                        .ToListAsync();

                    if (!rawLogs.Any())
                    {
                        _logger.LogInformation("No hay datos para agregar en HourlyTrafficSummary para la hora {Start}.", processingHourStart);
                        return;
                    }

                    var groupedLogs = rawLogs
                        .GroupBy(rl => new { rl.EndpointGroupAccessed, rl.HttpMethod });

                    var newSummaries = new List<HourlyTrafficSummary>();
                    var groupNameToIdMap = await dbContext.EndpointGroups.ToDictionaryAsync(g => g.GroupName, g => g.GroupId);

                    foreach (var group in groupedLogs)
                    {
                        if (!groupNameToIdMap.TryGetValue(group.Key.EndpointGroupAccessed, out var groupId))
                        {
                            _logger.LogWarning("No se encontró GroupId para el GroupName '{GroupName}'. Saltando agregado.", group.Key.EndpointGroupAccessed);
                            continue;
                        }

                        var durations = group.Select(rl => rl.DurationMs).OrderBy(d => d).ToList();
                        var p95Index = durations.Any() ? (int)Math.Ceiling(0.95 * durations.Count) - 1 : -1;

                        var summary = new HourlyTrafficSummary
                        {
                            HourUtc = processingHourStart,
                            EndpointGroupId = groupId,
                            HttpMethod = group.Key.HttpMethod,
                            RequestCount = group.Count(),
                            ErrorCount4xx = 0,               
                            ErrorCount5xx = 0,           
                            AverageDurationMs = (decimal?)group.Average(rl => rl.DurationMs),
                            P95durationMs = p95Index >= 0 ? durations[p95Index] : (decimal?)null,
                            TotalRequestBytes = group.Sum(rl => rl.RequestSizeBytes),
                            TotalResponseBytes = group.Sum(rl => rl.ResponseSizeBytes),
                            UniqueClientIps = group.Select(rl => rl.ClientIpAddress).Distinct().Count()
                        };
                        newSummaries.Add(summary);
                    }
                    if (newSummaries.Any())
                    {
                        dbContext.HourlyTrafficSummaries.AddRange(newSummaries);
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Agregados {Count} registros a HourlyTrafficSummary para la hora {Start}.", newSummaries.Count, processingHourStart);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante la ejecución de DoWork en HourlyTrafficAggregatorService.");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
        }

        private bool IsDevelopmentEnvironment()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            bool isDev = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
            return isDev;
        }
    }
}
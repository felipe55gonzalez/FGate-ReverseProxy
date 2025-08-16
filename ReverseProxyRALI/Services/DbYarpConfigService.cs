using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;

namespace FGate.Services
{
    public class DbYarpConfigService : IYarpConfigService
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbYarpConfigService> _logger;

        public DbYarpConfigService(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<DbYarpConfigService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<(IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters)> GetConfigAsync()
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();

            clusters.Add(new ClusterConfig { ClusterId = "_maintenance_cluster" });

            try
            {
                var dbEndpointGroups = await dbContext.EndpointGroups
                    .Include(eg => eg.EndpointGroupDestinations)
                    .ThenInclude(egd => egd.Destination)
                    .ToListAsync();

                foreach (var group in dbEndpointGroups)
                {
                    if (group.IsInMaintenanceMode)
                    {
                        routes.Add(new RouteConfig
                        {
                            RouteId = $"route_maintenance_for_{group.GroupName}",
                            ClusterId = "_maintenance_cluster",
                            Match = new RouteMatch { Path = group.PathPattern },
                            Order = -1   
                        });
                        continue;      
                    }

                    var activeDestinations = group.EndpointGroupDestinations
                        .Where(egd => egd.Destination != null && egd.Destination.IsEnabled && egd.IsEnabledInGroup)
                        .ToList();

                    if (activeDestinations.Any())
                    {
                        var clusterId = group.GroupName;
                        var destinations = activeDestinations.ToDictionary(
                            egd => $"dest_{clusterId}_{egd.Destination.DestinationId}",
                            egd => new DestinationConfig { Address = egd.Destination.Address }
                        );

                        string? clusterHealthCheckPath = activeDestinations.Select(egd => egd.Destination.HealthCheckPath).FirstOrDefault(hcp => !string.IsNullOrEmpty(hcp));
                        HealthCheckConfig? healthCheckConfig = !string.IsNullOrEmpty(clusterHealthCheckPath) ? new HealthCheckConfig { Active = new ActiveHealthCheckConfig { Enabled = true, Path = clusterHealthCheckPath, Interval = TimeSpan.FromSeconds(30), Timeout = TimeSpan.FromSeconds(10), Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures } } : null;

                        clusters.Add(new ClusterConfig
                        {
                            ClusterId = clusterId,
                            Destinations = destinations,
                            HealthCheck = healthCheckConfig,
                            LoadBalancingPolicy = LoadBalancingPolicies.PowerOfTwoChoices
                        });

                        routes.Add(new RouteConfig
                        {
                            RouteId = $"route_for_{clusterId}",
                            ClusterId = clusterId,
                            Match = new RouteMatch { Path = group.PathPattern },
                            Order = group.MatchOrder,
                            Transforms = new List<IReadOnlyDictionary<string, string>>
                            {
                                new Dictionary<string, string> { { "RequestHeaderOriginalHost", "true" } },
                                new Dictionary<string, string> { { "X-Forwarded", "Append" } }
                            }
                        });
                    }
                    else
                    {
                        _logger.LogWarning("El grupo '{GroupName}' no tiene destinos activos y no está en modo mantenimiento. No se generará ninguna ruta para él.", group.GroupName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico al construir la configuración de YARP desde la base de datos.");
                return (new List<RouteConfig>(), new List<ClusterConfig>());
            }

            return (routes.AsReadOnly(), clusters.AsReadOnly());
        }
    }
}
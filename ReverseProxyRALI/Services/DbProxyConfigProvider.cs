using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace FGate.Services
{
    public class DbProxyConfigProvider : IProxyConfigProvider
    {
        private readonly IYarpConfigService _configService;
        private readonly ProxyConfigManager _configManager;

        public DbProxyConfigProvider(IYarpConfigService configService, ProxyConfigManager configManager)
        {
            _configService = configService;
            _configManager = configManager;
        }

        public IProxyConfig GetConfig()
        {
            var (routes, clusters) = _configService.GetConfigAsync().GetAwaiter().GetResult();
            return new InMemoryConfig(routes, clusters, _configManager.ChangeToken);
        }

        private class InMemoryConfig : IProxyConfig
        {
            public IReadOnlyList<RouteConfig> Routes { get; }
            public IReadOnlyList<ClusterConfig> Clusters { get; }
            public IChangeToken ChangeToken { get; }

            public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters, IChangeToken changeToken)
            {
                Routes = routes;
                Clusters = clusters;
                ChangeToken = changeToken;
            }
        }
    }
}
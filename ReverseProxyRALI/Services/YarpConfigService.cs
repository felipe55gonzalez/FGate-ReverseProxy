using Yarp.ReverseProxy.Configuration;

namespace FGate.Services
{
    public interface IYarpConfigService
    {
        Task<(IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters)> GetConfigAsync();
    }
}

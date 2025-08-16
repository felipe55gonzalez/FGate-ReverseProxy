using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;    
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;

[ApiController]
[Route("api/[controller]")]
public class HealthStatusController : ControllerBase
{
    private readonly IProxyStateLookup _proxyStateLookup;
    private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;

    public HealthStatusController(IProxyStateLookup proxyStateLookup, IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
    {
        _proxyStateLookup = proxyStateLookup;
        _dbContextFactory = dbContextFactory;
    }

    [HttpGet("GetDestinationsStatus")]
    public async Task<IActionResult> GetDestinationsStatus()
    {
        var results = new List<object>();

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var clusterIds = await context.EndpointGroups
            .Select(g => g.GroupName)
            .ToListAsync();

        foreach (var clusterId in clusterIds)
        {
            if (_proxyStateLookup.TryGetCluster(clusterId, out var clusterState))
            {
                if (clusterState.DestinationsState == null) continue;

                foreach (var destination in clusterState.DestinationsState.AllDestinations)
                {
                    results.Add(new
                    {
                        DestinationId = destination.DestinationId,
                        Address = destination.Model.Config.Address,
                        Status = destination.Health.Passive.ToString()
                    });
                }
            }
        }

        return Ok(results);
    }
}
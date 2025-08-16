using Microsoft.EntityFrameworkCore;
using FGate.Data;
using FGate.Data.Entities;      
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace FGate.Services
{
    public class DbIpBlockingService : IIpBlockingService
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbIpBlockingService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(1);      
        private CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

        public DbIpBlockingService(
            IDbContextFactory<ProxyRaliDbContext> dbContextFactory,
            ILogger<DbIpBlockingService> logger,
            IMemoryCache memoryCache)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        public async Task<bool> IsIpBlockedAsync(IPAddress? ipAddress)
        {
            if (ipAddress == null)
            {
                return false;
            }

            string ipString = ipAddress.ToString();
            if (ipAddress.Equals(IPAddress.IPv6Loopback))
            {
                ipString = "::1";
            }

            string cacheKey = $"BlockedIp_{ipString}";

            if (_memoryCache.TryGetValue(cacheKey, out bool isBlockedCached))
            {
                return isBlockedCached;
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var blockedEntry = await dbContext.BlockedIps
                    .FirstOrDefaultAsync(b => b.IpAddress == ipString);

                bool isCurrentlyBlocked = false;
                if (blockedEntry != null)
                {
                    isCurrentlyBlocked = (blockedEntry.BlockedUntil == null || blockedEntry.BlockedUntil > DateTime.UtcNow);
                }
                else
                {
                }

                if (isCurrentlyBlocked)
                {
                }
                else
                {
                }

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                                    .SetAbsoluteExpiration(_cacheDuration)
                                    .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));

                _memoryCache.Set(cacheKey, isCurrentlyBlocked, cacheEntryOptions);
                return isCurrentlyBlocked;

            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public void ClearCache()
        {
            _logger.LogInformation("Invalidando toda la caché de IPs bloqueadas.");

            if (_resetCacheToken != null && !_resetCacheToken.IsCancellationRequested && _resetCacheToken.Token.CanBeCanceled)
            {
                _resetCacheToken.Cancel();
                _resetCacheToken.Dispose();
            }

            _resetCacheToken = new CancellationTokenSource();
        }
    }
}

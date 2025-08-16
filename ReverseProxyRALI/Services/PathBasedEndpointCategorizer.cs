using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;
using FGate.Models;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace FGate.Services
{
    public class PathBasedEndpointCategorizer : IEndpointCategorizer
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<PathBasedEndpointCategorizer> _logger;

        private record EndpointGroupPattern(string PathPattern, string GroupName, int MatchOrder, bool RequiresToken);

        private List<EndpointGroupPattern> _cachedPatterns = new List<EndpointGroupPattern>();
        private DateTime _lastCacheRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private readonly object _cacheLock = new object();
        private readonly object _refreshLock = new object();
        private volatile bool _isRefreshing = false;

        public PathBasedEndpointCategorizer(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<PathBasedEndpointCategorizer> logger)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;

            RefreshPatternsCacheAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public EndpointCategorizationResult? GetEndpointGroupForPath(string requestPath)
        {
            _logger.LogDebug("GetEndpointGroupForPath llamado con requestPath: {RequestPath}", requestPath);

            if (DateTime.UtcNow - _lastCacheRefresh > _cacheDuration && !_isRefreshing)
            {
                _logger.LogInformation("La caché de patrones ha expirado. Disparando actualización en segundo plano.");
                Task.Run(() => TriggerRefreshAsync());
            }

            List<EndpointGroupPattern> currentPatterns;
            lock (_cacheLock)
            {
                currentPatterns = new List<EndpointGroupPattern>(_cachedPatterns);
            }

            if (string.IsNullOrEmpty(requestPath))
            {
                return new EndpointCategorizationResult("Public_Group_EmptyPath", false, string.Empty);
            }

            foreach (var patternInfo in currentPatterns)
            {
                if (PathMatches(requestPath, patternInfo.PathPattern))
                {
                    _logger.LogDebug("Ruta '{RequestPath}' coincidió con el patrón '{PathPattern}' para el grupo '{GroupName}'. ReqToken: {RequiresToken}",
                        requestPath, patternInfo.PathPattern, patternInfo.GroupName, patternInfo.RequiresToken);
                    return new EndpointCategorizationResult(patternInfo.GroupName, patternInfo.RequiresToken, patternInfo.PathPattern);
                }
            }

            _logger.LogDebug("Ningún patrón específico de la base de datos coincidió con la ruta '{RequestPath}'. Usando 'Public_Group_NoMatch' por defecto.", requestPath);
            return new EndpointCategorizationResult("Public_Group_NoMatch", false, string.Empty);
        }

        public async Task TriggerRefreshAsync()
        {
            lock (_refreshLock)
            {
                if (_isRefreshing)
                {
                    _logger.LogInformation("La actualización de la caché de patrones ya está en progreso.");
                    return;
                }
                _isRefreshing = true;
            }

            try
            {
                await RefreshPatternsCacheAsync(CancellationToken.None);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async Task RefreshPatternsCacheAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Refrescando caché de patrones de EndpointGroup desde la base de datos...");
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var patternsFromDb = await dbContext.EndpointGroups
                    .Where(eg => !string.IsNullOrEmpty(eg.PathPattern))
                    .OrderBy(eg => eg.MatchOrder)
                    .ThenByDescending(eg => eg.PathPattern.Length)
                    .Select(eg => new EndpointGroupPattern(eg.PathPattern, eg.GroupName, eg.MatchOrder, eg.ReqToken))
                    .ToListAsync(cancellationToken);

                lock (_cacheLock)
                {
                    _cachedPatterns = patternsFromDb;
                    _lastCacheRefresh = DateTime.UtcNow;
                }
                _logger.LogInformation("Caché de patrones de EndpointGroup refrescada. {Count} patrones cargados.", _cachedPatterns.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al refrescar la caché de patrones de EndpointGroup.");
            }
        }

        private bool PathMatches(string requestPath, string pattern)
        {
            bool matchFound;

            if (pattern.EndsWith("/{**remainder}"))
            {
                string prefixPattern = pattern.Substring(0, pattern.Length - "/{**remainder}".Length);
                if (prefixPattern.Contains("{") && prefixPattern.Contains("}"))
                {
                    string regexPrefixString = ConvertPathPatternToRegexPrefix(prefixPattern);
                    if (Regex.IsMatch(requestPath, $"^{regexPrefixString}$", RegexOptions.IgnoreCase))
                    {
                        matchFound = true;
                    }
                    else if (Regex.IsMatch(requestPath, $"^{regexPrefixString}\\/.*$", RegexOptions.IgnoreCase))
                    {
                        matchFound = true;
                    }
                    else
                    {
                        matchFound = false;
                    }
                }
                else
                {
                    matchFound = requestPath.Equals(prefixPattern, StringComparison.OrdinalIgnoreCase) ||
                                 requestPath.StartsWith(prefixPattern + "/", StringComparison.OrdinalIgnoreCase);
                }

                return matchFound;
            }
            else if (pattern.EndsWith("/*"))
            {
                string prefixPattern = pattern.Substring(0, pattern.Length - "/*".Length);
                if (prefixPattern.Contains("{") && prefixPattern.Contains("}"))
                {
                    string regexPrefixString = ConvertPathPatternToRegexPrefix(prefixPattern);
                    matchFound = Regex.IsMatch(requestPath, $"^{regexPrefixString}\\/[^/]+(?:\\/.*)?$", RegexOptions.IgnoreCase);
                }
                else
                {
                    if (requestPath.StartsWith(prefixPattern + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        string remainder = requestPath.Substring(prefixPattern.Length + 1);
                        matchFound = remainder.Length > 0 && !remainder.Contains("/");
                    }
                    else
                    {
                        matchFound = false;
                    }
                }
                return matchFound;
            }
            else
            {
                if (pattern.Contains("{") && pattern.Contains("}"))
                {
                    string regexPattern = ConvertPathPatternToRegexPrefix(pattern);
                    matchFound = Regex.IsMatch(requestPath, $"^{regexPattern}$", RegexOptions.IgnoreCase);
                }
                else
                {
                    matchFound = requestPath.Equals(pattern, StringComparison.OrdinalIgnoreCase);
                }
                return matchFound;
            }
        }

        private string ConvertPathPatternToRegexPrefix(string pathPattern)
        {
            var regexBuilder = new StringBuilder();
            var segments = pathPattern.Split('/');
            bool firstSegmentProcessed = false;

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];

                if (i == 0 && string.IsNullOrEmpty(segment) && pathPattern.StartsWith("/"))
                {
                    regexBuilder.Append("\\/");
                    firstSegmentProcessed = true;
                    continue;
                }

                if (firstSegmentProcessed || (i > 0 && pathPattern.StartsWith("/")))
                {
                    if (!(i == 1 && string.IsNullOrEmpty(segments[0]) && pathPattern.StartsWith("/")))
                    {
                        regexBuilder.Append("\\/");
                    }
                }
                else if (i > 0)
                {
                    regexBuilder.Append("\\/");
                }

                if (segment.StartsWith("{") && segment.EndsWith("}"))
                {
                    regexBuilder.Append("([^/]+)");
                }
                else
                {
                    regexBuilder.Append(Regex.Escape(segment));
                }
                if (!string.IsNullOrEmpty(segment) || (i == 0 && string.IsNullOrEmpty(segment) && pathPattern.StartsWith("/")))
                {
                    firstSegmentProcessed = true;
                }
            }
            string resultRegex = regexBuilder.ToString();
            return resultRegex;
        }
    }
}
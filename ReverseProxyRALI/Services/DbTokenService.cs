using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;    
using FGate.Models;
namespace FGate.Services
{
    public class DbTokenService : ITokenService
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbTokenService> _logger;

        public DbTokenService(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<DbTokenService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<(bool IsValid, TokenDefinition? TokenDetails)> ValidateTokenAsync(string tokenValue, string requiredEndpointGroup)
        {
            if (string.IsNullOrEmpty(tokenValue) || string.IsNullOrEmpty(requiredEndpointGroup))
            {
                return (false, null);
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            try
            {
                var apiToken = await dbContext.ApiTokens
                    .Include(t => t.TokenPermissions)
                        .ThenInclude(tp => tp.Group)
                    .FirstOrDefaultAsync(t => t.TokenValue == tokenValue);

                if (apiToken == null)
                {
                    return (false, null);
                }

                if (!apiToken.IsEnabled)
                {
                    return (false, null);
                }

                if (apiToken.DoesExpire && apiToken.ExpiresAt.HasValue && apiToken.ExpiresAt.Value < DateTime.UtcNow)
                {
                    return (false, null);
                }

                var permission = apiToken.TokenPermissions
                    .FirstOrDefault(tp => tp.Group != null && tp.Group.GroupName == requiredEndpointGroup);

                if (permission == null)
                {
                    return (false, null);
                }

                var tokenDetailsModel = new Models.TokenDefinition(apiToken.TokenValue,
                    apiToken.TokenPermissions.Select(tp => tp.Group?.GroupName ?? string.Empty).ToList())
                {
                    IsActive = apiToken.IsEnabled,
                    ExpiryDate = apiToken.ExpiresAt ?? DateTime.MaxValue     
                };

                apiToken.LastUsedAt = DateTime.Now;
                await dbContext.SaveChangesAsync();


                return (true, tokenDetailsModel);
            }
            catch (Exception ex)
            {
                return (false, null);
            }
        }
    }
}

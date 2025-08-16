using Microsoft.AspNetCore.Mvc;
using FGate.Services;
using System.Threading.Tasks;

[ApiController]
[Route("api/management")]         
public class CacheController : ControllerBase
{
    private readonly ICacheManagementService _cacheService;

    public CacheController(ICacheManagementService cacheService)
    {
        _cacheService = cacheService;
    }

    [HttpPost("cache/refresh")]
    public async Task<IActionResult> RefreshCache()
    {
        await _cacheService.RefreshAllAsync();
        return Ok(new { message = "La caché del proxy ha sido invalidada y se está recargando." });
    }
}
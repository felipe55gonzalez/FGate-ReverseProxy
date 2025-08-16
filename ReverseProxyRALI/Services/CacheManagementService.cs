namespace FGate.Services
{
    public class CacheManagementService : ICacheManagementService
    {
        private readonly ILogger<CacheManagementService> _logger;
        private readonly PathBasedEndpointCategorizer _endpointCategorizer;
        private readonly DbIpBlockingService _ipBlockingService;
        private readonly ProxyConfigManager _proxyConfigManager;   

        public CacheManagementService(
            ILogger<CacheManagementService> logger,
            IEndpointCategorizer endpointCategorizer,
            IIpBlockingService ipBlockingService,
            ProxyConfigManager proxyConfigManager)   
        {
            _logger = logger;
            _endpointCategorizer = (PathBasedEndpointCategorizer)endpointCategorizer;
            _ipBlockingService = (DbIpBlockingService)ipBlockingService;
            _proxyConfigManager = proxyConfigManager;   
        }

        public async Task RefreshAllAsync()
        {
            _logger.LogInformation("Iniciando refresco manual de todas las cachés...");

            await _endpointCategorizer.TriggerRefreshAsync();

            _ipBlockingService.ClearCache();

            _proxyConfigManager.TriggerReload();
            _logger.LogInformation("Señal de recarga de configuración de YARP enviada.");
            _logger.LogInformation("Refresco de todas las cachés completado.");
        }
    }
}
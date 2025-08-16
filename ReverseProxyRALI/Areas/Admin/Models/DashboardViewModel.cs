namespace FGate.Areas.Admin.Models
{
    public class DashboardViewModel
    {
        public int EndpointGroupCount { get; set; }
        public int ApiTokenCount { get; set; }
        public int BlockedIpCount { get; set; }
        public long TotalRequests24h { get; set; }  
        public string? LastCacheRefreshMessage { get; set; }
        public string MainTrafficChartJson { get; set; } = "{}";  
    }
}
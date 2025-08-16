namespace FGate.Areas.Admin.Models
{
    public class TokenUsageDetailViewModel
    {
        public int TokenId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public bool IsEnabled { get; set; }
        public string TokenValue { get; set; } = string.Empty;

        public int TotalRequests { get; set; }
        public DateTime? FirstUsedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }

        public string UsageByEndpointChartJson { get; set; } = "{}";
        public string StatusCodesChartJson { get; set; } = "{}";

        public List<RecentRequestLog> RecentRequests { get; set; } = new List<RecentRequestLog>();
    }

    public class RecentRequestLog
    {
        public DateTime TimestampUtc { get; set; }
        public string HttpMethod { get; set; } = string.Empty;
        public string RequestPath { get; set; } = string.Empty;
        public int ResponseStatusCode { get; set; }
        public int DurationMs { get; set; }
    }
}
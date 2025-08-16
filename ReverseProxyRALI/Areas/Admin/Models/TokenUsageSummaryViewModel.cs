namespace FGate.Areas.Admin.Models
{
    public class TokenUsageSummaryViewModel
    {
        public int TokenId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public int TotalRequests { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public bool IsEnabled { get; set; }
    }
}
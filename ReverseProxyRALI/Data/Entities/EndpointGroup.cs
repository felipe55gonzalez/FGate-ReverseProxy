using System.ComponentModel.DataAnnotations.Schema;

namespace FGate.Data.Entities
{
    public class EndpointGroup
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? PathPattern { get; set; }
        public int MatchOrder { get; set; }
        public bool ReqToken { get; set; }
        public bool IsInMaintenanceMode { get; set; }
        public int? RateLimitRuleId { get; set; }

        [ForeignKey("RateLimitRuleId")]
        public virtual RateLimitRule? RateLimitRule { get; set; }

        public virtual ICollection<EndpointGroupDestination> EndpointGroupDestinations { get; set; } = new List<EndpointGroupDestination>();
        public virtual ICollection<HourlyTrafficSummary> HourlyTrafficSummaries { get; set; } = new List<HourlyTrafficSummary>();
        public virtual ICollection<TokenPermission> TokenPermissions { get; set; } = new List<TokenPermission>();

        // --- AÑADE ESTA LÍNEA ---
        public virtual ICollection<EndpointGroupWafRule> EndpointGroupWafRules { get; set; } = new List<EndpointGroupWafRule>();
    }
}
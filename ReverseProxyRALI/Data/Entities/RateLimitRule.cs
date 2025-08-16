using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FGate.Data.Entities
{
    public class RateLimitRule
    {
        [Key]
        public int RuleId { get; set; }

        [Required]
        [StringLength(150)]
        public string RuleName { get; set; } = null!;

        public int PeriodSeconds { get; set; }

        public int RequestLimit { get; set; }

        public DateTime CreatedAt { get; set; }

        [InverseProperty("RateLimitRule")]
        public virtual ICollection<EndpointGroup> EndpointGroups { get; set; } = new List<EndpointGroup>();
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FGate.Data.Entities
{
    public class WafRule
    {
        [Key]
        public int RuleId { get; set; }

        [Required]
        [StringLength(150)]
        public string RuleName { get; set; } = null!;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(500)]
        public string Pattern { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = "Block"; 

        public bool IsEnabled { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<EndpointGroupWafRule> EndpointGroupWafRules { get; set; } = new List<EndpointGroupWafRule>();
    }
}
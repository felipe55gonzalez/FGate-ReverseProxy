using System.ComponentModel.DataAnnotations.Schema;

namespace FGate.Data.Entities
{
    public class EndpointGroupWafRule
    {
        public int EndpointGroupId { get; set; }
        public int WafRuleId { get; set; }

        [ForeignKey("EndpointGroupId")]
        public virtual EndpointGroup EndpointGroup { get; set; } = null!;

        [ForeignKey("WafRuleId")]
        public virtual WafRule WafRule { get; set; } = null!;
    }
}
namespace FGate.Areas.Admin.Models
{
    public class WafRuleAssignmentViewModel
    {
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsAssigned { get; set; }
    }
}
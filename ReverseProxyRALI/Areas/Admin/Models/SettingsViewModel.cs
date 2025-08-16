using System.ComponentModel.DataAnnotations;

namespace FGate.Areas.Admin.Models
{
    public class SettingsViewModel
    {
        [Required]
        [Range(1, 365, ErrorMessage = "El valor debe estar entre 1 y 365 días.")]
        [Display(Name = "Días de Retención de Logs")]
        public int LogRetentionDays { get; set; } = 30;
    }
}
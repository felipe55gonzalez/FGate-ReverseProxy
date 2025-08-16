using System.ComponentModel.DataAnnotations;

namespace FGate.Areas.Admin.Models
{
    public class RouteTesterViewModel
    {
        [Required(ErrorMessage = "La ruta es requerida.")]
        [Display(Name = "Ruta de la Solicitud (Ej: /api/users/123)")]
        public string RequestPath { get; set; }

        public bool HasResult { get; set; } = false;
        public string? MatchedGroupName { get; set; }
        public bool RequiresToken { get; set; }
        public string? MatchedPathPattern { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
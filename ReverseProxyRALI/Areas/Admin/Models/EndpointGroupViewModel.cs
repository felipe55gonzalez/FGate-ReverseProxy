using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FGate.Areas.Admin.Models
{
    public class EndpointGroupViewModel
    {
        public int GroupId { get; set; }

        [Required(ErrorMessage = "El nombre del grupo es requerido.")]
        [Display(Name = "Nombre del Grupo")]
        public string GroupName { get; set; }

        [Display(Name = "Descripción")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "El patrón de ruta es requerido.")]
        [Display(Name = "Patrón de Ruta (ej. /api/users/{**remainder})")]
        public string PathPattern { get; set; }

        [Display(Name = "Orden de Coincidencia")]
        public int MatchOrder { get; set; }

        [Display(Name = "Requiere Token de API")]
        public bool ReqToken { get; set; }

        [Display(Name = "Modo Mantenimiento")]
        public bool IsInMaintenanceMode { get; set; }

        [Display(Name = "Regla de Límite de Tasa")]
        public int? RateLimitRuleId { get; set; }
        public List<SelectListItem>? AvailableRateLimitRules { get; set; }
        public List<WafRuleAssignmentViewModel> WafRuleAssignments { get; set; } = new();


        public List<BackendDestinationViewModel> Destinations { get; set; } = new List<BackendDestinationViewModel>();
    }

    public class BackendDestinationViewModel
    {
        public int DestinationId { get; set; }
        public string Address { get; set; }
        public string? FriendlyName { get; set; }
        public bool IsAssigned { get; set; }
    }
}
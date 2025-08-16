using System.ComponentModel.DataAnnotations;

namespace FGate.Areas.Admin.Models
{
    public class AllowedCorsOriginViewModel
    {
        public int OriginId { get; set; }

        [Required(ErrorMessage = "La URL del origen es requerida.")]
        [StringLength(512, ErrorMessage = "La URL no puede exceder los 512 caracteres.")]
        [Display(Name = "URL del Origen (Ej: https://mi-frontend.com)")]
        public string OriginUrl { get; set; }

        [StringLength(255)]
        [Display(Name = "Descripción")]
        public string? Description { get; set; }

        [Display(Name = "Habilitado")]
        public bool IsEnabled { get; set; }
    }
}
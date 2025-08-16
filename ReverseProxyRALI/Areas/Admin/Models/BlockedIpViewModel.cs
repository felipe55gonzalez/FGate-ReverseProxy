using System.ComponentModel.DataAnnotations;

namespace FGate.Areas.Admin.Models
{
    public class BlockedIpViewModel
    {
        [Required(ErrorMessage = "La dirección IP es requerida.")]
        [StringLength(45)]
        [Display(Name = "Dirección IP")]
        public string IpAddress { get; set; }

        [Display(Name = "Razón del Bloqueo")]
        public string? Reason { get; set; }

        [Display(Name = "Bloquear hasta (opcional)")]
        [DataType(DataType.DateTime)]
        public DateTime? BlockedUntil { get; set; }
    }
}
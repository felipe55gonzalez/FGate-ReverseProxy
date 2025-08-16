using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FGate.Areas.Admin.Models
{
    public class ApiTokenViewModel
    {
        public int TokenId { get; set; }

        [Display(Name = "Token (se genera automáticamente)")]
        public string? TokenValue { get; set; }

        [Required(ErrorMessage = "La descripción es requerida.")]
        [StringLength(255)]
        [Display(Name = "Descripción")]
        public string Description { get; set; }

        [StringLength(150)]
        [Display(Name = "Propietario")]
        public string? OwnerName { get; set; }

        [Display(Name = "Habilitado")]
        public bool IsEnabled { get; set; }

        [Display(Name = "Expira")]
        public bool DoesExpire { get; set; }

        [Display(Name = "Fecha de Expiración")]
        [DataType(DataType.DateTime)]
        public DateTime? ExpiresAt { get; set; }

        public List<TokenPermissionViewModel> Permissions { get; set; } = new List<TokenPermissionViewModel>();
    }

    public class TokenPermissionViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public bool IsAssigned { get; set; }
        
        [Display(Name = "Métodos HTTP Permitidos")]
        public string AllowedHttpMethods { get; set; } = "GET,POST,PUT,DELETE";
    }
}
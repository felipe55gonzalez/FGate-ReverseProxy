using System.ComponentModel.DataAnnotations;

namespace FGate.Data.Entities
{
    public class ProxyConfiguration
    {
        [Key]
        [StringLength(100)]
        public string ConfigurationKey { get; set; } = null!;

        [StringLength(500)]
        public string ConfigurationValue { get; set; } = null!;

        public DateTime UpdatedAt { get; set; }
    }
}
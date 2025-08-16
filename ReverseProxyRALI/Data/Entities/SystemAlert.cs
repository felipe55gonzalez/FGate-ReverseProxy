using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FGate.Data.Entities
{
    public class SystemAlert
    {
        [Key]
        public int AlertId { get; set; }

        public DateTime TimestampUtc { get; set; }

        [Required]
        [StringLength(50)]
        public string Level { get; set; } = string.Empty;

        [Required]
        [StringLength(250)]
        public string Title { get; set; } = string.Empty;

        public string? Details { get; set; }

        public bool IsRead { get; set; }
    }
}
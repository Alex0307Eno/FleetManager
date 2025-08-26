using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class PlaceAlias
    {
        [Key]
        public int AliasId { get; set; }

        [Required, MaxLength(100)]
        public string Keyword { get; set; }

        [Required, MaxLength(50)]
        public string Alias { get; set; }

        public string FullAddress { get; set; }
    }
}

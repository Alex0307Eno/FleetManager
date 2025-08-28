using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{

    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required, StringLength(50)]
        public string UserName { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string Role { get; set; }
    }

}

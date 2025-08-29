using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{

    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string Account { get; set; }
        public string PasswordHash { get; set; }
        public string DisplayName { get; set; }
        public string Role { get; set; }
    }

    public class LoginDto
    {
        public string Account { get; set; }
        public string Password { get; set; }
    }

}

using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class DispatchAdmin
    {
        [Key]
        public int AdminId { get; set; }
        public string Name { get; set; }
        public string Dept { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        public int? UserId { get; set; }
        public User User { get; set; }
    }

}

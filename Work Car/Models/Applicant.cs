namespace Cars.Models
{
    public class Applicant
    {
        public int ApplicantId { get; set; }   // PK
        public string Name { get; set; }
        public DateTime? Birth { get; set; }
        public string Dept { get; set; }
        public string Ext { get; set; }
        public string Email { get; set; }

        public int? UserId { get; set; }       // 對應 Users
        public User User { get; set; }

        // 🚗 對應多筆申請單
        public ICollection<CarApply> CarApplications { get; set; }
    }
}

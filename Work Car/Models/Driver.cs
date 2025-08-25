using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class Driver
    {
        public int DriverId { get; set; }
        public string? DriverName { get; set; }
        [Display(Name = "身分證字號"), MaxLength(20)]
        public string? NationalId { get; set; }


        [Display(Name = "出生年月日")]
        public DateTime? BirthDate { get; set; }


        [Display(Name = "戶籍地址"), MaxLength(200)]
        public string? HouseholdAddress { get; set; }


        [Display(Name = "聯絡地址"), MaxLength(200)]
        public string? ContactAddress { get; set; }


        [Display(Name = "市話"), MaxLength(50)]
        public string? Phone { get; set; }


        [Display(Name = "行動電話"), MaxLength(50)]
        public string? Mobile { get; set; }


        [Display(Name = "緊急聯絡人"), MaxLength(50)]
        public string? EmergencyContactName { get; set; }


        [Display(Name = "緊急聯絡電話"), MaxLength(50)]
        public string? EmergencyContactPhone { get; set; }

        // 🔗 關聯 (一個駕駛可以有多筆派車單)
        public virtual ICollection<Dispatch>? Dispatches { get; set; }
    }
}

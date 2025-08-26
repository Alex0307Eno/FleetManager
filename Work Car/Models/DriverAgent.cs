using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Cars.Models
{
    public class DriverAgent
    {
        [Key]
        public int AgentId { get; set; }

        [Required(ErrorMessage = "此為必填欄位")]
        [Display(Name = "代理人姓名")]
        public string AgentName { get; set; }

        [Required(ErrorMessage = "此為必填欄位")]
        [MaxLength(20)]
        [Display(Name = "身分證字號")]
        public string NationalId { get; set; }

        [Display(Name = "出生年月日")]
        public DateTime? BirthDate { get; set; }

        [MaxLength(200)]
        [Display(Name = "戶籍地址")]
        public string HouseholdAddress { get; set; }

        [MaxLength(200)]
        [Display(Name = "聯絡地址")]
        public string ContactAddress { get; set; }

        [MaxLength(50)]
        [Display(Name = "市話")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "此為必填欄位")]
        [MaxLength(50)]
        [Display(Name = "行動電話")]
        public string Mobile { get; set; }

        [MaxLength(50)]
        [Display(Name = "緊急聯絡人")]
        public string EmergencyContactName { get; set; }

        [MaxLength(50)]
        [Display(Name = "緊急聯絡電話")]
        public string EmergencyContactPhone { get; set; }

        // 🔗 一個代理人員可以有多筆代理紀錄
        public virtual ICollection<DriverDelegation> Delegations { get; set; }
    }
}

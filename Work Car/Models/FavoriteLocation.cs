// Models/FavoriteLocation.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class FavoriteLocation
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; } // 由登入者 Claim 取得

        [Required, MaxLength(80)]
        public string CustomName { get; set; } // 自訂名稱（別名），例：公司 / 家

        [Required, MaxLength(255)]
        public string Address { get; set; } // 完整地址文字

        [MaxLength(128)]
        public string PlaceId { get; set; } // Google Place Id（可選）

        public double? Lat { get; set; }
        public double? Lng { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

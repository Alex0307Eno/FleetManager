using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class RepairRequest
    {
        [Key] public int Id { get; set; }
        public int VehicleId { get; set; }
        public string PlateNo { get; set; } = "";
        public DateTime Date { get; set; }
        public int? Odometer { get; set; }
        public string Issue { get; set; } = "";
        public string Status { get; set; } = "待處理";
        public string? Vendor { get; set; }
        public decimal? CostEstimate { get; set; }
        public string? Note { get; set; }
    }
}

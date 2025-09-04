using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class VehicleRepair
    {
        [Key] public int RepairRequestId { get; set; }
        public int VehicleId { get; set; }
        public string PlateNo { get; set; } = "";
        public DateTime Date { get; set; }
        public string? Place { get; set; }
        public string Issue { get; set; } = "";
        public string Status { get; set; } = "待處理";
        public decimal? CostEstimate { get; set; }
        public string? Vendor { get; set; }
        public string? Note { get; set; }
    }
}

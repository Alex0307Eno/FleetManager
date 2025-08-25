using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class MaintenanceRecord
    {
        [Key] public int MaintenanceRecordId { get; set; }
        public int VehicleId { get; set; }
        public string PlateNo { get; set; } = "";
        public DateTime Date { get; set; }
        public int? Odometer { get; set; }
        public string Item { get; set; } = "";
        public string? Unit { get; set; }
        public decimal? Qty { get; set; }
        public decimal? Amount { get; set; }
        public string? Vendor { get; set; }
        public string? Note { get; set; }
    }
}

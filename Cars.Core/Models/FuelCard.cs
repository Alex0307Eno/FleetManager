namespace Cars.Models
{
    public class FuelCard
    {
        public int FuelCardId { get; set; }
        public string CardNo { get; set; } = null!;   // 中油卡號
        public int VehicleId { get; set; }
        public Vehicle Vehicle { get; set; } = null!;
        public bool IsActive { get; set; } = true;
    }
}

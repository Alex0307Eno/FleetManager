namespace Cars.Features.DashBoard
{
    public class DriverBreakingStatusDto
    {
        
        
            public int VehicleId { get; set; }
            public string PlateNo { get; set; }
            public string VehicleState { get; set; }   // DB 原本的狀態
            public string UiState { get; set; }        // 額外狀態（休息中/待命中）
            public DateTime? RestUntil { get; set; }   // 預計休息到什麼時候
            public int? RestRemainMinutes { get; set; }
        

    }
}

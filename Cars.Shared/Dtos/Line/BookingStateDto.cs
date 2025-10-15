namespace  Cars.Shared.Dtos.Line
{
    public class BookingStateDto
    {
        public string? ReserveTime { get; set; }
        public string? ArrivalTime { get; set; }
        public bool WaitingForManualDeparture { get; set; }
        public bool WaitingForManualArrival { get; set; }

        public string? Reason { get; set; }
        public int? PassengerCount { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public string? TripType { get; set; }
        // 給管理員指派流程用
        public int? SelectedDriverId { get; set; }
        public string? SelectedDriverName { get; set; }
    }
}

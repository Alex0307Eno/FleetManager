namespace Cars.Features.DashBoard
{
    public class DispatchMapView
    {
        public int DriverId { get; set; }
        public DateTime UseStart { get; set; }
        public DateTime UseEnd { get; set; }

        public string PlateNo { get; set; }
        public string Dept { get; set; }
        public string Name { get; set; }
        public int? PassengerCount { get; set; }
    }

}

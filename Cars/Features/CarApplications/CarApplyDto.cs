using Cars.Models;

namespace Cars.Features.CarApplications
{
    public record CarApplyDto
    {
        public CarApplication Application { get; set; }
        public List<CarPassenger> Passengers { get; set; } = new();

    }
}

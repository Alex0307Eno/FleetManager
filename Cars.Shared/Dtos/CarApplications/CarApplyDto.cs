using System;
using System.Collections.Generic;


namespace Cars.Shared.Dtos.CarApplications
{
    public record CarApplyDto
    {
        public CarApplicationDto Application { get; set; }   
        public List<CarPassengerDto> Passengers { get; set; } = new();

    }
}

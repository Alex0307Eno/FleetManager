using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cars.Shared.Dtos.CarApplications
{
    public class CarPassengerDto
    {
        public int PassengerId { get; set; }
        public int ApplyId { get; set; }
        public string? Name { get; set; }
        public string? DeptTitle { get; set; }
    }
}

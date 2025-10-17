using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cars.Shared.Dtos.Drivers
{
    public class DriverDto
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; }
        public bool IsAgent { get; set; }
    }
}

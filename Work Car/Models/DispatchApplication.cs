using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models

{
    public class DispatchApplication
    {
        [Key]
        public int DispatchId { get; set; }
        public int ApplyId { get; set; }
        public int Seats { get; set; }
        public Dispatch Dispatch { get; set; }

        public CarApplication Application { get; set; }

    }
}

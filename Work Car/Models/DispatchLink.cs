using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models

{
    public class DispatchLink
    {
        public int ParentDispatchId { get; set; }
        public int ChildDispatchId { get; set; }
        public int Seats { get; set; }
        public Dispatch ParentDispatch { get; set; }
        public Dispatch ChildDispatch { get; set; }

    }
}

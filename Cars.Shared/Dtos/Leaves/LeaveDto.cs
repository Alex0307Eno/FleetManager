using System;

namespace Cars.Shared.Dtos.Leaves
{
    public record LeaveDto
    {
        public string LeaveType { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Reason { get; set; }
    }
}

namespace Cars.Features.Drivers
{
    public record SetAssignmentDto
    {
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; } // null=一直到未來
        public int? A { get; set; }
        public int? B { get; set; }
        public int? C { get; set; }
        public int? D { get; set; }
        public int? E { get; set; }
    }
}

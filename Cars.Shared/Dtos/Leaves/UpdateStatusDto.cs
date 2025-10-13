namespace Cars.Shared.Dtos.Leaves
{
    public record UpdateStatusDto
    {
        public string Status { get; set; }
        public int? AgentDriverId { get; set; }
    }
}

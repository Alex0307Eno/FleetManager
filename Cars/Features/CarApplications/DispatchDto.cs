namespace Cars.Features.CarApplications
{
    public record DispatchDto(
      int DispatchId,
      int ApplyId,
      DateTime? StartTime,
      DateTime? EndTime,
      string DispatchStatus
  );
}

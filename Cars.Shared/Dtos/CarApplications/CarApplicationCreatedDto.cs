using System;

namespace Cars.Shared.Dtos.CarApplications
{
    public record CarApplicationCreatedDto
    (
        int ApplyId,
        string Status,
        DateTime UseStart,
        DateTime UseEnd,
        string ApplicantName
    );
}

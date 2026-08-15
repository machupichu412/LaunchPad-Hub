using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Cohorts;

public class UpdateCohortStatusRequest
{
    public CohortStatus Status { get; set; }
}

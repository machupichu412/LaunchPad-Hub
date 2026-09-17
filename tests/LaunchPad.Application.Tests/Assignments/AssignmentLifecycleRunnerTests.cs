using FluentAssertions;
using LaunchPad.Application.Assignments;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Xunit;

namespace LaunchPad.Application.Tests.Assignments;

/// <summary>
/// The date rules the nightly sweep applies, exercised without a database — a mis-set
/// boundary here would either start work early or leave assignments approved forever,
/// which is the bug this runner exists to fix.
/// </summary>
public class AssignmentLifecycleRunnerTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static Assignment Make(
        AssignmentStatus status, DateOnly? projectStart, DateOnly? projectEnd, DateOnly? assignmentStart = null) => new()
        {
            Status = status,
            StartDate = assignmentStart,
            Project = new Project { StartDate = projectStart, EndDate = projectEnd },
        };

    [Theory]
    [InlineData(-1, true)]   // started yesterday
    [InlineData(0, true)]    // starts today
    [InlineData(1, false)]   // starts tomorrow
    public void OpsApproved_StartsOnlyOnceItsStartDateHasArrived(int dayOffset, bool expected)
    {
        var assignment = Make(AssignmentStatus.OpsApproved, Today.AddDays(dayOffset), Today.AddDays(90));

        AssignmentLifecycleRunner.IsDueToStart(assignment, Today).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, true)]   // ended yesterday
    [InlineData(0, false)]   // ends today — still the last day of work
    [InlineData(1, false)]
    public void Active_CompletesOnlyAfterItsEndDateHasPassed(int dayOffset, bool expected)
    {
        var assignment = Make(AssignmentStatus.Active, Today.AddDays(-90), Today.AddDays(dayOffset));

        AssignmentLifecycleRunner.IsDueToComplete(assignment, Today).Should().Be(expected);
    }

    [Fact]
    public void WithoutProjectDates_TheApprovalDateStartsItAndNothingAutoCompletesIt()
    {
        var approvedYesterday = Make(AssignmentStatus.OpsApproved, null, null, assignmentStart: Today.AddDays(-1));
        AssignmentLifecycleRunner.IsDueToStart(approvedYesterday, Today).Should().BeTrue();

        var openEnded = Make(AssignmentStatus.Active, null, null);
        AssignmentLifecycleRunner.IsDueToComplete(openEnded, Today)
            .Should().BeFalse("with no end date, Ops closes the assignment rather than the job guessing a date");
    }

    [Theory]
    [InlineData(AssignmentStatus.Proposed)]
    [InlineData(AssignmentStatus.SponsorApproved)]
    [InlineData(AssignmentStatus.Withdrawn)]
    [InlineData(AssignmentStatus.Completed)]
    public void OtherStatusesAreLeftAlone(AssignmentStatus status)
    {
        var assignment = Make(status, Today.AddDays(-30), Today.AddDays(-2));

        AssignmentLifecycleRunner.IsDueToStart(assignment, Today).Should().BeFalse();
        AssignmentLifecycleRunner.IsDueToComplete(assignment, Today).Should().BeFalse();
    }
}

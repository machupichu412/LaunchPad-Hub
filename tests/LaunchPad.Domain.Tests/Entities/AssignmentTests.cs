using FluentAssertions;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Xunit;

namespace LaunchPad.Domain.Tests.Entities;

public class AssignmentTests
{
    [Fact]
    public void NewAssignment_DefaultsToProposedStatus()
    {
        var assignment = new Assignment();

        assignment.Status.Should().Be(AssignmentStatus.Proposed);
    }
}

using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;
using static JobTrack.Domain.Enums.ApplicationStatus;

namespace JobTrack.Tests.Unit;

public sealed class ApplicationStatusRulesTests
{
    [Theory]
    // Forward moves, including skipped stages.
    [InlineData(Saved, ToApply)]
    [InlineData(Saved, Applied)]
    [InlineData(ToApply, Interview)]
    [InlineData(Applied, Screening)]
    [InlineData(Applied, Offer)]
    [InlineData(Interview, Offer)]
    // Back and forth before applying.
    [InlineData(ToApply, Saved)]
    // Closing.
    [InlineData(Offer, Accepted)]
    [InlineData(Applied, Rejected)]
    [InlineData(Offer, Rejected)]
    [InlineData(Saved, Withdrawn)]
    [InlineData(Interview, Withdrawn)]
    // Reopening.
    [InlineData(Rejected, Interview)]
    [InlineData(Withdrawn, Saved)]
    public void Allowed_transitions(ApplicationStatus from, ApplicationStatus to) =>
        Assert.True(ApplicationStatusRules.CanTransition(from, to));

    [Theory]
    [InlineData(Applied, Applied)]
    [InlineData(Applied, Saved)]
    [InlineData(Interview, ToApply)]
    [InlineData(Interview, Screening)]
    [InlineData(Saved, Rejected)]
    [InlineData(ToApply, Rejected)]
    [InlineData(Interview, Accepted)]
    [InlineData(Accepted, Withdrawn)]
    [InlineData(Accepted, Offer)]
    [InlineData(Rejected, Accepted)]
    [InlineData(Rejected, Withdrawn)]
    [InlineData(Withdrawn, Rejected)]
    public void Forbidden_transitions(ApplicationStatus from, ApplicationStatus to) =>
        Assert.False(ApplicationStatusRules.CanTransition(from, to));

    [Fact]
    public void Accepted_is_final() =>
        Assert.Empty(ApplicationStatusRules.AllowedTransitionsFrom(Accepted));

    [Fact]
    public void Every_status_has_a_spanish_label()
    {
        foreach (var status in Enum.GetValues<ApplicationStatus>())
        {
            Assert.NotEqual(status.ToString(), ApplicationStatusRules.Label(status));
        }
    }
}

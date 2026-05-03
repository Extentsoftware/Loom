using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Runs;
using Xunit;

namespace Loom.Domain.Tests.Runs;

public sealed class RunEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StepStarted_CarriesStepKey()
    {
        var e = RunEvent.StepStarted(RunId.New(), sequence: 0, stepKey: "discovery", now: Now);
        e.Kind.Should().Be(RunEventKind.StepStarted);
        e.Sequence.Should().Be(0);
        e.PayloadJson.Should().Contain("\"step\":\"discovery\"");
    }

    [Fact]
    public void GateResolved_RecordsResolver()
    {
        var by = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var e = RunEvent.GateResolved(RunId.New(), 4, "discovery", by, Now);
        e.Kind.Should().Be(RunEventKind.GateResolved);
        e.PayloadJson.Should().Contain("\"by\":");
        e.PayloadJson.Should().Contain(by.ToString("N"));
    }

    [Fact]
    public void TokenUsage_CarriesCounts()
    {
        var e = RunEvent.TokenUsage(RunId.New(), 7, input: 1500, output: 800, now: Now);
        e.Kind.Should().Be(RunEventKind.TokenUsage);
        e.PayloadJson.Should().Be("""{"in":1500,"out":800}""");
    }

    [Fact]
    public void Factory_RejectsNegativeSequence()
    {
        var act = () => RunEvent.StepStarted(RunId.New(), sequence: -1, stepKey: "x", now: Now);
        act.Should().Throw<DomainException>().WithMessage("*sequence*");
    }
}

using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Xunit;

namespace Loom.Domain.Tests.Runs;

public sealed class AssembledPromptTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_CapturesSystemPromptAndMessages()
    {
        var runId = RunId.New();
        var fragRef = new FragmentRef(FragmentId.New(), FragmentVersionId.New(), 3);

        var p = AssembledPrompt.Create(
            runId,
            systemPrompt: "You are a PO discovery assistant.",
            messages: [new AssembledPromptMessage("user", "Here is the transcript.")],
            fragments: [fragRef],
            outputSchemaJson: """{"type":"object"}""",
            now: Now);

        p.RunId.Should().Be(runId);
        p.SystemPrompt.Should().Contain("PO discovery");
        p.Messages.Should().ContainSingle();
        p.Fragments.Should().ContainSingle().Which.Should().Be(fragRef);
        p.OutputSchemaJson.Should().NotBeNull();
        p.AssembledAt.Should().Be(Now);
    }

    [Fact]
    public void Create_RejectsEmptySystemPrompt()
    {
        var act = () => AssembledPrompt.Create(
            RunId.New(),
            systemPrompt: "",
            messages: [new AssembledPromptMessage("user", "x")],
            fragments: [],
            outputSchemaJson: null,
            now: Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsNoMessages()
    {
        var act = () => AssembledPrompt.Create(
            RunId.New(),
            systemPrompt: "system",
            messages: [],
            fragments: [],
            outputSchemaJson: null,
            now: Now);
        act.Should().Throw<DomainException>().WithMessage("*at least one message*");
    }

    [Fact]
    public void Create_NormalizesWhitespaceOutputSchemaToNull()
    {
        var p = AssembledPrompt.Create(
            RunId.New(),
            systemPrompt: "system",
            messages: [new AssembledPromptMessage("user", "x")],
            fragments: [],
            outputSchemaJson: "   ",
            now: Now);
        p.OutputSchemaJson.Should().BeNull();
    }
}

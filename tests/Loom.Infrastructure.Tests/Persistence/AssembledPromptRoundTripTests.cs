using FluentAssertions;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loom.Infrastructure.Tests.Persistence;

[Collection(nameof(MsSqlCollection))]
public sealed class AssembledPromptRoundTripTests(MsSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AssembledPrompt_RoundTripsMessagesAndFragments()
    {
        var runId = RunId.New();
        var fragRef = new FragmentRef(FragmentId.New(), FragmentVersionId.New(), 3);

        AssembledPromptId promptId;
        await using (var ctx = fixture.CreateContext())
        {
            var prompt = AssembledPrompt.Create(
                runId,
                systemPrompt: "You are a PO discovery assistant. Be concise.",
                messages:
                [
                    new AssembledPromptMessage("user", "Here is the transcript:\n\nAnna: We need express checkout."),
                    new AssembledPromptMessage("assistant", "Understood.")
                ],
                fragments: [fragRef],
                outputSchemaJson: """{"type":"object","properties":{"intent":{"type":"string"}}}""",
                now: Now);
            promptId = prompt.Id;
            ctx.AssembledPrompts.Add(prompt);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var loaded = await ctx.AssembledPrompts
                .AsNoTracking()
                .FirstAsync(p => p.Id == promptId);

            loaded.RunId.Should().Be(runId);
            loaded.SystemPrompt.Should().StartWith("You are a PO");
            loaded.OutputSchemaJson.Should().Contain("intent");
            loaded.Messages.Should().HaveCount(2);
            loaded.Messages[0].Role.Should().Be("user");
            loaded.Messages[1].Content.Should().Be("Understood.");
            loaded.Fragments.Should().ContainSingle().Which.Should().Be(fragRef);
        }
    }
}

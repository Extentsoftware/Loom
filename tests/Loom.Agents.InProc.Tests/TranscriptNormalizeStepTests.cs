using System.Text.Json;
using FluentAssertions;
using Loom.Agents.InProc;
using Xunit;

namespace Loom.Agents.InProc.Tests;

public sealed class TranscriptNormalizeStepTests
{
    private static async Task<JsonElement> Normalize(string transcript)
    {
        var step = new TranscriptNormalizeStep();
        var json = await step.ExecuteAsync(new Dictionary<string, string> { ["transcript"] = transcript });
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task SimpleTranscript_SplitsBySpeaker()
    {
        const string transcript = """
            Anna: We need express checkout for returning users.
            Ben: Agreed, the abandonment numbers are bad.
            Anna: Let's prioritise it for next quarter.
            """;

        var root = await Normalize(transcript);
        var turns = root.GetProperty("turns");

        turns.GetArrayLength().Should().Be(3);
        turns[0].GetProperty("speaker").GetString().Should().Be("Anna");
        turns[0].GetProperty("text").GetString().Should().StartWith("We need express checkout");
        turns[0].GetProperty("order").GetInt32().Should().Be(0);
        turns[1].GetProperty("speaker").GetString().Should().Be("Ben");
        turns[2].GetProperty("order").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task MultiLineTurn_StaysWithSpeaker()
    {
        const string transcript = """
            Anna: First line.
            Continuation of Anna's turn.
            Ben: Ben's response.
            """;

        var root = await Normalize(transcript);
        var turns = root.GetProperty("turns");

        turns.GetArrayLength().Should().Be(2);
        turns[0].GetProperty("text").GetString()
            .Should().Contain("First line.")
            .And.Contain("Continuation");
    }

    [Fact]
    public async Task EmptyTranscript_ReturnsEmptyTurns()
    {
        var step = new TranscriptNormalizeStep();
        var json = await step.ExecuteAsync(new Dictionary<string, string>());
        var root = JsonDocument.Parse(json).RootElement;
        root.GetProperty("turns").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task TranscriptWithoutSpeakers_FallsBackToUnknown()
    {
        const string transcript = "Just a paragraph of free text with no speaker labels.";
        var root = await Normalize(transcript);
        var turns = root.GetProperty("turns");

        turns.GetArrayLength().Should().Be(1);
        turns[0].GetProperty("speaker").GetString().Should().Be("Unknown");
    }

    [Fact]
    public void StepKey_IsNormalize()
    {
        new TranscriptNormalizeStep().StepKey.Should().Be("normalize");
    }
}

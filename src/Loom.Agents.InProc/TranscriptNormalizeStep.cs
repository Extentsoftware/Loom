using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Loom.Application.Workflows;

namespace Loom.Agents.InProc;

/// <summary>
/// Stage-0 of the kickoff workflow: convert a pasted Teams/Slack-style
/// transcript into a structured TranscriptTurns JSON array. Pure regex —
/// no LLM. Format expected: lines like "Speaker Name: text...". Multi-line
/// turns continue until the next "Speaker Name:" pattern is encountered.
///
/// This is intentionally simple. Phase 3+ swaps in a structural normaliser
/// driven by an inproc LLM if the heuristic doesn't hold for real
/// transcripts.
/// </summary>
public sealed partial class TranscriptNormalizeStep : IInProcStep
{
    public string StepKey => "normalize";

    [GeneratedRegex(@"^([\p{L}][\p{L}\p{M}\.\- ']{0,80}?):\s*(.*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SpeakerLine();

    public Task<string> ExecuteAsync(IReadOnlyDictionary<string, string> inputs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (!inputs.TryGetValue("transcript", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            // No transcript to normalize — return an empty turn list.
            return Task.FromResult(JsonSerializer.Serialize(new TranscriptTurns(Turns: [])));
        }

        var turns = new List<TranscriptTurn>();
        var matches = SpeakerLine().Matches(raw);

        if (matches.Count == 0)
        {
            // Couldn't parse speakers — fall back to one anonymous turn.
            turns.Add(new TranscriptTurn("Unknown", raw.Trim(), 0));
            return Task.FromResult(JsonSerializer.Serialize(new TranscriptTurns(turns)));
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var speaker = m.Groups[1].Value.Trim();
            var firstLine = m.Groups[2].Value.Trim();

            // Capture continuation lines up to the next match.
            var contentEnd = i + 1 < matches.Count ? matches[i + 1].Index : raw.Length;
            var contentStart = m.Index + m.Length;
            var continuation = contentStart < contentEnd
                ? raw[contentStart..contentEnd].Trim()
                : string.Empty;

            var text = string.IsNullOrEmpty(continuation)
                ? firstLine
                : $"{firstLine}\n{continuation}".Trim();

            turns.Add(new TranscriptTurn(speaker, text, turns.Count));
        }

        return Task.FromResult(JsonSerializer.Serialize(new TranscriptTurns(turns)));
    }

    /// <summary>
    /// Wire shape consumed by downstream workflow steps. Order is the turn's
    /// position in the source transcript; Speaker/Text are verbatim trimmed.
    /// </summary>
    private sealed record TranscriptTurns(
        [property: JsonPropertyName("turns")] IReadOnlyList<TranscriptTurn> Turns);

    private sealed record TranscriptTurn(
        [property: JsonPropertyName("speaker")] string Speaker,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("order")] int Order);
}

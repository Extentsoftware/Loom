using System.Text;
using System.Text.Json;
using Loom.Application.Abstractions;
using Loom.Application.Agents;
using Loom.Domain.Runs;

namespace Loom.Application.Workflows.Kickoff;

/// <summary>
/// Implementation that runs a one-shot agent call against the routed
/// engine (Foundry by default). The classification doesn't go through
/// the workflow engine — it bypasses Run aggregate persistence because
/// the result is advisory and short-lived. The synthesised RunId is
/// only used to satisfy AssembledPrompt / AgentRunRequest construction;
/// the runtime uses Prompt.SystemPrompt + Messages directly and never
/// reads back through that id.
/// </summary>
public sealed class ScopeClassifier(IAgentRouter router, ISystemClock clock) : IScopeClassifier
{
    private const string SystemPrompt = """
        You classify kickoff transcripts. Decide whether the transcript
        describes ONE feature or MULTIPLE features.

        - "single" — the conversation is about one coherent feature even
          if many capabilities, edge cases, or stakeholders are
          mentioned. The output of decompose will be capabilities under
          that one feature.
        - "multi" — the transcript clearly covers two or more
          independently-shippable features (e.g. "let's go through
          checkout, the saved-card flow, and a fraud-flag panel"). The
          output will be an initiative containing several features,
          each with their own capabilities.

        Output strict JSON with this shape — no prose, no markdown:

          {
            "mode":            "single" | "multi",
            "confidence":      number,        // 0.0–1.0
            "reason":          string,        // one short sentence
            "suggestedTitles": [string, ...]  // empty for single; one per feature for multi
          }

        Rules:
        - When in doubt, prefer "single" with confidence ≤ 0.6.
        - "suggestedTitles" is empty when mode is "single".
        - Each suggested title is noun-shaped, ≤ 60 chars.
        """;

    private static readonly Budgets ClassifyBudgets = new(
        MaxInputTokens: 50_000,
        MaxOutputTokens: 600,
        MaxWallClock: TimeSpan.FromSeconds(30),
        MaxCostUsd: 0.05m);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<ScopeClassification> ClassifyAsync(string transcript, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        var runId = RunId.New();
        var prompt = AssembledPrompt.Create(
            runId,
            SystemPrompt,
            messages: [new AssembledPromptMessage("user", $"Transcript:\n\n{transcript}")],
            fragments: [],
            outputSchemaJson: null,
            now: clock.UtcNow);

        var request = new AgentRunRequest(
            RunId: runId,
            Prompt: prompt,
            Budgets: ClassifyBudgets,
            ToolGrants: [],
            PreferredModel: null,
            RequiresJsonOutput: true);

        var runtime = router.Resolve(EngineName.Foundry);
        var externalRunId = await runtime.StartAsync(request, ct);

        var sb = new StringBuilder();
        string? failure = null;
        await foreach (var evt in runtime.StreamEventsAsync(externalRunId, ct).WithCancellation(ct))
        {
            switch (evt)
            {
                case AgentRunEvent.Output o:
                    sb.Append(o.Content);
                    break;
                case AgentRunEvent.Failed f:
                    failure = f.Reason;
                    break;
            }
        }

        if (failure is not null)
        {
            // Treat any classifier failure as "advisory unavailable" —
            // fall back to single-feature with low confidence so the
            // Kickoff page still surfaces both options.
            return new ScopeClassification(
                KickoffScope.Single, Confidence: 0.0,
                Reason: $"Classifier unavailable: {failure}",
                SuggestedTitles: []);
        }

        var raw = sb.ToString().Trim();
        try
        {
            return JsonSerializer.Deserialize<ScopeClassification>(raw, JsonOptions)
                ?? FallbackSingle("Empty classifier response.");
        }
        catch (JsonException ex)
        {
            return FallbackSingle($"Classifier returned non-JSON: {ex.Message}");
        }
    }

    private static ScopeClassification FallbackSingle(string reason) =>
        new(KickoffScope.Single, Confidence: 0.0, Reason: reason, SuggestedTitles: []);
}

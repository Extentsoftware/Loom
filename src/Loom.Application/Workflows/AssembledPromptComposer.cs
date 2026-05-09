using System.Globalization;
using System.Text;
using Loom.Application.Fragments;
using Loom.Domain.Artifacts;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows;

/// <summary>
/// Default composer: stitches identity → methodology → project → domain → skill
/// fragments into a single system prompt, then adds the live node context as a
/// trailing system block, and the step inputs (e.g. the raw transcript) as a
/// user message. The fragments list captured on the AssembledPrompt records
/// the exact (FragmentId, FragmentVersionId) trio used so a replay can be
/// deterministic.
/// </summary>
public sealed class AssembledPromptComposer : IAssembledPromptComposer
{
    public AssembledPrompt Compose(
        WorkflowStep workflowStep,
        IReadOnlyList<EffectiveFragment> fragments,
        NodeContext nodeContext,
        IReadOnlyDictionary<string, string> inputs,
        RunId runId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(workflowStep);
        ArgumentNullException.ThrowIfNull(fragments);
        ArgumentNullException.ThrowIfNull(nodeContext);
        ArgumentNullException.ThrowIfNull(inputs);

        var systemBuilder = new StringBuilder();

        foreach (var category in PromptCategoryOrder)
        {
            foreach (var ef in fragments.Where(f => f.Fragment.Category == category))
            {
                AppendFragmentBlock(systemBuilder, ef);
            }
        }

        AppendNodeContext(systemBuilder, nodeContext);
        AppendProjectArtifacts(systemBuilder, nodeContext);

        // If the step declares an OutputSchemaName the runtime forces JSON
        // mode, but the agent still needs to know the *shape* of that JSON.
        // Append a known-schema instruction so we don't rely on the
        // selected fragments happening to teach the right shape.
        AppendOutputSchema(systemBuilder, workflowStep.OutputSchemaName);

        var messages = new List<AssembledPromptMessage>();
        foreach (var (key, value) in inputs)
        {
            messages.Add(new AssembledPromptMessage(
                Role: "user",
                Content: $"<{key}>\n{value}\n</{key}>"));
        }
        if (messages.Count == 0)
        {
            // The aggregate requires at least one message; provide an explicit
            // pass-through marker rather than letting the caller pass empty.
            messages.Add(new AssembledPromptMessage("user", "(no inputs provided)"));
        }

        var fragmentRefs = fragments
            .Select(ef => new FragmentRef(ef.Fragment.Id, ef.Version.Id, ef.Version.Version))
            .ToList();

        return AssembledPrompt.Create(
            runId: runId,
            systemPrompt: systemBuilder.ToString().TrimEnd(),
            messages: messages,
            fragments: fragmentRefs,
            outputSchemaJson: null,
            now: now);
    }

    private static readonly FragmentCategory[] PromptCategoryOrder =
    [
        FragmentCategory.Identity,
        FragmentCategory.Methodology,
        FragmentCategory.Project,
        FragmentCategory.Domain,
        FragmentCategory.Skill,
        FragmentCategory.Context,
        FragmentCategory.Feedback
    ];

    private static void AppendFragmentBlock(StringBuilder sb, EffectiveFragment ef)
    {
        var inv = CultureInfo.InvariantCulture;
        sb.Append("## ").Append(ef.Fragment.Category).Append(": ").AppendLine(ef.Fragment.Title);
        sb.AppendLine(inv, $"<!-- {ef.Fragment.Key.Value} v{ef.Version.Version} ({ef.Source}) -->");
        sb.AppendLine(ef.Version.Content);
        sb.AppendLine();
    }

    private static void AppendNodeContext(StringBuilder sb, NodeContext ctx)
    {
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine("## Context");
        sb.AppendLine(inv, $"Node: {ctx.Title} ({ctx.Type}, phase: {ctx.Phase})");
        if (ctx.AncestorTitles.Count > 0)
        {
            sb.AppendLine(inv, $"Ancestors: {string.Join(" ▸ ", ctx.AncestorTitles)}");
        }
        if (!string.IsNullOrWhiteSpace(ctx.Intent))
        {
            sb.AppendLine(inv, $"Intent: {ctx.Intent}");
        }
        if (ctx.Outcomes.Count > 0)
        {
            sb.AppendLine("Outcomes:");
            foreach (var o in ctx.Outcomes)
            {
                sb.Append("  - ").AppendLine(o.Statement);
            }
        }
        if (ctx.OpenQuestions.Count > 0)
        {
            sb.AppendLine("Open questions:");
            foreach (var q in ctx.OpenQuestions)
            {
                sb.Append("  - ").AppendLine(q);
            }
        }
        sb.AppendLine();
    }

    private static void AppendProjectArtifacts(StringBuilder sb, NodeContext ctx)
    {
        if (ctx.ProjectArtifacts.Count == 0)
        {
            return;
        }

        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine("## Project artifacts");
        sb.AppendLine("Reference material attached to this project (and any feature-scoped");
        sb.AppendLine("seeds). Links are open via their URL; file uploads are addressable via");
        sb.AppendLine("the MCP get_artifact tool using their loom-artifact:// URI.");
        sb.AppendLine();
        foreach (var a in ctx.ProjectArtifacts)
        {
            if (a.Payload == ProjectArtifactPayload.Link)
            {
                sb.AppendLine(inv, $"- [{a.Kind}] {a.Label} — {a.Url}");
            }
            else
            {
                sb.AppendLine(inv, $"- [{a.Kind}] {a.Label} — {a.BlobUri} ({a.ContentType ?? "unknown"})");
            }
            if (!string.IsNullOrWhiteSpace(a.Description))
            {
                sb.Append("  ").AppendLine(a.Description);
            }
        }
        sb.AppendLine();
    }

    private static void AppendOutputSchema(StringBuilder sb, string? schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            return;
        }
        var instruction = schemaName switch
        {
            "Wireframe" =>
                """
                Output strict JSON matching the Wireframe schema EXACTLY:

                {
                  "description": "<one-paragraph summary of what the wireframe shows and why>",
                  "html": "<self-contained HTML document. Inline CSS via a <style> block. No external resources, no scripts, no images. Use semantic tags (header/main/section/nav/article/footer) and a clean grid; the goal is a low-fidelity layout reviewers can critique.>"
                }

                Hard rules:
                - Top-level JSON object with exactly the two string properties above.
                - Do NOT wrap the JSON in markdown fences.
                - The html property must contain a complete <html><head><style>…</style></head><body>…</body></html> document.
                - Keep the wireframe greyscale + monospace; no hero imagery, no real product copy, no chrome that pretends to be a finished product.
                """,
            "AcceptanceCriteria" =>
                """
                Output strict JSON matching the AcceptanceCriteria schema EXACTLY:

                [
                  { "statement": "<scenario in given/when/then form, single sentence>",
                    "metric_hint": "<optional measurable signal, or null>",
                    "measurable": true | false }
                ]

                Hard rules:
                - Top-level JSON array (no wrapper object).
                - 3–8 items typical; never zero.
                - Do NOT wrap the JSON in markdown fences.
                """,
            "RiskRegister" =>
                """
                Output strict JSON matching the RiskRegister schema EXACTLY:

                [
                  { "risk":       "<one-sentence statement of what could go wrong>",
                    "likelihood": "low" | "medium" | "high",
                    "impact":     "low" | "medium" | "high",
                    "mitigation": "<one-sentence concrete countermeasure>" }
                ]

                Hard rules:
                - Top-level JSON array (no wrapper object).
                - Likelihood and impact must be one of the three string values above (lowercase).
                - 3–8 items typical.
                - Do NOT wrap the JSON in markdown fences.
                """,
            "SliceDesign" =>
                """
                Output strict JSON matching the SliceDesign schema EXACTLY:

                {
                  "summary":   "<one-paragraph plain-English summary of the slice>",
                  "data_model": [
                    { "entity": "<TypeName>",
                      "fields": [
                        { "name": "<fieldName>", "type": "<string|int|Guid|…>", "notes": "<optional>" }
                      ],
                      "rationale": "<why this shape>" }
                  ],
                  "endpoints": [
                    { "verb":     "GET" | "POST" | "PUT" | "PATCH" | "DELETE",
                      "route":    "/api/...",
                      "purpose":  "<what it does>",
                      "request":  "<request shape or null>",
                      "response": "<response shape or null>" }
                  ],
                  "handlers": [
                    { "name": "<HandlerName>", "purpose": "<single sentence>" }
                  ],
                  "open_questions": [ "<question 1>", "<question 2>" ]
                }

                Hard rules:
                - Top-level JSON object with exactly the keys above; arrays may be empty but must be present.
                - Do NOT wrap the JSON in markdown fences.
                - Be specific to the node's intent and acceptance criteria; do not invent unrelated entities.
                """,
            "ImplementationPlan" =>
                """
                Output strict JSON matching the ImplementationPlan schema EXACTLY:

                {
                  "summary": "<one-paragraph plain-English summary of the build approach>",
                  "steps": [
                    { "ordinal":     1,
                      "title":       "<short imperative step title>",
                      "files":       [ "<path/to/File.cs>", "<path/to/Other.cs>" ],
                      "rationale":   "<one-sentence why>",
                      "verification":"<how the dev confirms this step is done — test, build, manual check>" }
                  ],
                  "risks":       [ "<technical risk to keep an eye on>" ],
                  "open_questions": [ "<question for the slice designer or PO>" ]
                }

                Hard rules:
                - Top-level JSON object with exactly the keys above.
                - 3–10 steps typical; ordinals start at 1 and are contiguous.
                - "files" lists realistic relative paths the dev will touch.
                - Do NOT wrap the JSON in markdown fences.
                """,
            "TestPlan" =>
                """
                Output strict JSON matching the TestPlan schema EXACTLY:

                {
                  "summary": "<one-paragraph plain-English coverage summary>",
                  "cases": [
                    { "id":            "TC-001",
                      "title":         "<short imperative title>",
                      "level":         "unit" | "integration" | "e2e",
                      "given":         "<precondition>",
                      "when":          "<action>",
                      "then":          "<expected outcome>",
                      "criterion_ref": "<acceptance-criterion id or text fragment, or null>" }
                  ],
                  "gaps": [ "<area the agent could not cover and why>" ]
                }

                Hard rules:
                - Top-level JSON object with exactly the keys above.
                - Test ids are TC-NNN starting at TC-001, contiguous, zero-padded to three digits.
                - Levels must be one of: unit, integration, e2e.
                - Do NOT wrap the JSON in markdown fences.
                """,
            _ => null
        };
        if (instruction is null)
        {
            return;
        }
        sb.AppendLine("## Required output shape");
        sb.AppendLine(CultureInfo.InvariantCulture, $"<!-- schema: {schemaName} -->");
        sb.AppendLine(instruction);
        sb.AppendLine();
    }
}

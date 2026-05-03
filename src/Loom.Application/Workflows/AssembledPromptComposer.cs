using System.Globalization;
using System.Text;
using Loom.Application.Fragments;
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
}

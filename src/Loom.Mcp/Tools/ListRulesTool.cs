using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Loom.Application.Abstractions;
using Loom.Domain.Fragments;
using ModelContextProtocol.Server;

namespace Loom.Mcp.Tools;

/// <summary>
/// Phase-2 MCP tool: <c>loom.list_rules</c>. Returns the same compiled view
/// that the Phase-2 GitSync writer puts into <c>CLAUDE.md</c> /
/// <c>.cursorrules</c> — a flat concatenation of the global Methodology +
/// Project + Skill fragments at their current versions, scoped to a
/// project. IDE clients call this to refresh in-place when the developer
/// hasn't pulled latest from git.
/// </summary>
[McpServerToolType]
public static class ListRulesTool
{
    [McpServerTool(Name = "loom_list_rules")]
    [Description("List the current methodology + project + skill fragments compiled into a single text block (matches what GitSync writes to CLAUDE.md).")]
    public static async Task<ListRulesResult> ListAsync(
        IFragmentRepository fragments,
        [Description("Optional project id to include project-scoped fragments alongside globals.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var globals = await fragments.ListGlobalAsync(ct);
        var projectFragments = projectId is Guid pid
            ? await fragments.ListByProjectAsync(pid, ct)
            : Array.Empty<Fragment>();

        var rendered = new StringBuilder();
        var ordered = globals
            .Concat(projectFragments)
            .Where(f => f.CurrentVersion is not null && !f.CurrentVersion.IsDeprecated)
            .Where(f => f.Category is FragmentCategory.Identity
                or FragmentCategory.Methodology
                or FragmentCategory.Project
                or FragmentCategory.Skill
                or FragmentCategory.Domain)
            .OrderBy(f => (int)f.Category)
            .ThenBy(f => f.Key.Value, StringComparer.Ordinal)
            .ToList();

        foreach (var f in ordered)
        {
            rendered.Append("## ").Append(f.Category).Append(": ").AppendLine(f.Title);
            rendered.AppendLine(CultureInfo.InvariantCulture, $"<!-- {f.Key.Value} v{f.CurrentVersion!.Version} -->");
            rendered.AppendLine(f.CurrentVersion.Content);
            rendered.AppendLine();
        }

        return new ListRulesResult(
            ProjectId: projectId,
            Count: ordered.Count,
            Markdown: rendered.ToString().TrimEnd(),
            Fragments: [.. ordered.Select(f => new RuleFragmentDto(
                Key: f.Key.Value,
                Title: f.Title,
                Category: f.Category.ToString(),
                Scope: f.Scope.ToString(),
                Version: f.CurrentVersion!.Version))]);
    }

    public sealed record ListRulesResult(
        [property: JsonPropertyName("project_id")] Guid? ProjectId,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("markdown")] string Markdown,
        [property: JsonPropertyName("fragments")] IReadOnlyList<RuleFragmentDto> Fragments);

    public sealed record RuleFragmentDto(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("version")] int Version);
}

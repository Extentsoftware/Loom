using System.Text.Json;
using FluentAssertions;
using Loom.Application.Memory;
using Loom.Domain.Nodes;
using Xunit;

namespace Loom.Application.Tests.Memory;

public sealed class MemoryLookupStepTests
{
    [Fact]
    public async Task Empty_inputs_emit_empty_report_without_calling_search()
    {
        var search = new RecordingSearch();
        var step = new MemoryLookupStep(search);
        var json = await step.ExecuteAsync(new Dictionary<string, string>());

        var report = JsonSerializer.Deserialize<SimilarityReport>(json)!;
        report.Candidates.Should().BeEmpty();
        search.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Inputs_passed_through_to_search()
    {
        var search = new RecordingSearch();
        var step = new MemoryLookupStep(search);
        var pid = Guid.CreateVersion7();
        var json = await step.ExecuteAsync(new Dictionary<string, string>
        {
            ["memory-lookup.query"] = "checkout flow",
            ["memory-lookup.project_id"] = pid.ToString("D"),
            ["memory-lookup.limit"] = "3"
        });

        search.LastQuery.Should().Be("checkout flow");
        search.LastProjectId.Should().Be(pid);
        search.LastLimit.Should().Be(3);
        json.Should().NotBeNullOrEmpty();
    }

    private sealed class RecordingSearch : IMemorySearch
    {
        public int Calls { get; private set; }
        public string? LastQuery { get; private set; }
        public Guid? LastProjectId { get; private set; }
        public int LastLimit { get; private set; }

        public Task<SimilarityReport> SearchSimilarAsync(string query, Guid? projectId, int limit, CancellationToken ct = default)
        {
            Calls++;
            LastQuery = query;
            LastProjectId = projectId;
            LastLimit = limit;
            return Task.FromResult(new SimilarityReport(query, []));
        }
    }
}

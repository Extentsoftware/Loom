using FluentAssertions;
using Loom.Application.Abstractions;
using Loom.Application.Artifacts;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Loom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loom.Infrastructure.Tests.Persistence;

[Collection(nameof(MsSqlCollection))]
public sealed class ProjectArtifactRoundTripTests(MsSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    [Fact]
    public async Task LinkArtifact_RoundTrips_AndIsListedAtProjectScope()
    {
        var projectId = await SeedProject(fixture);

        await using (var ctx = fixture.CreateContext())
        {
            var a = ProjectArtifact.CreateLink(
                projectId, nodeId: null, ProjectArtifactKind.Repo,
                "Existing checkout", "https://github.com/acme/checkout",
                description: "current production code",
                createdByUserId: Guid.CreateVersion7(), Now);
            ctx.ProjectArtifacts.Add(a);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var repo = new ProjectArtifactRepository(ctx);
            var rows = await repo.ListForFeatureAsync(projectId, nodeId: null);
            rows.Should().HaveCount(1);
            rows[0].Url.Should().Be("https://github.com/acme/checkout");
            rows[0].Payload.Should().Be(ProjectArtifactPayload.Link);
            rows[0].Blob.Should().BeNull();
        }
    }

    [Fact]
    public async Task FileArtifact_PutsAndRetrievesBundle_ViaBlobStore()
    {
        var projectId = await SeedProject(fixture);
        var clock = new FixedClock(Now);

        BlobRef blobRef;
        await using (var ctx = fixture.CreateContext())
        {
            var store = new SqlArtifactBlobStore(ctx, clock);
            var bundle = new ArtifactBundle([
                new ArtifactFile("checkout.png", "image/png", new byte[] { 1, 2, 3, 4, 5 })
            ]);
            blobRef = await store.PutAsync(bundle);

            var fileArtifact = ProjectArtifact.CreateFile(
                projectId, nodeId: null, ProjectArtifactKind.Image,
                "Current checkout screenshot", blobRef,
                description: null, createdByUserId: null, Now);
            ctx.ProjectArtifacts.Add(fileArtifact);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var store = new SqlArtifactBlobStore(ctx, clock);
            var fetched = await store.GetAsync(blobRef.Uri);
            fetched.Should().NotBeNull();
            fetched!.Files.Should().HaveCount(1);
            fetched.Files[0].Filename.Should().Be("checkout.png");
            fetched.Files[0].ContentType.Should().Be("image/png");
            fetched.Files[0].Bytes.Should().Equal(1, 2, 3, 4, 5);
        }
    }

    [Fact]
    public async Task ListForFeature_UnionsProjectScope_WithFeatureScope()
    {
        var projectId = await SeedProject(fixture);
        var nodeId = NodeId.New();

        await using (var ctx = fixture.CreateContext())
        {
            var projectScope = ProjectArtifact.CreateLink(
                projectId, null, ProjectArtifactKind.Figma, "Mocks",
                "https://figma.com/file/abc", null, null, Now);
            var featureScope = ProjectArtifact.CreateLink(
                projectId, nodeId, ProjectArtifactKind.ExternalUrl, "Notion brief",
                "https://notion.so/x", null, null, Now);
            var otherFeatureScope = ProjectArtifact.CreateLink(
                projectId, NodeId.New(), ProjectArtifactKind.ExternalUrl, "Unrelated",
                "https://other", null, null, Now);
            ctx.ProjectArtifacts.AddRange(projectScope, featureScope, otherFeatureScope);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var repo = new ProjectArtifactRepository(ctx);

            var unionRows = await repo.ListForFeatureAsync(projectId, nodeId);
            unionRows.Should().HaveCount(2);
            unionRows.Select(r => r.Label).Should().BeEquivalentTo(["Mocks", "Notion brief"]);

            var projectOnly = await repo.ListForFeatureAsync(projectId, nodeId: null);
            projectOnly.Should().HaveCount(1);
            projectOnly[0].Label.Should().Be("Mocks");
        }
    }

    [Fact]
    public async Task BlobStore_GetAsync_ReturnsNull_ForUnknownUri()
    {
        await using var ctx = fixture.CreateContext();
        var store = new SqlArtifactBlobStore(ctx, new FixedClock(Now));

        (await store.GetAsync("loom-artifact://does-not-exist")).Should().BeNull();
        (await store.GetAsync("https://wrong-scheme")).Should().BeNull();
        (await store.GetAsync("")).Should().BeNull();
    }

    private static async Task<Guid> SeedProject(MsSqlFixture f)
    {
        await using var ctx = f.CreateContext();
        var p = Project.Create(Slug.From($"proj-{Guid.NewGuid():N}".Substring(0, 20)), "Test", Now);
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }
}

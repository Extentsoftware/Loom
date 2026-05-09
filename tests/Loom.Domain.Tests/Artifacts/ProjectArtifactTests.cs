using FluentAssertions;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Xunit;

namespace Loom.Domain.Tests.Artifacts;

public sealed class ProjectArtifactTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Project = Guid.CreateVersion7();
    private static readonly NodeId Node = new(Guid.CreateVersion7());

    [Fact]
    public void CreateLink_populates_url_and_leaves_blob_null()
    {
        var a = ProjectArtifact.CreateLink(
            Project, nodeId: null, ProjectArtifactKind.Repo,
            "Existing checkout", "https://github.com/acme/checkout", description: null,
            createdByUserId: null, Now);

        a.Payload.Should().Be(ProjectArtifactPayload.Link);
        a.Url.Should().Be("https://github.com/acme/checkout");
        a.Blob.Should().BeNull();
        a.NodeId.Should().BeNull();
        a.ProjectId.Should().Be(Project);
    }

    [Fact]
    public void CreateLink_rejects_file_kinds()
    {
        var act = () => ProjectArtifact.CreateLink(
            Project, null, ProjectArtifactKind.Image, "Screenshot",
            "https://example.com/x.png", null, null, Now);

        act.Should().Throw<DomainException>().WithMessage("*carries bytes*");
    }

    [Fact]
    public void CreateFile_attaches_blob_and_optional_node_scope()
    {
        var blob = new BlobRef("loom-artifact://abc", "image/png", 4096);

        var a = ProjectArtifact.CreateFile(
            Project, Node, ProjectArtifactKind.Image,
            "Current checkout screenshot", blob,
            description: "Annotated by UX",
            createdByUserId: Guid.CreateVersion7(), Now);

        a.Payload.Should().Be(ProjectArtifactPayload.File);
        a.Blob.Should().Be(blob);
        a.Url.Should().BeNull();
        a.NodeId.Should().Be(Node);
        a.Description.Should().Be("Annotated by UX");
    }

    [Fact]
    public void CreateFile_rejects_link_kinds()
    {
        var blob = new BlobRef("loom-artifact://abc", "image/png", 4096);

        var act = () => ProjectArtifact.CreateFile(
            Project, null, ProjectArtifactKind.Repo, "x", blob, null, null, Now);

        act.Should().Throw<DomainException>().WithMessage("*link kind*");
    }

    [Fact]
    public void Create_methods_trim_whitespace_and_normalize_blank_description()
    {
        var a = ProjectArtifact.CreateLink(
            Project, null, ProjectArtifactKind.Figma,
            "  Mocks  ", "  https://figma.com/file/abc  ", "   ",
            null, Now);

        a.Label.Should().Be("Mocks");
        a.Url.Should().Be("https://figma.com/file/abc");
        a.Description.Should().BeNull();
    }
}

using FluentAssertions;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Xunit;

namespace Loom.Domain.Tests.Artifacts;

public sealed class ArtifactTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly NodeId TestNode = new(Guid.CreateVersion7());

    private static Artifact MakeArtifact() =>
        Artifact.Create(TestNode, ArtifactKind.Wireframe, "Checkout flow",
            new CanonicalPointer(CanonicalStore.HubNative, "checkout", null), Now);

    private static BlobRef SomeContent() =>
        new("loom-blob://artifact/checkout/v1", "image/png", 12345);

    [Fact]
    public void PublishVersion_numbers_monotonically_from_1()
    {
        var a = MakeArtifact();
        var v1 = a.PublishVersion(new Author(AuthorKind.Agent, Guid.CreateVersion7()), SomeContent(), null, "first", Now);
        var v2 = a.PublishVersion(new Author(AuthorKind.Human, Guid.CreateVersion7()), SomeContent(), null, "second", Now.AddMinutes(1));
        v1.VersionNumber.Should().Be(1);
        v2.VersionNumber.Should().Be(2);
        a.CurrentVersion.Should().Be(v2);
    }

    [Fact]
    public void Acquire_then_release_lock_round_trips()
    {
        var a = MakeArtifact();
        var user = Guid.CreateVersion7();
        a.AcquireLock(user, TimeSpan.FromMinutes(15), Now);
        a.Lock.Should().NotBeNull();
        a.Lock!.HolderUserId.Should().Be(user);

        a.ReleaseLock(user, Now.AddMinutes(1));
        a.Lock.Should().BeNull();
    }

    [Fact]
    public void Acquire_rejected_when_active_lock_held_by_other()
    {
        var a = MakeArtifact();
        a.AcquireLock(Guid.CreateVersion7(), TimeSpan.FromMinutes(15), Now);

        var act = () => a.AcquireLock(Guid.CreateVersion7(), TimeSpan.FromMinutes(15), Now.AddMinutes(1));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Acquire_replaces_expired_lock_held_by_other()
    {
        var a = MakeArtifact();
        a.AcquireLock(Guid.CreateVersion7(), TimeSpan.FromMinutes(15), Now);

        var second = Guid.CreateVersion7();
        a.AcquireLock(second, TimeSpan.FromMinutes(15), Now.AddHours(1));
        a.Lock!.HolderUserId.Should().Be(second);
    }

    [Fact]
    public void PublishVersion_rejected_for_human_when_locked_by_other()
    {
        var a = MakeArtifact();
        a.AcquireLock(Guid.CreateVersion7(), TimeSpan.FromMinutes(15), Now);

        var act = () => a.PublishVersion(
            new Author(AuthorKind.Human, Guid.CreateVersion7()),
            SomeContent(), null, "edit", Now.AddMinutes(1));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Agent_publishes_through_human_lock()
    {
        var a = MakeArtifact();
        a.AcquireLock(Guid.CreateVersion7(), TimeSpan.FromMinutes(15), Now);

        var v = a.PublishVersion(
            new Author(AuthorKind.Agent, Guid.CreateVersion7()),
            SomeContent(), null, "agent run", Now.AddMinutes(1));
        v.VersionNumber.Should().Be(1);
    }

    [Fact]
    public void Release_by_non_holder_when_active_throws()
    {
        var a = MakeArtifact();
        var holder = Guid.CreateVersion7();
        a.AcquireLock(holder, TimeSpan.FromMinutes(15), Now);

        var act = () => a.ReleaseLock(Guid.CreateVersion7(), Now.AddMinutes(1));
        act.Should().Throw<DomainException>();
    }
}

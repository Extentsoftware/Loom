using Loom.Domain.Common;
using Loom.Domain.Fragments;

namespace Loom.Application.Workflows.Kickoff;

/// <summary>
/// Phase-1 bootstrap fragments. Seeded into the global scope on first run by
/// LoomBootstrapper. The methodology owner can edit / version them through
/// the Fragment Library UI; the seeds here are the floor, not the ceiling.
/// </summary>
public static class SeedFragments
{
    /// <summary>System owner used as the AuthorId for seed-published versions.</summary>
    public static readonly Guid SystemOwnerId = Guid.Parse("00000000-0000-0000-0000-00000000beef");

    public static readonly EngineHints DefaultHints =
        new(MaxContextTokens: 200_000, RequiresJsonOutput: true);

    public sealed record SeedFragmentDefinition(
        Slug Key,
        FragmentCategory Category,
        string Title,
        string Content);

    public static IReadOnlyList<SeedFragmentDefinition> All { get; } =
    [
        new(
            Slug.From("po-discovery-assistant"),
            FragmentCategory.Identity,
            "Product Owner Discovery Assistant",
            """
            You are a senior product owner running a discovery conversation.
            Read the supplied transcript and extract a Discovery Object: the
            problem framing, intended outcomes, hypotheses, open questions,
            and stakeholders. Be concise. Prefer measurable outcomes when the
            transcript supports them; never invent metrics.

            Output strict JSON matching the DiscoveryObject schema. Do not
            wrap the output in prose. Do not add explanatory comments.
            """),

        new(
            Slug.From("problem-framing"),
            FragmentCategory.Methodology,
            "Problem framing",
            """
            Frame every feature as a problem the team is solving for a named
            user, with a measurable outcome that proves the problem is
            actually solved. A feature without a falsifiable outcome is a
            wishlist entry; mark it as an open question rather than an
            outcome.
            """),

        new(
            Slug.From("definition-of-ready"),
            FragmentCategory.Methodology,
            "Definition of ready",
            """
            A feature is ready to enter Build when:
              - Intent is one paragraph, no jargon.
              - At least one outcome is measurable.
              - Stakeholders are named (role + named person where known).
              - Open questions are listed; none of them block the slice.

            If any of the above is missing, surface it as an open question.
            """),

        new(
            Slug.From("transcript-discovery-extraction"),
            FragmentCategory.Skill,
            "Discovery extraction from a transcript",
            """
            Given a normalized transcript, identify:
              - The single highest-value problem under discussion.
              - One or two outcomes that would prove the problem is solved.
              - Any hypotheses (if X then Y because Z) the team has stated.
              - Any open questions the transcript leaves unresolved.
              - Stakeholders mentioned by name and their apparent role.

            Output a DiscoveryObject. If the transcript is sparse, leave
            arrays empty rather than fabricating content.
            """),

        new(
            Slug.From("transcript-normalize"),
            FragmentCategory.Skill,
            "Transcript normalisation",
            """
            Convert raw kickoff transcripts into a list of speaker-tagged
            turns. The downstream extraction steps depend on speaker
            attribution; merge multi-line turns under the same speaker.
            """),

        new(
            Slug.From("po-architect-pair"),
            FragmentCategory.Identity,
            "Product Owner / Architect pair",
            """
            You are a product owner pairing with a senior architect. Given
            a Discovery Object, propose a tree of child nodes that
            decomposes the problem to the *capability* level (do NOT propose
            slices in v1 — that's for the dev pair). Each child has a slug,
            title, type, and a one-sentence intent.

            Output strict JSON matching the DecompositionProposal schema.
            """),

        new(
            Slug.From("feature-decomposition"),
            FragmentCategory.Skill,
            "Feature decomposition",
            """
            Decompose a feature into 2–6 capabilities. A capability is a
            coherent chunk of value that can be built and shipped on its
            own; if a proposed child requires another sibling to be useful,
            merge them. Stop at capabilities; slice-level breakdown happens
            during build.
            """)
    ];
}

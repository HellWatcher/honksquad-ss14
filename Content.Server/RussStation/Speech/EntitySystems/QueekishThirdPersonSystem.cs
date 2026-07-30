// HONK - Issue #481 P5: head-Skaven third-person speech ("Thanquol-speak"). Rewrites
// first-person pronouns to the speaker's first name when a Skaven on a command job speaks,
// e.g. "I am leaving" -> "Thanquol is leaving". Verb conjugation isn't fixed up; the broken
// grammar reads as authentic posturing.
//
// Runs `before` ReplacementAccentSystem so the pronoun swap lands first and the standard
// Queekish word replacements then operate on the resulting text. Species-guarded to Skaven
// only via HumanoidProfileComponent so the marker is harmless if it lands on someone else.
//
// Upstream #43008 moved every accent onto RelayAccentSystem<T>, which owns the (now by-ref)
// AccentGetEvent subscription plus the inventory/status-effect relay paths, so this system
// derives from it instead of hand-rolling its own subscription.

using System.Text.RegularExpressions;
using Content.Server.RussStation.Speech;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Speech.EntitySystems;

public sealed class QueekishThirdPersonSystem : RelayAccentSystem<QueekishThirdPersonComponent>
{
    private static readonly ProtoId<SpeciesPrototype> SkavenSpecies = "Skaven";

    private static readonly Regex Pronouns =
        new(@"\b(?:I|me|my|mine|myself)\b", RegexOptions.IgnoreCase);

    protected override Type[]? AccentBefore => [typeof(ReplacementAccentSystem)];

    public override string Accentuate(string message, Entity<QueekishThirdPersonComponent>? ent = null)
    {
        if (ent == null)
            return message;

        var uid = ent.Value.Owner;

        // Species guard. Component lives on the entity but the marker is only meaningful for
        // Skaven; if a job add-component path attaches it to a non-Skaven we silently skip.
        if (!TryComp<HumanoidProfileComponent>(uid, out var profile) || profile.Species != SkavenSpecies)
            return message;

        var firstName = ResolveFirstName(uid);
        if (string.IsNullOrEmpty(firstName))
            return message;

        return Pronouns.Replace(message, firstName);
    }

    private string ResolveFirstName(EntityUid uid)
    {
        // Skaven hyphenate (e.g. "Thanquol-Boneripper") so split on space and hyphen and
        // take the first segment. Falls back to the full name if there's no separator.
        var name = MetaData(uid).EntityName;
        var sep = name.IndexOfAny([' ', '-']);
        return sep < 0 ? name : name[..sep];
    }
}

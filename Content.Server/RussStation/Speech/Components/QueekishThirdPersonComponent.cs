// HONK - Issue #481 P5: head-Skaven third-person speech ("Thanquol-speak").
// Marker component attached to Skaven on command jobs (Captain, HoP, HoS, CE, CMO, RD, QM)
// so first-person pronouns get rewritten to the speaker's first name before the standard
// Queekish word replacement runs. Lore-flavour: clan-leadership posturing.
//
// Derives from BaseAccentComponent (upstream #43008) so QueekishThirdPersonSystem can use
// RelayAccentSystem<T>. RelayAccent stays at its inherited default of false: the pronoun
// swap is a property of the speaker, not of worn clothing.

using Content.Shared.Speech.Components;

namespace Content.Server.RussStation.Speech;

[RegisterComponent]
public sealed partial class QueekishThirdPersonComponent : BaseAccentComponent;

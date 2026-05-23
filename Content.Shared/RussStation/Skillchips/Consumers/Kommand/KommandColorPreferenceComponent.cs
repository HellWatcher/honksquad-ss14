using Robust.Shared.GameStates;

namespace Content.Shared.RussStation.Skillchips.Consumers.Kommand;

/// <summary>
/// Per-mob preference set by the Kommand chip's color picker BUI. The chip
/// itself only persists as a prototype id inside <c>SkillchipHolderComponent.ImplantedChips</c>
/// (there is no live chip entity to hang state on), so the chosen color rides
/// on the mob and gets read at point time. Added lazily the first time the
/// holder opens the picker; survives chip removal so reinstalling restores
/// the last color the user picked.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KommandColorPreferenceComponent : Component
{
    /// <summary>SS13's default pointer tint was red; the player can change it from the chip's action.</summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.Red;
}

using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;

namespace Content.Shared.RussStation.Eye;

// Shared apply/remove dance for components that act as a "blindness source"
// (ScarredEye, PermanentBlindness, etc). Set MinDamage on init, clear it and
// heal residual EyeDamage on shutdown so cures leave the eye fully healed.
public static class BlindnessSourceHelper
{
    public static void Apply(IEntityManager entMan, BlindableSystem blinding, EntityUid uid, int damage)
    {
        if (!entMan.TryGetComponent<BlindableComponent>(uid, out var blindable))
            return;

        blinding.SetMinDamage((uid, blindable), damage);
    }

    public static void Remove(IEntityManager entMan, BlindableSystem blinding, EntityUid uid)
    {
        if (!entMan.TryGetComponent<BlindableComponent>(uid, out var blindable))
            return;

        if (blindable.MinDamage != EyeConstants.ClearedMinDamage)
            blinding.SetMinDamage((uid, blindable), EyeConstants.ClearedMinDamage);

        blinding.AdjustEyeDamage((uid, blindable), -blindable.EyeDamage);
    }
}

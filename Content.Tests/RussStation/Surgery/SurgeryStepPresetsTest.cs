using Content.Shared.RussStation.Surgery;
using NUnit.Framework;

namespace Content.Tests.RussStation.Surgery;

/// <summary>
/// Pure unit tests for <see cref="SurgeryStepPresets.Resolve"/>. These guard the preset table from
/// accidental changes: any edit that shifts the surgery numbers or swaps tool qualities breaks
/// these deliberately so the author has to update both.
/// </summary>
[TestFixture]
public sealed class SurgeryStepPresetsTest
{
    [Test]
    public void IncisionPreset_UsesSlicingQualityAndIncisionBleed()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.Incision);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Quality.Id, Is.EqualTo("Slicing"));
            Assert.That(defaults.Duration, Is.EqualTo(SurgeryConstants.IncisionDuration));
            Assert.That(defaults.BleedPreset, Is.EqualTo(SurgeryBleedPreset.Incision));
            Assert.That(defaults.Repeatable, Is.False);
        });
    }

    [Test]
    public void ClampPreset_UsesClampingQualityAndFullBleedReversal()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.Clamp);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Quality.Id, Is.EqualTo("Clamping"));
            Assert.That(defaults.Duration, Is.EqualTo(SurgeryConstants.ClampDuration));
            Assert.That(defaults.BleedPreset, Is.EqualTo(SurgeryBleedPreset.ClampFull));
            Assert.That(defaults.Effect, Is.Null);
        });
    }

    [Test]
    public void RetractPreset_UsesRetractingQualityAndBluntDamage()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.Retract);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Quality.Id, Is.EqualTo("Retracting"));
            Assert.That(defaults.Duration, Is.EqualTo(SurgeryConstants.RetractDuration));
            Assert.That(defaults.Damage?.DamageDict.ContainsKey("Blunt"), Is.True);
        });
    }

    [Test]
    public void SawPreset_UsesSawingQualityAndSlashDamage()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.Saw);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Quality.Id, Is.EqualTo("Sawing"));
            Assert.That(defaults.Duration, Is.EqualTo(SurgeryConstants.SawDuration));
            Assert.That(defaults.Damage?.DamageDict.ContainsKey("Slash"), Is.True);
        });
    }

    [Test]
    public void TendBrutePreset_HasHealingBudgetAndRepeats()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.TendBrute);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Quality.Id, Is.EqualTo("Clamping"));
            Assert.That(defaults.Duration, Is.EqualTo(SurgeryConstants.TendStepDuration));
            Assert.That(defaults.Repeatable, Is.True);
            Assert.That(defaults.HealingFlat, Is.EqualTo(SurgeryConstants.TendHealingFlat));
            Assert.That(defaults.HealingMultiplier, Is.EqualTo(SurgeryConstants.TendHealingMultiplier));
            Assert.That(defaults.Healing, Is.Not.Null);
            Assert.That(defaults.Healing!.DamageDict.Keys, Does.Contain("Blunt"));
        });
    }

    [Test]
    public void TendBurnPreset_HealsBurnDamageTypes()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.TendBurn);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Healing, Is.Not.Null);
            Assert.That(defaults.Healing!.DamageDict.Keys, Does.Contain("Heat"));
            Assert.That(defaults.Healing.DamageDict.Keys, Does.Not.Contain("Blunt"));
            Assert.That(defaults.Repeatable, Is.True);
        });
    }

    [Test]
    public void RemoveOrganPreset_CarriesOrganEffect()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.RemoveOrgan);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Quality.Id, Is.EqualTo("Clamping"));
            Assert.That(defaults.Repeatable, Is.True);
            Assert.That(defaults.Effect, Is.Not.Null);
        });
    }

    [Test]
    public void CauterizeBurnWoundsPreset_UsesCauterizingAndClearsBurns()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.CauterizeBurnWounds);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Quality.Id, Is.EqualTo("Cauterizing"));
            Assert.That(defaults.Duration, Is.EqualTo(SurgeryConstants.WoundRepairDuration));
            Assert.That(defaults.Effect, Is.Not.Null);
        });
    }

    [Test]
    public void SetBonesPreset_UsesBoneSettingAndClearsFractures()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.SetBones);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Quality.Id, Is.EqualTo("BoneSetting"));
            Assert.That(defaults.Duration, Is.EqualTo(SurgeryConstants.WoundRepairDuration));
            Assert.That(defaults.Effect, Is.Not.Null);
        });
    }

    [Test]
    public void NonePreset_ReturnsEmptyDefaults()
    {
        var defaults = SurgeryStepPresets.Resolve(SurgeryStepPreset.None);

        Assert.Multiple(() =>
        {
            Assert.That(defaults.Duration, Is.Null);
            Assert.That(defaults.Damage, Is.Null);
            Assert.That(defaults.Healing, Is.Null);
            Assert.That(defaults.Effect, Is.Null);
            Assert.That(defaults.Repeatable, Is.False);
        });
    }
}

using System.Collections.Generic;
using Content.Server.RussStation.Messenger;
using NUnit.Framework;

namespace Content.Tests.Server.RussStation.Messenger;

[TestFixture, TestOf(typeof(AntagAddressFilter))]
[Parallelizable(ParallelScope.All)]
public sealed class AntagAddressFilterTest
{
    private static readonly AntagAddressFilter Filter = AntagAddressFilter.Default;

    [Test]
    public void GetAddressPrefix_PlainPda_ReturnsCrewPrefix()
    {
        Assert.That(Filter.GetAddressPrefix("PDA"), Is.EqualTo(AntagAddressFilter.CrewAddressPrefix));
        Assert.That(Filter.GetAddressPrefix("Captain PDA"), Is.EqualTo(AntagAddressFilter.CrewAddressPrefix));
    }

    [Test]
    public void GetAddressPrefix_AntagPda_ReturnsCategoryPrefix()
    {
        Assert.That(Filter.GetAddressPrefix("Syndicate PDA"), Is.EqualTo("SY"));
        Assert.That(Filter.GetAddressPrefix("Space Ninja PDA"), Is.EqualTo("NJ"));
        Assert.That(Filter.GetAddressPrefix("Pirate PDA"), Is.EqualTo("PR"));
        Assert.That(Filter.GetAddressPrefix("Wizard PDA"), Is.EqualTo("WZ"));
    }

    [Test]
    public void GetAddressPrefix_IsCaseInsensitive()
    {
        Assert.That(Filter.GetAddressPrefix("syndicate pda"), Is.EqualTo("SY"));
        Assert.That(Filter.GetAddressPrefix("SYNDICATE PDA"), Is.EqualTo("SY"));
    }

    [Test]
    public void IsAntagAddress_AntagPrefixedAddresses_AreAntag()
    {
        Assert.That(Filter.IsAntagAddress("SY1234"), Is.True);
        Assert.That(Filter.IsAntagAddress("NJABCD"), Is.True);
        Assert.That(Filter.IsAntagAddress("CB0001"), Is.True);
    }

    [Test]
    public void IsAntagAddress_CrewAddress_IsNotAntag()
    {
        Assert.That(Filter.IsAntagAddress("NT1234"), Is.False);
    }

    [Test]
    public void CustomPrefixes_AreRespected()
    {
        var custom = new AntagAddressFilter(
            new Dictionary<string, string> { { "revolution", "RV" } },
            crewPrefix: "ZZ");

        Assert.That(custom.CrewPrefix, Is.EqualTo("ZZ"));
        Assert.That(custom.GetAddressPrefix("Revolution PDA"), Is.EqualTo("RV"));
        Assert.That(custom.GetAddressPrefix("Captain PDA"), Is.EqualTo("ZZ"));
        Assert.That(custom.IsAntagAddress("RV9999"), Is.True);
        // The default antag prefixes no longer apply to a custom map.
        Assert.That(custom.IsAntagAddress("SY1234"), Is.False);
    }
}

using System.Globalization;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class InvariantHashTests
{
    [Theory]
    [InlineData("")]
    [InlineData("vehicle_layout_STANDARD")]
    [InlineData("Common:/Data/Levels/GTA5/VehicleLayouts.meta")]
    [InlineData("A\0Z_09:/.-")]
    [InlineData("ÉCOLE_İIı_Σςσ_漢字")]
    [InlineData("\U00010400\U00010427\U0001F600")]
    [InlineData("\ud800ABC\udfff")]
    public void LowercaseHashMatchesExistingStringAlgorithm(string text)
    {
        Assert.Equal(JenkHash.GenHash(text.ToLowerInvariant()), JenkHash.GenHashLowerInvariant(text));
        var padded = "[" + text + "]";
        Assert.Equal(JenkHash.GenHash(text.ToLowerInvariant()),
            JenkHash.GenHashLowerInvariant(padded.AsSpan(1, text.Length)));
    }

    [Fact]
    public void UnicodeCasingMatchesAcrossStackAndPoolBoundaries()
    {
        foreach (int length in new[] { 255, 256, 257, 4096 })
        {
            string text = new string('A', length - 4) + "É\U00010400\udfff";
            Assert.Equal(JenkHash.GenHash(text.ToLowerInvariant()), JenkHash.GenHashLowerInvariant(text));
        }

        string allCharacters = new(Enumerable.Range(0, 65536).Select(i => (char)i).ToArray());
        Assert.Equal(JenkHash.GenHash(allCharacters.ToLowerInvariant()), JenkHash.GenHashLowerInvariant(allCharacters));
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            string text = new(Enumerable.Range(0, random.Next(1, 800)).Select(_ => (char)random.Next(65536)).ToArray());
            Assert.Equal(JenkHash.GenHash(text.ToLowerInvariant()), JenkHash.GenHashLowerInvariant(text));
        }
    }

    [Fact]
    public void CasingIsIndependentOfCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(JenkHash.GenHash("identifier"), JenkHash.GenHashLowerInvariant("IDENTIFIER"));
            Assert.Equal(0u, JenkHash.GenHashLowerInvariant(default));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void MetadataNamesMatchExistingEnumBehaviorIncludingAliases()
    {
        foreach (var value in Enum.GetValues<MetaName>())
        {
            Assert.True(MetaNames.TryGetString((uint)value, out string? actual));
            string expected = value.ToString();
            if (expected.StartsWith('@')) expected = expected[1..];
            Assert.Equal(expected, actual);
        }

        var random = new Random(37);
        for (int i = 0; i < 1000; i++)
        {
            uint value = (uint)random.NextInt64(0, (long)uint.MaxValue + 1);
            bool found = MetaNames.TryGetString(value, out string? actual);
            Assert.Equal(Enum.IsDefined(typeof(MetaName), value), found);
            if (!found) Assert.Null(actual);
        }
    }
}

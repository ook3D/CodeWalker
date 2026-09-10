using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class TimeArchetypeTests
{
    [Fact]
    public void UninitializedArchetypeIsActive()
    {
        var archetype = new TimeArchetype();

        Assert.True(archetype.IsActive(0));
        Assert.True(archetype.IsActive(23.5f));
    }

    [Fact]
    public void InitPopulatesAllHoursFromTimeFlags()
    {
        var definition = new CTimeArchetypeDef();
        definition._TimeArchetypeDef.timeFlags = (1u << 0) | (1u << 23) | (1u << 24);
        var archetype = new TimeArchetype();

        archetype.Init(new YtypFile(), ref definition);

        Assert.Equal(24, archetype.ActiveHours.Length);
        Assert.Equal(24, archetype.ActiveHoursText.Length);
        for (int hour = 0; hour < 24; hour++)
        {
            Assert.Equal(hour is 0 or 23, archetype.IsActive(hour + 0.5f));
        }
        Assert.Equal("00:00 - 01:00 - On", archetype.ActiveHoursText[0]);
        Assert.Equal("01:00 - 02:00 - Off", archetype.ActiveHoursText[1]);
        Assert.Equal("23:00 - 00:00 - On", archetype.ActiveHoursText[23]);
        Assert.True(archetype.ExtraFlag);
        Assert.True(archetype.IsActive(24));
    }

    [Fact]
    public void SetTimeFlagsRefreshesExistingArrays()
    {
        var archetype = new TimeArchetype();
        archetype.SetTimeFlags(0xFFFFFF);
        var hours = archetype.ActiveHours;
        var text = archetype.ActiveHoursText;

        archetype.SetTimeFlags(1u << 12);

        Assert.Same(hours, archetype.ActiveHours);
        Assert.Same(text, archetype.ActiveHoursText);
        Assert.Equal(1u << 12, archetype.TimeArchetypeDef.TimeArchetypeDef.timeFlags);
        for (int hour = 0; hour < 24; hour++)
        {
            Assert.Equal(hour == 12, archetype.IsActive(hour));
        }
        Assert.Equal("00:00 - 01:00 - Off", text[0]);
        Assert.Equal("12:00 - 13:00 - On", text[12]);
    }

    [Theory]
    [InlineData(0, 24)]
    [InlineData(24, 0)]
    [InlineData(1, 25)]
    public void UpdateRepairsEachArrayIndependently(int hourCount, int textCount)
    {
        var archetype = new TimeArchetype
        {
            ActiveHours = new bool[hourCount],
            ActiveHoursText = new string[textCount],
            TimeFlags = 1u << 23,
        };

        archetype.UpdateActiveHours();

        Assert.Equal(24, archetype.ActiveHours.Length);
        Assert.Equal(24, archetype.ActiveHoursText.Length);
        Assert.True(archetype.IsActive(23));
        Assert.Equal("23:00 - 00:00 - On", archetype.ActiveHoursText[23]);
    }
}

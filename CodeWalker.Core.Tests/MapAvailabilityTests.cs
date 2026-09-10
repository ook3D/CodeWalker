using System.Reflection;
using CodeWalker.GameFiles;
using CodeWalker.World;
using Xunit;

namespace CodeWalker.Core.Tests;

public class MapAvailabilityTests
{
    [Fact]
    public void EmptyWeatherListDoesNotHideMap()
    {
        var space = CreateSpace(new YmfMapDataGroup { Name = 123 });

        Assert.True(IsAvailable(space, 12, 456));
        Assert.True(IsAvailable(space, 0, 789));
    }

    [Fact]
    public void ExplicitWeatherRestrictionsAreRespected()
    {
        var space = CreateSpace(new YmfMapDataGroup { Name = 123, WeatherTypes = [456, 789] });

        Assert.True(IsAvailable(space, 12, 456));
        Assert.True(IsAvailable(space, 12, 789));
        Assert.False(IsAvailable(space, 12, 999));
        Assert.True(IsAvailable(space, 12, 0));
    }

    [Fact]
    public void EmptyWeatherListPreservesTimeRestriction()
    {
        var space = CreateSpace(new YmfMapDataGroup { Name = 123, HoursOnOff = 1u << 12 });

        Assert.True(IsAvailable(space, 12, 456));
        Assert.False(IsAvailable(space, 11, 456));
    }

    private static Space CreateSpace(YmfMapDataGroup group)
    {
        var cache = new GameFileCache(1024, 10, "", false, "", false, "");
        cache.AllManifests.Add(new YmfFile { MapDataGroups = [group] });
        var space = new Space();
        typeof(Space).GetField("gameFileCache", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(space, cache);
        typeof(Space).GetMethod("InitManifestData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(space, null);
        return space;
    }

    private static bool IsAvailable(Space space, int hour, MetaHash weather) =>
        (bool)typeof(Space).GetMethod("IsYmapAvailable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(space, [123u, hour, weather])!;
}

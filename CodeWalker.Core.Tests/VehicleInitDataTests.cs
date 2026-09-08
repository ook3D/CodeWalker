using System.Threading.Tasks;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class VehicleInitDataTests
{
    [Fact]
    public void ParallelLoadsKeepVehicleArraysIsolated()
    {
        Parallel.For(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var document = new XmlDocument();
            document.LoadXml($"""
                <Item>
                    <lodDistances> {i}
                invalid
                {i + 1}
                </lodDistances>
                    <flags> FLAG_{i}  COMMON </flags>
                    <trailers><Item>trailer_{i}</Item><Item></Item><Item>shared</Item></trailers>
                </Item>
                """);
            var vehicle = new VehicleInitData();

            vehicle.Load(document.DocumentElement!);

            Assert.Equal(new float[] { i, i + 1 }, vehicle.lodDistances);
            Assert.Equal(new[] { $"FLAG_{i}", "COMMON" }, vehicle.flags);
            Assert.Equal(new[] { $"trailer_{i}", "shared" }, vehicle.trailers);
        });
    }
}

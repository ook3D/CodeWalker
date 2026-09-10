using System.Globalization;
using System.Text;
using Xunit;

namespace CodeWalker.Core.Tests;

public class FbxParsingTests
{
    [Theory]
    [InlineData("1.123456")]
    [InlineData("1.1234567")]
    [InlineData("-1.123456e+3")]
    [InlineData("1.1234567E-3")]
    [InlineData("0.5")]
    [InlineData("1.")]
    public void DecimalNumbersPreservePrecisionSelection(string text)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("; FBX 7.4.0 project file\nValue: " + text + " {\n}\n"));
            var document = new FbxAsciiReader(stream).Read();
            var actual = document["Value"]!.Value;
            if (text.Split('.', 'e', 'E')[1].Length > 6)
                Assert.Equal(double.Parse(text), Assert.IsType<double>(actual));
            else
                Assert.Equal(float.Parse(text), Assert.IsType<float>(actual));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NodeLookupPreservesFirstMatchAndPathBehavior()
    {
        var document = new FbxDocument();
        var parent = new FbxNode { Name = "Objects" };
        var first = new FbxNode { Name = "Model" };
        parent.Nodes.AddRange([null, first, new FbxNode { Name = "Model" }, new FbxNode { Name = "模型" }]);
        document.Nodes.AddRange([null, parent, new FbxNode { Name = "Objects" }]);

        foreach (var root in new FbxNodeList[] { document, parent })
        {
            foreach (var path in new[] { "", "/", "///", "Objects", "/Objects//Model/", "Objects/模型", "Objects/model", "Objects/Missing/Model", "Model", "模型" })
            {
                FbxNodeList? expected = root;
                foreach (var part in path.Split('/'))
                {
                    if (part.Length == 0) continue;
                    expected = expected.Nodes.Find(n => n != null && n.Name == part);
                    if (expected == null) break;
                }
                Assert.Same(expected as FbxNode, root.GetRelative(path));
            }
            Assert.Throws<NullReferenceException>(() => root.GetRelative(null!));
        }
        Assert.Same(first, parent["Model"]);
        Assert.Null(parent["model"]);
        Assert.Null(parent[null!]);
        Assert.Same(first, document.GetRelative("Objects/Model"));
    }
}

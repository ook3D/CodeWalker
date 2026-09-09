using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class XmlMetaParsingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void NumericArraysPreservePointersResourceBytesAndErrors(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var whitespace = new string(Enumerable.Range(0, 65536).Select(i => (char)i).Where(char.IsWhiteSpace).ToArray());
            foreach (var kind in new[] { "UInt", "UShort", "UByte", "Float" })
            {
                var method = typeof(XmlMeta).GetMethod($"TraverseRaw{kind}Array", BindingFlags.NonPublic | BindingFlags.Static)!;
                foreach (var text in new[] { "", whitespace, "0" + whitespace + "1 255", "256 65535", "4294967295", "4294967296", "+1 -0", "-1", "1.25 -2e3 NaN Infinity -Infinity", "1,234", "invalid", "12\u200b34" })
                {
                    var doc = new XmlDocument();
                    var node = doc.CreateElement("Item");
                    node.InnerText = text;
                    var expectedBuilder = new MetaBuilder();
                    var actualBuilder = new MetaBuilder();
                    object? expectedPointer = null;
                    var expectedError = Record.Exception(() => expectedPointer = PreviousParser(text, kind, expectedBuilder));
                    if (expectedError != null)
                    {
                        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [node, actualBuilder]));
                        Assert.Equal(expectedError.GetType(), error.InnerException!.GetType());
                        Assert.Equal(0, actualBuilder.GetMeta().DataBlocksCount);
                        continue;
                    }
                    Assert.Equal(expectedPointer, method.Invoke(null, [node, actualBuilder]));
                    var expected = expectedBuilder.GetMeta();
                    var actual = actualBuilder.GetMeta();
                    Assert.Equal(expected.DataBlocksCount, actual.DataBlocksCount);
                    for (int i = 0; i < expected.DataBlocksCount; i++)
                    {
                        Assert.Equal(expected.DataBlocks![i].StructureNameHash, actual.DataBlocks![i].StructureNameHash);
                        Assert.Equal(expected.DataBlocks[i].Data, actual.DataBlocks[i].Data);
                    }
                }
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static object PreviousParser(string text, string kind, MetaBuilder builder)
    {
        var tokens = Regex.Split(text, @"[\s\r\n\t]").Where(s => s.Length != 0);
        return kind switch
        {
            "UInt" => builder.AddUintArrayPtr(tokens.Select(s => Convert.ToUInt32(s)).ToArray()),
            "UShort" => builder.AddUshortArrayPtr(tokens.Select(s => Convert.ToUInt16(s)).ToArray()),
            "UByte" => builder.AddByteArrayPtr(tokens.Select(s => Convert.ToByte(s)).ToArray()),
            "Float" => builder.AddFloatArrayPtr(tokens.Select(s => FloatUtil.Parse(s.Trim())).ToArray()),
            _ => throw new ArgumentException(nameof(kind))
        };
    }
}

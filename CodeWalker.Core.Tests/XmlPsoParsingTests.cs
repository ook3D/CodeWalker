using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class XmlPsoParsingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void NumericArraysPreserveValuesByteOrderAndErrors(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var whitespace = new string(Enumerable.Range(0, 65536).Select(i => (char)i).Where(char.IsWhiteSpace).ToArray());
            foreach (var kind in new[] { "SInt", "UInt", "UByte", "UShort", "Float" })
            {
                var method = typeof(XmlPso).GetMethod($"Traverse{kind}ArrayRaw", BindingFlags.NonPublic | BindingFlags.Static)!;
                foreach (var text in new[] { "", whitespace, "0" + whitespace + "1 127 255", "256 65535", "-2147483648 2147483647", "4294967295", "4294967296", "+1 -0", "-1", "1.25 -2e3 NaN Infinity -Infinity", "1,234", "invalid", "12\u200b34" })
                {
                    var doc = new XmlDocument();
                    var node = doc.CreateElement("Item");
                    node.InnerText = text;
                    Array? expected = null;
                    var expectedError = Record.Exception(() => expected = PreviousParser(text, kind));
                    if (expectedError != null)
                    {
                        var actualError = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [node]));
                        Assert.Equal(expectedError.GetType(), actualError.InnerException!.GetType());
                        continue;
                    }
                    var actual = Assert.IsAssignableFrom<Array>(method.Invoke(null, [node]));
                    Assert.Equal(expected!.GetType(), actual.GetType());
                    Assert.Equal(Bytes(expected), Bytes(actual));
                }
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static Array PreviousParser(string text, string kind)
    {
        var tokens = Regex.Split(text, @"[\s\r\n\t]").Where(s => s.Length != 0);
        return kind switch
        {
            "SInt" => tokens.Select(s => BinaryPrimitives.ReverseEndianness(Convert.ToInt32(s))).ToArray(),
            "UInt" => tokens.Select(s => BinaryPrimitives.ReverseEndianness(Convert.ToUInt32(s))).ToArray(),
            "UByte" => tokens.Select(s => Convert.ToByte(s)).ToArray(),
            "UShort" => tokens.Select(s => BinaryPrimitives.ReverseEndianness(Convert.ToUInt16(s))).ToArray(),
            "Float" => tokens.Select(s => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(BitConverter.SingleToInt32Bits(FloatUtil.Parse(s))))).ToArray(),
            _ => throw new ArgumentException(nameof(kind))
        };
    }

    private static byte[] Bytes(Array array)
    {
        var result = new byte[Buffer.ByteLength(array)];
        Buffer.BlockCopy(array, 0, result, 0, result.Length);
        return result;
    }
}

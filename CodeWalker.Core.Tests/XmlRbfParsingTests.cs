using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class XmlRbfParsingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void NumericArraysMatchPreviousParser(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var whitespace = new string(Enumerable.Range(0, 65536).Select(i => (char)i).Where(char.IsWhiteSpace).ToArray());
            foreach (var shorts in new[] { false, true })
            {
                var method = typeof(XmlRbf).GetMethod(shorts ? "GetUshortArray" : "GetByteArray", BindingFlags.NonPublic | BindingFlags.Static)!;
                foreach (var text in new[] { "", whitespace, "0" + whitespace + "1 255", "256 65535", "+1 -0", "-1", "65536", "1,000", "invalid", "12\u200b34" })
                {
                    byte[]? expected = null;
                    var expectedError = Record.Exception(() =>
                    {
                        if (!shorts && text.Length == 0) return;
                        var tokens = Regex.Split(text, @"[\s\r\n\t]").Where(s => s.Length != 0);
                        expected = shorts
                            ? tokens.Select(s => Convert.ToUInt16(s)).SelectMany(v => new[] { (byte)(v & 255), (byte)(v >> 8) }).ToArray()
                            : tokens.Select(s => Convert.ToByte(s)).ToArray();
                    });
                    if (expectedError == null)
                        Assert.Equal(expected, (byte[]?)method.Invoke(null, [text]));
                    else
                    {
                        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [text]));
                        Assert.Equal(expectedError.GetType(), error.InnerException!.GetType());
                    }
                }
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextKeepsAsciiReplacementAndNullTerminator(bool attributed)
    {
        const string text = "Model café 模型 😀";
        var doc = new XmlDocument();
        var root = doc.CreateElement("Root");
        doc.AppendChild(root);
        if (attributed) root.SetAttribute("kind", "text");
        root.InnerText = text;
        var rbf = XmlRbf.GetRbf(doc);
        var bytes = Assert.IsType<RbfBytes>(Assert.Single(rbf.current!.Children));
        Assert.Equal(Encoding.ASCII.GetBytes(text).Concat(new byte[] { 0 }), bytes.Value);
    }

    [Theory]
    [InlineData("char_array", "0 1 255", new byte[] { 0, 1, 255 })]
    [InlineData("short_array", "0 256 65535", new byte[] { 0, 0, 0, 1, 255, 255 })]
    public void ImportedArraysKeepTheirByteOrder(string content, string text, byte[] expected)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"<Root content=\"{content}\">{text}</Root>");
        var rbf = XmlRbf.GetRbf(doc);
        Assert.Equal(expected, Assert.IsType<RbfBytes>(Assert.Single(rbf.current!.Children)).Value);
    }
}

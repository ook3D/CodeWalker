using SharpDX;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CodeWalker.GameFiles
{
    public class XmlRbf
    {

        public static RbfFile GetRbf(XmlDocument doc)
        {
            var rbf = new RbfFile();

            using (var reader = new XmlNodeReader(doc))
            {
                reader.MoveToContent();
                var root = XDocument.Load(reader).Root
                    ?? throw new XmlException("The RBF document must have a root element.");
                rbf.current = Traverse(root) as RbfStructure
                    ?? throw new XmlException("The RBF root must be a structure.");
            }

            return rbf;
        }

        private static IRbfType? Traverse(XNode node)
        {
            if (node is XElement element)
            {
                if (element.Attribute("value") is { } valueAttribute)
                {
                    var val = valueAttribute.Value;
                    if (!string.IsNullOrEmpty(val))
                    {
                        var rval = CreateValueNode(element.Name.LocalName, val);
                        if (rval != null)
                        {
                            return rval;
                        }
                    }
                }
                else if ((element.Attributes().Count() == 3) && (element.Attribute("x") is { } xAttribute) && (element.Attribute("y") is { } yAttribute) && (element.Attribute("z") is { } zAttribute))
                {
                    FloatUtil.TryParse(xAttribute.Value, out float x);
                    FloatUtil.TryParse(yAttribute.Value, out float y);
                    FloatUtil.TryParse(zAttribute.Value, out float z);
                    return new RbfFloat3()
                    {
                        Name = element.Name.LocalName,
                        X = x,
                        Y = y,
                        Z = z
                    };
                }
                else if ((element.Elements().Count() == 0) && (element.Attributes().Count() == 0) && (!element.IsEmpty)) //else if (element.Name == "type" || element.Name == "key" || element.Name == "platform")
                {
                    var bytes = new RbfBytes() { Value = GetNullTerminatedAscii(element.Value) };
                    var struc = new RbfStructure() { Name = element.Name.LocalName };
                    struc.Children.Add(bytes);
                    return struc;
                }

                var n = new RbfStructure();
                n.Name = element.Name.LocalName;
                n.Children = new List<IRbfType>();
                foreach (var c in element.Nodes())
                {
                    var child = Traverse(c);
                    if (child == null) continue;
                    n.Children.Add(child);
                }

                foreach (var attr in element.Attributes())
                {
                    var val = attr.Value;
                    var aval = CreateValueNode(attr.Name.LocalName, val);
                    if (aval != null)
                    {
                        n.Attributes.Add(aval);
                    }
                }

                return n;
            }
            else if (node is XText text)
            {
                byte[]? bytes = null;
                var contentAttr = node.Parent?.Attribute("content");
                if (contentAttr != null)
                {
                    if (contentAttr.Value == "char_array")
                    {
                        bytes = GetByteArray(text.Value);
                    }
                    else if (contentAttr.Value == "short_array")
                    {
                        bytes = GetUshortArray(text.Value);
                    }
                    else
                    { }
                }
                else
                {
                    bytes = GetNullTerminatedAscii(text.Value);
                }
                if (bytes != null)
                {
                    return new RbfBytes()
                    {
                        Name = "",
                        Value = bytes
                    };
                }
            }

            return null;
        }


        private static IRbfType CreateValueNode(string name, string val)
        {
            if (val == "True")
            {
                return new RbfBoolean()
                {
                    Name = name,
                    Value = true
                };
            }
            else if (val == "False")
            {
                return new RbfBoolean()
                {
                    Name = name,
                    Value = false
                };
            }
            else if (val.StartsWith("0x"))
            {
                uint.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint u);
                return new RbfUint32()
                {
                    Name = name,
                    Value = u
                };
            }
            else if (FloatUtil.TryParse(val, out float f))
            {
                return new RbfFloat()
                {
                    Name = name,
                    Value = f
                };
            }
            else
            {
                return new RbfString()
                {
                    Name = name,
                    Value = val
                };
            }
        }





        private static byte[] GetNullTerminatedAscii(string text)
        {
            var bytes = new byte[Encoding.ASCII.GetByteCount(text) + 1];
            Encoding.ASCII.GetBytes(text.AsSpan(), bytes.AsSpan());
            return bytes;
        }

        private static byte[]? GetByteArray(string? text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var data = new List<byte>();
            var span = text.AsSpan();
            foreach (var range in span.SplitAny(ReadOnlySpan<char>.Empty))
            {
                var token = span[range];
                if (token.IsEmpty) continue;
                data.Add(byte.Parse(token, CultureInfo.CurrentCulture));
            }
            return data.ToArray();
        }
        private static byte[] GetUshortArray(string text)
        {
            var data = new List<byte>();
            var span = text.AsSpan();
            foreach (var range in span.SplitAny(ReadOnlySpan<char>.Empty))
            {
                var token = span[range];
                if (token.IsEmpty) continue;
                var val = ushort.Parse(token, CultureInfo.CurrentCulture);
                data.Add((byte)(val & 0xFF));
                data.Add((byte)(val >> 8));
            }
            return data.ToArray();
        }

    }
}

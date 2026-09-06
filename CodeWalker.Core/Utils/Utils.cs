using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpDX;
using Color = SharpDX.Color;

namespace CodeWalker
{


    public static class PathUtil
    {
        public static string AppPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static string WorkPath = Directory.GetCurrentDirectory();

        public static string GetFilePath(string appRelativePath)
        {
            var path = Path.Combine(AppPath, appRelativePath);
            if (File.Exists(path)) return path;
            path = Path.Combine(WorkPath, appRelativePath);
            return path;
        }

        public static byte[] ReadAllBytes(string appRelativePath)
        {
            var path = GetFilePath(appRelativePath);
            return File.ReadAllBytes(path);
        }

        public static async Task<byte[]> ReadAllBytesAsync(string appRelativePath, CancellationToken cancellationToken = default)
        {
            var path = GetFilePath(appRelativePath);
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }

    }



    public static class TextUtil
    {

        public static string GetBytesReadable(long i)
        {
            //shamelessly stolen from stackoverflow, and a bit mangled

            // Returns the human-readable file size for an arbitrary, 64-bit file size 
            // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
            // Get absolute value
            long absolute_i = (i < 0 ? -i : i);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 bytes"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);

            string fmt = "0.### ";
            if (readable > 1000)
            {
                fmt = "0";
            }
            else if (readable > 100)
            {
                fmt = "0.#";
            }
            else if (readable > 10)
            {
                fmt = "0.##";
            }

            // Return formatted number with suffix
            return readable.ToString(fmt) + suffix;
        }



        public static string GetUTF8Text(byte[]? bytes)
        {
            if (bytes == null)
            { return string.Empty; } //file not found..
            if ((bytes.Length > 3) && (bytes[0] == 0xEF) && (bytes[1] == 0xBB) && (bytes[2] == 0xBF))
            {
                // Use AsSpan to avoid allocation when trimming BOM
                return Encoding.UTF8.GetString(bytes.AsSpan(3));
            }
            return Encoding.UTF8.GetString(bytes);
        }

    }



    public static class FloatUtil
    {
        public static bool TryParse(string? s, out float f) => TryParse(s.AsSpan(), out f);

        public static bool TryParse(ReadOnlySpan<char> s, out float f) =>
            float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out f);

        public static float Parse(string? s) => Parse(s.AsSpan());

        public static float Parse(ReadOnlySpan<char> s)
        {
            TryParse(s, out float f);
            return f;
        }
        public static string ToString(float f)
        {
            var c = CultureInfo.InvariantCulture;
            var s = f.ToString(c);
            var t = Parse(s);
            if (t == f) return s;
            return f.ToString("G9", c);
        }


        public static string GetVector2String(Vector2 v, string d = ", ")
        {
            return ToString(v.X) + d + ToString(v.Y);
        }
        public static string GetVector2XmlString(Vector2 v)
        {
            return $"x=\"{ToString(v.X)}\" y=\"{ToString(v.Y)}\"";
        }
        public static string GetVector3String(Vector3 v, string d = ", ")
        {
            return ToString(v.X) + d + ToString(v.Y) + d + ToString(v.Z);
        }
        public static string GetVector3StringFormat(Vector3 v, string format)
        {
            var c = CultureInfo.InvariantCulture;
            return v.X.ToString(format, c) + ", " + v.Y.ToString(format, c) + ", " + v.Z.ToString(format, c);
        }
        public static string GetVector3XmlString(Vector3 v)
        {
            return $"x=\"{ToString(v.X)}\" y=\"{ToString(v.Y)}\" z=\"{ToString(v.Z)}\"";
        }
        public static string GetVector4String(Vector4 v, string d = ", ")
        {
            return ToString(v.X) + d + ToString(v.Y) + d + ToString(v.Z) + d + ToString(v.W);
        }
        public static string GetVector4XmlString(Vector4 v)
        {
            return $"x=\"{ToString(v.X)}\" y=\"{ToString(v.Y)}\" z=\"{ToString(v.Z)}\" w=\"{ToString(v.W)}\"";
        }
        public static string GetQuaternionXmlString(Quaternion q)
        {
            return $"x=\"{ToString(q.X)}\" y=\"{ToString(q.Y)}\" z=\"{ToString(q.Z)}\" w=\"{ToString(q.W)}\"";
        }
        public static string GetHalf2String(Half2 v, string d = ", ")
        {
            var f = SharpDX.Half.ConvertToFloat(new[] { v.X, v.Y });
            return ToString(f[0]) + d + ToString(f[1]);
        }
        public static string GetHalf4String(Half4 v, string d = ", ")
        {
            var f = SharpDX.Half.ConvertToFloat(new[] { v.X, v.Y, v.Z, v.W });
            return ToString(f[0]) + d + ToString(f[1]) + d + ToString(f[2]) + d + ToString(f[3]);
        }
        public static string GetColourString(Color v, string d = ", ")
        {
            var c = CultureInfo.InvariantCulture;
            return v.R.ToString(c) + d + v.G.ToString(c) + d + v.B.ToString(c) + d + v.A.ToString(c);
        }


        public static Vector2 ParseVector2String(string s)
        {
            ArgumentNullException.ThrowIfNull(s);
            Span<float> components = stackalloc float[2];
            ParseVectorComponents(s.AsSpan(), components);
            return new Vector2(components[0], components[1]);
        }
        public static Vector3 ParseVector3String(string s)
        {
            ArgumentNullException.ThrowIfNull(s);
            Span<float> components = stackalloc float[3];
            ParseVectorComponents(s.AsSpan(), components);
            return new Vector3(components[0], components[1], components[2]);
        }
        public static Vector4 ParseVector4String(string s)
        {
            ArgumentNullException.ThrowIfNull(s);
            Span<float> components = stackalloc float[4];
            ParseVectorComponents(s.AsSpan(), components);
            return new Vector4(components[0], components[1], components[2], components[3]);
        }


        private static void ParseVectorComponents(ReadOnlySpan<char> text, Span<float> components)
        {
            components.Clear();
            int index = 0;
            foreach (var range in text.Split(','))
            {
                components[index++] = Parse(text[range].Trim());
                if (index == components.Length) break;
            }
        }

        public static float Saturate(float f)
        {
            return (f > 1.0f) ? 1.0f : (f < 0.0f) ? 0.0f : f;
        }
        public static float Clamp(float f, float min, float max)
        {
            return f > max ? max : f < min ? min : f;
        }

    }





    public static class BitUtil
    {
        public static bool IsBitSet(uint value, int bit)
        {
            return (((value >> bit) & 1) > 0);
        }
        public static uint SetBit(uint value, int bit)
        {
            return (value | (1u << bit));
        }
        public static uint ClearBit(uint value, int bit)
        {
            return (value & (~(1u << bit)));
        }
        public static uint UpdateBit(uint value, int bit, bool flag)
        {
            if (flag) return SetBit(value, bit);
            else return ClearBit(value, bit);
        }
        public static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }
        public static uint RotateRight(uint value, int count)
        {
            return (value >> count) | (value << (32 - count));
        }
    }


}

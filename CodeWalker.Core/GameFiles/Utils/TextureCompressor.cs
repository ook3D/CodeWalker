using CodeWalker.GameFiles;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CodeWalker.Utils
{
    public enum TextureCompressionFormat
    {
        DXT1,
        DXT1a,
        DXT3,
        DXT5,
        BC4,
        BC5,
        BC7,
        Uncompressed
    }

    public enum TextureCompressionQuality
    {
        Fastest,
        Normal,
        Production,
        Highest
    }
    public class TextureCompressionResult
    {
        public Texture? Texture { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public bool HasAlpha { get; set; }
    }

    public static class TextureCompressor
    {
        private static bool? _nvttAvailable = null;
        private static string? _nvttErrorMessage = null;

        public static bool IsNvttAvailable
        {
            get
            {
                if (_nvttAvailable == null)
                {
                    try
                    {
                        var nvttAssembly = System.Reflection.Assembly.Load("CodeWalker.NVTT");
                        _nvttAvailable = nvttAssembly != null;
                    }
                    catch (Exception ex)
                    {
                        _nvttAvailable = false;
                        _nvttErrorMessage = ex.Message;
                    }
                }
                return _nvttAvailable.Value;
            }
        }

        public static string? NvttErrorMessage => _nvttErrorMessage;
        public static TextureCompressionResult CompressTexture(
            string? imagePath,
            TextureCompressionFormat format,
            TextureCompressionQuality quality,
            bool generateMipmaps,
            bool useCuda = true,
            int minMipmapSize = 0)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                return new TextureCompressionResult
                {
                    Success = false,
                    ErrorMessage = "Image file not found"
                };
            }

            try
            {
                using var image = Image.Load<Bgra32>(imagePath);
                return CompressImageInternal(image, format, quality, generateMipmaps, useCuda, minMipmapSize);
            }
            catch (Exception ex)
            {
                return new TextureCompressionResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to load image: {ex.Message}"
                };
            }
        }

        public static TextureCompressionResult CompressTexture(
            byte[]? imageData,
            TextureCompressionFormat format,
            TextureCompressionQuality quality,
            bool generateMipmaps,
            bool useCuda = true,
            int minMipmapSize = 0)
        {
            if (imageData == null || imageData.Length == 0)
            {
                return new TextureCompressionResult
                {
                    Success = false,
                    ErrorMessage = "Image data is empty"
                };
            }

            try
            {
                // Load image using ImageSharp
                using var image = Image.Load<Bgra32>(imageData);
                return CompressImageInternal(image, format, quality, generateMipmaps, useCuda, minMipmapSize);
            }
            catch (Exception ex)
            {
                return new TextureCompressionResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to load image: {ex.Message}"
                };
            }
        }

        private static TextureCompressionResult CompressImageInternal(
            Image<Bgra32> image,
            TextureCompressionFormat format,
            TextureCompressionQuality quality,
            bool generateMipmaps,
            bool useCuda = true,
            int minMipmapSize = 0)
        {
            var result = new TextureCompressionResult
            {
                OriginalWidth = image.Width,
                OriginalHeight = image.Height,
                HasAlpha = HasTransparentPixels(image)
            };

            try
            {
                // Get raw pixel data in BGRA format
                byte[] pixelData = GetPixelData(image);

                // Use NVTT for compression if available
                if (IsNvttAvailable)
                {
                    return CompressWithNvtt(pixelData, image.Width, image.Height, format, quality, generateMipmaps, useCuda, minMipmapSize, result);
                }
                else
                {
                    // Fallback: Create uncompressed texture or return error
                    return CreateUncompressedTexture(pixelData, image.Width, image.Height, result);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Compression failed: {ex.Message}";
                return result;
            }
        }

        private static TextureCompressionResult CompressWithNvtt(
            byte[] pixelData,
            int width,
            int height,
            TextureCompressionFormat format,
            TextureCompressionQuality quality,
            bool generateMipmaps,
            bool useCuda,
            int minMipmapSize,
            TextureCompressionResult result)
        {
            try
            {
                var nvttAssembly = System.Reflection.Assembly.Load("CodeWalker.NVTT");

                // Get types
                var surfaceType = nvttAssembly.GetType("CodeWalker.NVTT.Surface")!;
                var contextType = nvttAssembly.GetType("CodeWalker.NVTT.Context")!;
                var compressionOptionsType = nvttAssembly.GetType("CodeWalker.NVTT.CompressionOptions")!;
                var outputOptionsType = nvttAssembly.GetType("CodeWalker.NVTT.OutputOptions")!;
                var memoryHandlerType = nvttAssembly.GetType("CodeWalker.NVTT.MemoryOutputHandler")!;
                var formatEnum = nvttAssembly.GetType("CodeWalker.NVTT.Format")!;
                var qualityEnum = nvttAssembly.GetType("CodeWalker.NVTT.Quality")!;
                var inputFormatEnum = nvttAssembly.GetType("CodeWalker.NVTT.InputFormat")!;
                var mipmapFilterEnum = nvttAssembly.GetType("CodeWalker.NVTT.MipmapFilter")!;

                // Create instances
                var surface = Activator.CreateInstance(surfaceType)!;
                var context = Activator.CreateInstance(contextType)!;
                var compressionOptions = Activator.CreateInstance(compressionOptionsType)!;
                var outputOptions = Activator.CreateInstance(outputOptionsType)!;
                var memoryHandler = Activator.CreateInstance(memoryHandlerType)!;

                try
                {
                    // Enable/disable CUDA acceleration on context
                    var enableCudaMethod = contextType.GetMethod("EnableCudaAcceleration");
                    if (enableCudaMethod != null)
                    {
                        enableCudaMethod.Invoke(context, new object[] { useCuda });
                    }

                    // Set image data on surface - BGRA_8UB = 0
                    var setImageMethod = surfaceType.GetMethod("SetImage", new[] { inputFormatEnum, typeof(int), typeof(int), typeof(int), typeof(byte[]) })!;
                    var bgra8ub = Enum.ToObject(inputFormatEnum, 0); // InputFormat.BGRA_8UB
                    setImageMethod.Invoke(surface, new object[] { bgra8ub, width, height, 1, pixelData });

                    // Set compression format
                    var setFormatMethod = compressionOptionsType.GetMethod("SetFormat")!;
                    var nvttFormat = ConvertToNvttFormat(format, formatEnum);
                    setFormatMethod.Invoke(compressionOptions, new[] { nvttFormat });

                    // Set quality
                    var setQualityMethod = compressionOptionsType.GetMethod("SetQuality")!;
                    var nvttQuality = Enum.ToObject(qualityEnum, (int)quality);
                    setQualityMethod.Invoke(compressionOptions, new[] { nvttQuality });

                    // Set output handler
                    var setOutputHandlerMethod = outputOptionsType.GetMethod("SetOutputHandler", new[] { nvttAssembly.GetType("CodeWalker.NVTT.IUnsafeOutputHandler")! })!;
                    setOutputHandlerMethod.Invoke(outputOptions, new[] { memoryHandler });

                    // Enable output header
                    var setOutputHeaderMethod = outputOptionsType.GetMethod("SetOutputHeader")!;
                    setOutputHeaderMethod.Invoke(outputOptions, new object[] { true });

                    // Get mipmap count
                    var mipmapCountProp = surfaceType.GetProperty("MipmapCount")!;
                    int mipmapCount = generateMipmaps ? (int)mipmapCountProp.GetValue(surface)! : 1;

                    // Limit mipmap count based on minimum mipmap size
                    if (generateMipmaps && minMipmapSize > 0)
                    {
                        // Calculate how many mipmap levels we can have before hitting minMipmapSize
                        int maxMipmaps = CalculateMaxMipmapLevels(width, height, minMipmapSize);
                        mipmapCount = Math.Min(mipmapCount, maxMipmaps);
                    }

                    // Output header
                    var outputHeaderMethod = contextType.GetMethod("OutputHeader")!;
                    outputHeaderMethod.Invoke(context, new[] { surface, mipmapCount, compressionOptions, outputOptions });

                    // Compress each mipmap level
                    var compressMethod = contextType.GetMethod("Compress")!;
                    var canMakeMipmapMethod = surfaceType.GetMethod("CanMakeNextMipmap", Type.EmptyTypes)!;
                    var buildMipmapMethod = surfaceType.GetMethod("BuildNextMipmap", new[] { mipmapFilterEnum })!;
                    var cloneMethod = surfaceType.GetMethod("Clone")!;
                    var kaiserFilter = Enum.ToObject(mipmapFilterEnum, 2); // MipmapFilter.Kaiser

                    var mipSurface = cloneMethod.Invoke(surface, null)!;
                    for (int i = 0; i < mipmapCount; i++)
                    {
                        compressMethod.Invoke(context, new[] { mipSurface, 0, i, compressionOptions, outputOptions });

                        if (i < mipmapCount - 1 && (bool)canMakeMipmapMethod.Invoke(mipSurface, null)!)
                        {
                            buildMipmapMethod.Invoke(mipSurface, new[] { kaiserFilter });
                        }
                    }

                    // Dispose mipSurface if it implements IDisposable
                    if (mipSurface is IDisposable disposableMip)
                        disposableMip.Dispose();

                    // Get DDS data
                    var getDataMethod = memoryHandlerType.GetMethod("GetData")!;
                    byte[] ddsData = (byte[])getDataMethod.Invoke(memoryHandler, null)!;

                    // Parse DDS to create texture
                    var texture = DDSIO.GetTexture(ddsData);
                    if (texture == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Failed to parse compressed DDS data";
                        return result;
                    }

                    result.Texture = texture;
                    result.Success = true;
                    return result;
                }
                finally
                {
                    // Dispose NVTT objects
                    if (surface is IDisposable disposableSurface) disposableSurface.Dispose();
                    if (context is IDisposable disposableContext) disposableContext.Dispose();
                    if (compressionOptions is IDisposable disposableCompression) disposableCompression.Dispose();
                    if (outputOptions is IDisposable disposableOutput) disposableOutput.Dispose();
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"NVTT compression failed: {ex.Message}";
                return result;
            }
        }

        private static int CalculateMaxMipmapLevels(int width, int height, int minSize)
        {
            int levels = 1;
            int w = width;
            int h = height;

            while (w > minSize || h > minSize)
            {
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
                levels++;

                // Stop if we've reached the minimum size
                if (w <= minSize && h <= minSize)
                    break;
            }

            return levels;
        }

        private static TextureCompressionResult CreateUncompressedTexture(
            byte[] pixelData,
            int width,
            int height,
            TextureCompressionResult result)
        {
            try
            {
                // Create DDS file manually for uncompressed A8R8G8B8
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // DDS Magic
                bw.Write(0x20534444); // "DDS "

                // DDS_HEADER
                bw.Write(124); // dwSize
                bw.Write(0x1 | 0x2 | 0x4 | 0x1000); // dwFlags: CAPS | HEIGHT | WIDTH | PIXELFORMAT
                bw.Write(height); // dwHeight
                bw.Write(width); // dwWidth
                bw.Write(width * 4); // dwPitchOrLinearSize
                bw.Write(0); // dwDepth
                bw.Write(1); // dwMipMapCount
                for (int i = 0; i < 11; i++) bw.Write(0); // dwReserved1[11]

                // DDS_PIXELFORMAT
                bw.Write(32); // dwSize
                bw.Write(0x41); // dwFlags: RGBA
                bw.Write(0); // dwFourCC
                bw.Write(32); // dwRGBBitCount
                bw.Write(0x00FF0000); // dwRBitMask
                bw.Write(0x0000FF00); // dwGBitMask
                bw.Write(0x000000FF); // dwBBitMask
                bw.Write(0xFF000000); // dwABitMask

                // More DDS_HEADER
                bw.Write(0x1000); // dwCaps: TEXTURE
                bw.Write(0); // dwCaps2
                bw.Write(0); // dwCaps3
                bw.Write(0); // dwCaps4
                bw.Write(0); // dwReserved2

                // Pixel data
                bw.Write(pixelData);

                byte[] ddsData = ms.ToArray();
                var texture = DDSIO.GetTexture(ddsData);

                if (texture == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to create uncompressed texture";
                    return result;
                }

                result.Texture = texture;
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to create uncompressed texture: {ex.Message}";
                return result;
            }
        }

        private static object ConvertToNvttFormat(TextureCompressionFormat format, Type formatEnum)
        {
            int nvttFormat = format switch
            {
                TextureCompressionFormat.DXT1 => 1,   // Format.DXT1
                TextureCompressionFormat.DXT1a => 2,  // Format.DXT1a
                TextureCompressionFormat.DXT3 => 3,   // Format.DXT3
                TextureCompressionFormat.DXT5 => 4,   // Format.DXT5
                TextureCompressionFormat.BC4 => 6,    // Format.BC4
                TextureCompressionFormat.BC5 => 9,    // Format.BC5
                TextureCompressionFormat.BC7 => 15,   // Format.BC7
                TextureCompressionFormat.Uncompressed => 0, // Format.RGB
                _ => 4 // Default to DXT5
            };
            return Enum.ToObject(formatEnum, nvttFormat);
        }

        private static byte[] GetPixelData(Image<Bgra32> image)
        {
            int width = image.Width;
            int height = image.Height;
            byte[] pixelData = new byte[width * height * 4];

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int offset = y * width * 4;
                    MemoryMarshal.AsBytes(row).CopyTo(pixelData.AsSpan(offset, width * 4));
                }
            });

            return pixelData;
        }

        private static bool HasTransparentPixels(Image<Bgra32> image)
        {
            bool hasTransparency = false;
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height && !hasTransparency; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        if (row[x].A < 255)
                        {
                            hasTransparency = true;
                            break;
                        }
                    }
                }
            });
            return hasTransparency;
        }

        public static TextureCompressionFormat SuggestFormat(string imagePath)
        {
            try
            {
                using var image = Image.Load<Bgra32>(imagePath);
                return SuggestFormatForImage(image);
            }
            catch
            {
                return TextureCompressionFormat.DXT5; // Safe default
            }
        }

        private static TextureCompressionFormat SuggestFormatForImage(Image<Bgra32> image)
        {
            bool hasAlpha = HasTransparentPixels(image);

            // Check if it looks like a normal map (mostly purple/blue tint)
            bool looksLikeNormalMap = LooksLikeNormalMap(image);

            if (looksLikeNormalMap)
            {
                return TextureCompressionFormat.BC5;
            }
            else if (hasAlpha)
            {
                return TextureCompressionFormat.DXT5;
            }
            else
            {
                return TextureCompressionFormat.DXT1;
            }
        }

        private static bool LooksLikeNormalMap(Image<Bgra32> image)
        {
            // Sample some pixels and check if they're mostly in the blue/purple range
            // Normal maps typically have R around 128, G around 128, and B around 255
            int normalMapLikePixels = 0;
            int sampledPixels = 0;
            int sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 10);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y += sampleStep)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x += sampleStep)
                    {
                        var pixel = row[x];
                        sampledPixels++;

                        // Check if pixel looks like normal map pixel
                        // Blue channel should be high (>200), R and G should be mid-range
                        if (pixel.B > 200 && pixel.R > 64 && pixel.R < 200 && pixel.G > 64 && pixel.G < 200)
                        {
                            normalMapLikePixels++;
                        }
                    }
                }
            });

            // If more than 70% of sampled pixels look like normal map, suggest BC5
            return sampledPixels > 0 && (float)normalMapLikePixels / sampledPixels > 0.7f;
        }

        public static TextureFormat GetTextureFormat(TextureCompressionFormat format)
        {
            return format switch
            {
                TextureCompressionFormat.DXT1 => TextureFormat.D3DFMT_DXT1,
                TextureCompressionFormat.DXT1a => TextureFormat.D3DFMT_DXT1,
                TextureCompressionFormat.DXT3 => TextureFormat.D3DFMT_DXT3,
                TextureCompressionFormat.DXT5 => TextureFormat.D3DFMT_DXT5,
                TextureCompressionFormat.BC4 => TextureFormat.D3DFMT_ATI1,
                TextureCompressionFormat.BC5 => TextureFormat.D3DFMT_ATI2,
                TextureCompressionFormat.BC7 => TextureFormat.D3DFMT_BC7,
                TextureCompressionFormat.Uncompressed => TextureFormat.D3DFMT_A8R8G8B8,
                _ => TextureFormat.D3DFMT_DXT5
            };
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Newtonsoft.Json;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System.Diagnostics;

namespace texture_unpacker
{
    class Meta { public string image = null; }

    class Rect
    {
        public int x = 0;
        public int y = 0;
        public int w = 0;
        public int h = 0;
    }

    class Frame { public Rect frame = null; }

    class Sprite
    {
        public Meta meta = null;
        public Frame[] frames = null;
    }

    class SpriteResource
    {
        public Sprite resource = null;
    }

    class Program
    {
        static void Main(string[] args)
        {
            const string ResourceFolder = "resources/lobby/lobby/";
            const string SpriteResource = "Bitmap 93.spr";

            var spriteResourceString = File.ReadAllText(ResourceFolder + SpriteResource);
            var spriteResource = JsonConvert.DeserializeObject<SpriteResource>(spriteResourceString);

            using (var originalFileStream = File.OpenRead(ResourceFolder + spriteResource.resource.meta.image))
            {
                originalFileStream.Seek("ZPVR".Length, SeekOrigin.Begin);
                using (var unpackedPvr = new MemoryStream())
                {
                    using (var packedPvr = new InflaterInputStream(originalFileStream))
                    {
                        packedPvr.CopyTo(unpackedPvr);
                    }

                    unpackedPvr.Seek(0, SeekOrigin.Begin);

                    uint version = Read32(unpackedPvr);
                    Debug.Assert(version == 0x03525650, "Endianness is correct as per PVR documentation.");
                    uint flags = Read32(unpackedPvr);
                    Debug.Assert(flags == 2, "Premultiplied alpha");
                    uint format_channels = Read32(unpackedPvr);
                    Debug.Assert(format_channels != 0, "We only use non zero channels format type.");
                    uint format_bitness = Read32(unpackedPvr);
                    uint colorSpace = Read32(unpackedPvr);
                    Debug.Assert(colorSpace == 0, "Linear RGB.");
                    uint channelType = Read32(unpackedPvr);
                    Debug.Assert(channelType == 0, "Unsigned byte normalized channel type.");
                    uint height = Read32(unpackedPvr);
                    uint width = Read32(unpackedPvr);
                    uint depth = Read32(unpackedPvr);
                    Debug.Assert(depth == 1, "Depth is 1.");
                    uint numSurfaces = Read32(unpackedPvr);
                    Debug.Assert(numSurfaces == 1, "Only one surface.");
                    uint numFaces = Read32(unpackedPvr);
                    Debug.Assert(numFaces == 1, "Only one face.");
                    uint mipMapCount = Read32(unpackedPvr);
                    Debug.Assert(mipMapCount == 1, "Only one mip map.");
                    uint metadataSize = Read32(unpackedPvr);

                    string channels = System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(format_channels));
                    string bitness = string.Concat(BitConverter.GetBytes(format_bitness));
                    string format = $"{channels}{bitness}";
                    Debug.Assert(format == "rgba8888" || format == "rgba4444", "Only two pixel formats are supported.");
                    
                    Console.WriteLine($"Flags {flags:X}");
                    Console.WriteLine($"Format {format}");
                    Console.WriteLine($"Color space {colorSpace}");
                    Console.WriteLine($"Channel type {channelType}");
                    Console.WriteLine($"Height {height}");
                    Console.WriteLine($"Width {width}");
                    Console.WriteLine($"Depth {depth}");
                    Console.WriteLine($"Num surfaces {numSurfaces}");
                    Console.WriteLine($"Num faces {numFaces}");
                    Console.WriteLine($"Mip map count {mipMapCount}");
                    Console.WriteLine($"Metadata size {metadataSize}");

                    unpackedPvr.Seek(metadataSize, SeekOrigin.Current);

                    // for each Row in Height
                    //     for each Pixel in Width
                    //         Byte data[Size_Based_On_PixelFormat]
                    //     end
                    // end

                    bool is8BitPerChannel = format == "rgba8888";
                    if (is8BitPerChannel)
                    {
                        using (Image<Rgba32> image = new Image<Rgba32>((int)width, (int)height))
                        {
                            for (int y = 0; y < image.Height; y++)
                            {
                                Span<Rgba32> pixelRowSpan = image.GetPixelRowSpan(y);
                                for (int x = 0; x < image.Width; x++)
                                {
                                    pixelRowSpan[x] = new Rgba32(Read32(unpackedPvr));
                                }
                            }

                            using (Image<Rgba32> cutImage = new Image<Rgba32>(spriteResource.resource.frames[0].frame.w, spriteResource.resource.frames[0].frame.h))
                            {
                                for (int y = 0; y < cutImage.Height; y++)
                                {
                                    Span<Rgba32> pixelRowSpan = cutImage.GetPixelRowSpan(y);
                                    for (int x = 0; x < cutImage.Width; x++)
                                    {
                                        pixelRowSpan[x] = image[spriteResource.resource.frames[0].frame.x + x, spriteResource.resource.frames[0].frame.y + y];
                                    }
                                }

                                using (var pvrFile = File.Create(spriteResource.resource.meta.image))
                                {
                                    cutImage.SaveAsPng(pvrFile);
                                }
                            }
                        }
                    }
                }
            }
        }

        static uint Read32(MemoryStream stream)
        {
            uint result = 0;
            result |= (uint)(stream.ReadByte() << 0);
            result |= (uint)(stream.ReadByte() << 8);
            result |= (uint)(stream.ReadByte() << 16);
            result |= (uint)(stream.ReadByte() << 24);
            return result;
        }
    }
}



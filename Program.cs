using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Newtonsoft.Json;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

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

                    using (var pvrFile = File.Create(spriteResource.resource.meta.image + ".pvr"))
                    {
                        unpackedPvr.CopyTo(pvrFile);
                    }

                    unpackedPvr.Seek(0, SeekOrigin.Begin);

                    uint version = Read32(unpackedPvr);
                    System.Diagnostics.Debug.Assert(version == 0x03525650, "Endianness is correct as per PVR documentation.");
                    uint flags = Read32(unpackedPvr);
                    uint format_zero = Read32(unpackedPvr);
                    uint format = Read32(unpackedPvr);
                    uint colorSpace = Read32(unpackedPvr);
                    uint channelType = Read32(unpackedPvr);
                    uint height = Read32(unpackedPvr);
                    uint width = Read32(unpackedPvr);
                    uint depth = Read32(unpackedPvr);
                    uint numSurfaces = Read32(unpackedPvr);
                    uint numFaces = Read32(unpackedPvr);
                    uint mipMapCount = Read32(unpackedPvr);
                    uint metadataSize = Read32(unpackedPvr);

                    Console.WriteLine($"Flags {flags:X}");
                    if (format_zero == 0) Console.WriteLine($"Format {format}");
                    else Console.WriteLine($"Format {System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(format_zero))}{String.Concat(BitConverter.GetBytes(format).Select(x => x.ToString()))}");
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



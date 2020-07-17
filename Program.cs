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

namespace TextureUnpacker
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
            const string RootFolder = "resources";
            const string OutFolder = "out";
            
            foreach (var path in Directory.GetFiles(RootFolder, "*.spr", SearchOption.AllDirectories))
            {
                var directory = Path.GetDirectoryName(path);
                var fileName = Path.GetFileName(path);
                var relativeDirectory = directory.Substring(directory.IndexOf(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar;

                Console.WriteLine("Processing: " + path);
                Directory.CreateDirectory(OutFolder + relativeDirectory);
                UnpackSprite(RootFolder + relativeDirectory, fileName, OutFolder + relativeDirectory);
            }
        }

        static void UnpackSprite(string folder, string name, string outFolder)
        {
            var spriteResource = ReadSpriteResource(folder + name);
            using (var pvr = DeflateZippedPvrImage(folder + spriteResource.resource.meta.image))
            {
                ReadHeader(pvr, out string format, out uint width, out uint height);
                using (var atlas = CreateAtlas(pvr, format, width, height))
                using (var sprite = CutOutSprite(spriteResource, atlas))
                using (var png = File.Create(outFolder + name + ".png"))
                sprite.SaveAsPng(png);
            }
        }

        static SpriteResource ReadSpriteResource(string path)
        {
            var spriteResourceString = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<SpriteResource>(spriteResourceString);
        }

        static MemoryStream DeflateZippedPvrImage(string path)
        {
            var result = new MemoryStream();
            using (var zippedPvrFile = File.OpenRead(path))
            {
                zippedPvrFile.Seek("ZPVR".Length, SeekOrigin.Begin);
                using (var zippedPvrStream = new InflaterInputStream(zippedPvrFile))
                zippedPvrStream.CopyTo(result);
            }
            result.Seek(0, SeekOrigin.Begin);
            return result;
        }

        static void ReadHeader(MemoryStream stream, out string format, out uint width, out uint height)
        {
            uint version = Read32(stream);
            Debug.Assert(version == 0x03525650, "Endianness is correct as per PVR documentation.");

            uint flags = Read32(stream);
            Debug.Assert(flags == 2, "Premultiplied alpha");

            uint format_channels = Read32(stream);
            uint format_bitness = Read32(stream);
            Debug.Assert(format_channels != 0, "We only use non zero channels format type.");

            string channels = System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(format_channels));
            string bitness = string.Concat(BitConverter.GetBytes(format_bitness));
            format = $"{channels}{bitness}";

            Debug.Assert(format == "rgba8888" || format == "rgba4444", "Only two pixel formats are supported.");

            uint colorSpace = Read32(stream);
            Debug.Assert(colorSpace == 0, "Linear RGB.");

            uint channelType = Read32(stream);
            Debug.Assert(channelType == 0, "Unsigned byte normalized channel type.");

            height = Read32(stream);
            width = Read32(stream);

            uint depth = Read32(stream);
            Debug.Assert(depth == 1, "Depth is 1.");

            uint numSurfaces = Read32(stream);
            Debug.Assert(numSurfaces == 1, "Only one surface.");

            uint numFaces = Read32(stream);
            Debug.Assert(numFaces == 1, "Only one face.");

            uint mipMapCount = Read32(stream);
            Debug.Assert(mipMapCount == 1, "Only one mip map.");

            uint metadataSize = Read32(stream);
            stream.Seek(metadataSize, SeekOrigin.Current);
        }

        static Image<Rgba32> CreateAtlas(MemoryStream stream, string format, uint width, uint height)
        {
            bool is8BitPerChannel = format == "rgba8888";
            Image<Rgba32> image = new Image<Rgba32>((int)width, (int)height);
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> pixelRowSpan = image.GetPixelRowSpan(y);
                for (int x = 0; x < image.Width; x++)
                {
                    pixelRowSpan[x] = new Rgba32(is8BitPerChannel ? Read32(stream) : Read16(stream));
                }
            }
            return image;
        }

        static Image<Rgba32> CutOutSprite(SpriteResource resource, Image<Rgba32> atlas)
        {
            Image<Rgba32> image = new Image<Rgba32>(resource.resource.frames[0].frame.w, resource.resource.frames[0].frame.h);
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgba32> pixelRowSpan = image.GetPixelRowSpan(y);
                for (int x = 0; x < image.Width; x++)
                {
                    pixelRowSpan[x] = atlas[resource.resource.frames[0].frame.x + x, resource.resource.frames[0].frame.y + y];
                }
            }
            return image;
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

        static uint Read16(MemoryStream stream)
        {
            uint result = 0;
            uint byte_one = (uint)stream.ReadByte();
            uint byte_two = (uint)stream.ReadByte();

            result |= ((((byte_two & 0x000000_F0) >> 4) * 17) << 0);
            result |= ((((byte_two & 0x000000_0F) >> 0) * 17) << 8);
            result |= ((((byte_one & 0x000000_F0) >> 4) * 17) << 16);
            result |= ((((byte_one & 0x000000_0F) >> 0) * 17) << 24);
            return result;
        }
    }
}



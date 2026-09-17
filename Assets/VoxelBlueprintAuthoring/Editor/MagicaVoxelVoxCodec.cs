using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Vr3.VoxelBlueprintAuthoring.Editor
{
    /// <summary>Minimal MagicaVoxel .vox (150+) import/export for a single blueprint volume.</summary>
    internal static class MagicaVoxelVoxCodec
    {
        const int VoxVersion = 150;

        struct VoxCell
        {
            public byte x;
            public byte y;
            public byte z;
            public byte colorIndex;
        }

        public static void Import(string path, VoxelBlueprintAsset asset)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                if (ReadId(reader) != "VOX ")
                    throw new InvalidDataException("This is not a MagicaVoxel .vox file.");
                if (reader.ReadInt32() < VoxVersion)
                    throw new InvalidDataException("This .vox file is older than version 150.");

                Vector3Int size = Vector3Int.zero;
                List<VoxCell> cells = new List<VoxCell>();
                Color32[] palette = DefaultPalette();
                ReadChunks(reader, reader.BaseStream.Length, ref size, cells, ref palette);
                if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                    throw new InvalidDataException("The .vox file contains no SIZE chunk.");

                asset.Resize(size.x, size.y, size.z, preserveCells: false);
                asset.Clear();
                foreach (VoxCell cell in cells)
                {
                    if (cell.x >= size.x || cell.y >= size.y || cell.z >= size.z || cell.colorIndex == 0)
                        continue;
                    asset.SetCell(cell.x, cell.y, cell.z, true, palette[cell.colorIndex - 1], 4);
                }
            }
        }

        public static void Export(string path, VoxelBlueprintAsset asset)
        {
            List<Color32> palette = new List<Color32>();
            Dictionary<Color32, byte> paletteIndices = new Dictionary<Color32, byte>();
            List<VoxCell> cells = new List<VoxCell>();

            for (int z = 0; z < asset.sizeZ; z++)
            for (int y = 0; y < asset.sizeY; y++)
            for (int x = 0; x < asset.sizeX; x++)
            {
                if (!asset.TryGetCell(x, y, z, out VoxelBlueprintAsset.Cell cell) || !cell.enabled)
                    continue;
                if (!paletteIndices.TryGetValue(cell.color, out byte colorIndex))
                {
                    if (palette.Count >= 255)
                        colorIndex = FindClosestPaletteIndex(cell.color, palette);
                    else
                    {
                        palette.Add(cell.color);
                        colorIndex = (byte)palette.Count;
                        paletteIndices.Add(cell.color, colorIndex);
                    }
                }
                cells.Add(new VoxCell { x = CheckedByte(x), y = CheckedByte(y), z = CheckedByte(z), colorIndex = colorIndex });
            }

            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("VOX "));
                writer.Write(VoxVersion);
                using (MemoryStream childBytes = new MemoryStream())
                using (BinaryWriter children = new BinaryWriter(childBytes))
                {
                    WriteChunk(children, "SIZE", body => { body.Write(asset.sizeX); body.Write(asset.sizeY); body.Write(asset.sizeZ); });
                    WriteChunk(children, "XYZI", body =>
                    {
                        body.Write(cells.Count);
                        foreach (VoxCell cell in cells) { body.Write(cell.x); body.Write(cell.y); body.Write(cell.z); body.Write(cell.colorIndex); }
                    });
                    WriteChunk(children, "RGBA", body =>
                    {
                        for (int i = 0; i < 256; i++)
                        {
                            Color32 color = i < palette.Count ? palette[i] : new Color32(0, 0, 0, 255);
                            body.Write(color.r); body.Write(color.g); body.Write(color.b); body.Write(color.a);
                        }
                    });
                    children.Flush();
                    WriteMainChunk(writer, childBytes.ToArray());
                }
            }
        }

        static void ReadChunks(BinaryReader reader, long end, ref Vector3Int size, List<VoxCell> cells, ref Color32[] palette)
        {
            while (reader.BaseStream.Position < end)
            {
                string id = ReadId(reader);
                int contentSize = reader.ReadInt32();
                int childSize = reader.ReadInt32();
                long contentEnd = reader.BaseStream.Position + contentSize;
                if (id == "SIZE" && contentSize >= 12)
                    size = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                else if (id == "XYZI" && contentSize >= 4)
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count && reader.BaseStream.Position + 4 <= contentEnd; i++)
                        cells.Add(new VoxCell { x = reader.ReadByte(), y = reader.ReadByte(), z = reader.ReadByte(), colorIndex = reader.ReadByte() });
                }
                else if (id == "RGBA" && contentSize >= 1024)
                {
                    for (int i = 0; i < 256; i++)
                        palette[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                }
                reader.BaseStream.Position = contentEnd;
                long childrenEnd = reader.BaseStream.Position + childSize;
                if (childSize > 0) ReadChunks(reader, childrenEnd, ref size, cells, ref palette);
                reader.BaseStream.Position = childrenEnd;
            }
        }

        static void WriteMainChunk(BinaryWriter writer, byte[] children)
        {
            writer.Write(Encoding.ASCII.GetBytes("MAIN"));
            writer.Write(0);
            writer.Write(children.Length);
            writer.Write(children);
        }

        static void WriteChunk(BinaryWriter target, string id, Action<BinaryWriter> writeContent)
        {
            using (MemoryStream bytes = new MemoryStream())
            using (BinaryWriter content = new BinaryWriter(bytes))
            {
                writeContent(content);
                content.Flush();
                target.Write(Encoding.ASCII.GetBytes(id));
                target.Write((int)bytes.Length);
                target.Write(0);
                target.Write(bytes.ToArray());
            }
        }

        static string ReadId(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
        static byte CheckedByte(int value)
        {
            if (value > byte.MaxValue) throw new InvalidOperationException("MagicaVoxel .vox supports a maximum dimension of 256 voxels.");
            return (byte)value;
        }
        static byte FindClosestPaletteIndex(Color32 color, List<Color32> palette)
        {
            int best = 0, bestDistance = int.MaxValue;
            for (int i = 0; i < palette.Count; i++)
            {
                Color32 candidate = palette[i];
                int dr = color.r - candidate.r, dg = color.g - candidate.g, db = color.b - candidate.b, da = color.a - candidate.a;
                int distance = dr * dr + dg * dg + db * db + da * da;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return (byte)(best + 1);
        }
        static Color32[] DefaultPalette()
        {
            Color32[] colors = new Color32[256];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
            return colors;
        }
    }
}

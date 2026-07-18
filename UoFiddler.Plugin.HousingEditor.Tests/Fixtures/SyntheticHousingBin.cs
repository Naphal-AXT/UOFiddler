using System.IO;
using UoFiddler.Plugin.HousingEditor.Classes;

namespace UoFiddler.Plugin.HousingEditor.Tests.Fixtures;

/// <summary>
/// Hand-built housing.bin byte fixtures, independent of
/// <see cref="HousingBinWriter"/> so codec tests aren't circular against the
/// code that's supposed to be verified. Deliberately synthetic (not real
/// client data) - housing.bin is EA/Broadsword's licensed client content and
/// shouldn't be redistributed via this repo; the structure below is fully
/// documented in HousingBinCodec.cs / README.md and reproduced from that
/// documentation, not extracted from any client file.
/// </summary>
public static class SyntheticHousingBin
{
    public sealed class Piece
    {
        public required uint Direction;
        public required uint Value;
    }

    public sealed class Entry
    {
        public uint CategoryId;
        public uint SubcategoryId;
        public uint FeatureMask;
        public uint ClilocId;
        public List<Piece> Fields1 { get; } = new();
        public List<Piece> Fields2 { get; } = new();
    }

    public sealed class FileTypeSection
    {
        public required uint FileType;
        public List<Entry> Entries { get; } = new();
    }

    /// <summary>
    /// Builds raw (decompressed) housing.bin bytes for the given sections,
    /// following the structure documented in HousingBinCodec.cs exactly:
    /// fileType 5 (Walls) gets its "unknown" dword between fields1/fields2,
    /// every other fileType gets it after fields2.
    /// </summary>
    public static byte[] Build(params FileTypeSection[] sections)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write((uint)sections.Length);

        foreach (FileTypeSection section in sections)
        {
            bool isWalls = section.FileType == 5;

            writer.Write(section.FileType);
            writer.Write((uint)section.Entries.Count);

            foreach (Entry entry in section.Entries)
            {
                writer.Write(entry.CategoryId);
                writer.Write(entry.SubcategoryId);
                writer.Write(entry.FeatureMask);
                writer.Write(entry.ClilocId);

                WriteGroup(writer, entry.Fields1);

                if (isWalls)
                    writer.Write(0u);

                WriteGroup(writer, entry.Fields2);

                if (!isWalls)
                    writer.Write(0u);
            }
        }

        return stream.ToArray();
    }

    private static void WriteGroup(BinaryWriter writer, List<Piece> pieces)
    {
        writer.Write((uint)pieces.Count);

        foreach (Piece piece in pieces)
        {
            writer.Write(piece.Direction);
            writer.Write(piece.Value);
        }
    }
}

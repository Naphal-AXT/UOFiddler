// /***************************************************************************
//  *
//  * $Author:
//  *
//  * "THE BEER-WARE LICENSE"
//  * As long as you retain this notice you can do whatever you want with
//  * this stuff. If we meet some day, and you think this stuff is worth it,
//  * you can buy me a beer in return.
//  *
//  ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Ultima;
using UoFiddler.Plugin.UopPacker.Classes;

namespace UoFiddler.Plugin.HousingEditor.Classes
{
    /// <summary>
    /// Repacks a freshly written housing.bin (see <see cref="HousingBinWriter"/>)
    /// into a complete MultiCollection.uop.
    ///
    /// Deliberately reuses <c>UoFiddler.Plugin.UopPacker.Classes.
    /// LegacyMulFileConverter.ToUop</c> instead of a plugin-local
    /// implementation - it already builds a correct MultiCollection.uop from
    /// multi.mul/multi.idx (the other 871 entries besides housing.bin
    /// itself), including the exact housing.bin identifier hash
    /// (0x126D1E99DDEDEE0A) this project independently confirmed while
    /// reverse engineering housing.bin's own binary format. Rewriting that
    /// logic here would just be a worse copy of already-working code.
    ///
    /// This means repacking needs the client's own multi.mul/multi.idx to
    /// exist alongside MultiCollection.uop, to supply the other 871
    /// entries - it is NOT reading them back out of the original
    /// MultiCollection.uop (ToUop only ever builds from mul/idx). Most
    /// Classic clients still ship both side by side (ours does), but a
    /// client that dropped the legacy mul/idx fallback files entirely
    /// can't be repacked this way.
    ///
    /// One real gap this leaves: any multi that only ever existed in the
    /// UOP with no multi.mul equivalent (confirmed concretely: 71 of them
    /// on the real client this was verified against) would be silently
    /// dropped by ToUop alone. If the client's original MultiCollection.uop
    /// is present, <see cref="Repack"/> patches this by copying those
    /// entries' raw (still-compressed) bytes across verbatim - it never
    /// re-derives their contents, just carries forward what the original
    /// file already had for the entries `ToUop` couldn't produce.
    /// </summary>
    internal static class MultiCollectionRepacker
    {
        // Same on-disk constants LegacyMulFileConverter.ToUop uses - kept in
        // sync deliberately so a merged file's layout matches what ToUop
        // alone would have produced, entries beyond `tableSize` just spill
        // into additional 0x64-entry table blocks the same way.
        private const long FirstTable = 0x200;
        private const int TableSize = 0x64;

        private readonly record struct RawEntry(ulong Hash, byte[] Data, int DecompressedSize, short Flag);

        /// <summary>
        /// True if this client has what repacking needs: multi.mul and
        /// multi.idx alongside MultiCollection.uop.
        /// </summary>
        public static bool CanRepack(string clientPath)
        {
            return File.Exists(Path.Combine(clientPath, "multi.mul")) &&
                   File.Exists(Path.Combine(clientPath, "multi.idx"));
        }

        /// <summary>
        /// Builds a complete MultiCollection.uop at outputPath, combining
        /// the client's own multi.mul/multi.idx with the given housing.bin
        /// bytes (typically <see cref="HousingBinWriter"/>'s output). If the
        /// client's original MultiCollection.uop is also present, any multi
        /// entry it has that multi.mul doesn't is carried forward verbatim
        /// (see class remarks) instead of being silently dropped.
        /// </summary>
        public static void Repack(string clientPath, byte[] housingBinBytes, string outputPath)
        {
            if (!CanRepack(clientPath))
                throw new FileNotFoundException("multi.mul/multi.idx not found next to MultiCollection.uop - can't repack without them.");

            string tempHousingBin = Path.GetTempFileName();
            string tempBaseUop = Path.GetTempFileName();

            try
            {
                File.WriteAllBytes(tempHousingBin, housingBinBytes);

                LegacyMulFileConverter.ToUop(
                    Path.Combine(clientPath, "multi.mul"),
                    Path.Combine(clientPath, "multi.idx"),
                    tempBaseUop,
                    FileType.MultiCollection,
                    typeIndex: 0,
                    compressionFlag: CompressionFlag.Zlib,
                    housingBinFile: tempHousingBin);

                string originalUop = Path.Combine(clientPath, "MultiCollection.uop");

                if (!File.Exists(originalUop))
                {
                    File.Copy(tempBaseUop, outputPath, overwrite: true);
                    return;
                }

                MergeOrphanEntries(tempBaseUop, originalUop, outputPath);
            }
            finally
            {
                File.Delete(tempHousingBin);
                File.Delete(tempBaseUop);
            }
        }

        /// <summary>
        /// Writes outputPath as basePath's entries plus any entry from
        /// originalPath whose hash isn't already in basePath, copied across
        /// with its original (already-compressed) bytes unchanged.
        /// </summary>
        private static void MergeOrphanEntries(string basePath, string originalPath, string outputPath)
        {
            List<RawEntry> baseEntries = ReadRawEntries(basePath);
            List<RawEntry> originalEntries = ReadRawEntries(originalPath);

            HashSet<ulong> baseHashes = new();
            foreach (RawEntry e in baseEntries)
                baseHashes.Add(e.Hash);

            List<RawEntry> merged = new(baseEntries);

            foreach (RawEntry orig in originalEntries)
            {
                if (!baseHashes.Contains(orig.Hash))
                    merged.Add(orig);
            }

            WriteRawUop(merged, outputPath);
        }

        private static List<RawEntry> ReadRawEntries(string path)
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new(stream);

            if (reader.ReadInt32() != 0x50594D)
                throw new InvalidDataException($"'{path}' is not a MYP/UOP file.");

            _ = reader.ReadUInt32(); // version
            _ = reader.ReadUInt32(); // timestamp
            long nextTable = reader.ReadInt64();
            _ = reader.ReadUInt32(); // table size
            _ = reader.ReadInt32(); // file count

            List<RawEntry> entries = new();

            stream.Seek(nextTable, SeekOrigin.Begin);

            do
            {
                int count = reader.ReadInt32();
                nextTable = reader.ReadInt64();

                for (int i = 0; i < count; i++)
                {
                    long offset = reader.ReadInt64();
                    int headerLength = reader.ReadInt32();
                    int compressedSize = reader.ReadInt32();
                    int decompressedSize = reader.ReadInt32();
                    ulong hash = reader.ReadUInt64();
                    _ = reader.ReadUInt32(); // data hash (adler32) - recomputed on write if needed
                    short flag = reader.ReadInt16();

                    if (offset == 0)
                        continue;

                    long returnPos = stream.Position;
                    stream.Seek(offset + headerLength, SeekOrigin.Begin);
                    byte[] data = reader.ReadBytes(compressedSize);
                    stream.Seek(returnPos, SeekOrigin.Begin);

                    entries.Add(new RawEntry(hash, data, decompressedSize, flag));
                }

                if (nextTable != 0)
                    stream.Seek(nextTable, SeekOrigin.Begin);
            }
            while (nextTable != 0 && stream.Position < stream.Length);

            return entries;
        }

        private static void WriteRawUop(List<RawEntry> entries, string outputPath)
        {
            using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using BinaryWriter writer = new(stream);

            writer.Write(0x50594D); // MYP
            writer.Write(5); // version
            writer.Write(unchecked((int)0xFD23EC43)); // format timestamp
            writer.Write(FirstTable);
            writer.Write(TableSize);
            writer.Write(entries.Count);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            for (long i = 0x28; i < FirstTable; i++)
                writer.Write((byte)0);

            int tableCount = (int)Math.Ceiling(entries.Count / (double)TableSize);

            for (int t = 0; t < tableCount; t++)
            {
                long thisTable = stream.Position;

                int start = t * TableSize;
                int end = Math.Min(start + TableSize, entries.Count);

                writer.Write(end - start);
                writer.Write((long)0); // next table, fixed up below
                writer.Seek(34 * TableSize, SeekOrigin.Current); // TOC entries, filled in below

                long[] entryOffsets = new long[end - start];

                for (int j = start; j < end; j++)
                {
                    entryOffsets[j - start] = stream.Position;
                    writer.Write(entries[j].Data);
                }

                long nextTablePos = stream.Position;

                if (t < tableCount - 1)
                {
                    stream.Seek(thisTable + 4, SeekOrigin.Begin);
                    writer.Write(nextTablePos);
                }

                // Always return to the TOC area (right after the 12-byte table
                // header) before writing TOC entries - the seek above only
                // covers the "next table" pointer patch for non-last tables;
                // without this, the last table's TOC entries would be written
                // at the wrong offset (after all data, past the table header),
                // leaving its real TOC area all-zero and every one of its
                // entries silently unreadable (offset 0 reads as empty).
                stream.Seek(thisTable + 12, SeekOrigin.Begin);

                for (int j = start; j < end; j++)
                {
                    RawEntry entry = entries[j];
                    writer.Write(entryOffsets[j - start]);
                    writer.Write(0); // header length
                    writer.Write(entry.Data.Length); // compressed size
                    writer.Write(entry.DecompressedSize);
                    writer.Write(entry.Hash);
                    writer.Write(HashAdler32(entry.Data));
                    writer.Write(entry.Flag);
                }

                for (int j = end - start; j < TableSize; j++)
                    writer.Write(EmptyTableEntry);

                stream.Seek(nextTablePos, SeekOrigin.Begin);
            }
        }

        private static readonly byte[] EmptyTableEntry = new byte[8 + 4 + 4 + 4 + 8 + 4 + 2];

        private static uint HashAdler32(byte[] data)
        {
            uint a = 1, b = 0;

            foreach (byte value in data)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }
    }
}

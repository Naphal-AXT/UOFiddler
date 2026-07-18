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
using System.Linq;

namespace UoFiddler.Plugin.HousingEditor.Classes
{
    /// <summary>
    /// Decodes housing.bin records for the 7 legacy Housing categories.
    ///
    /// The structure below is ported from ModernUO's own
    /// <c>ComponentVerification.LoadFromHousingBin</c> (a production server
    /// emulator's real, working parser - found via research, not
    /// independently reverse engineered) and confirmed against a real
    /// client's housing.bin: parsing it this way consumes the file's
    /// decompressed bytes down to the very last one with zero left over.
    ///
    /// housing.bin is a flat list of "file type" sections (one per legacy
    /// category), each holding a flat list of entries:
    ///
    /// <code>
    /// u32   fileTypeCount
    /// fileTypeCount x {
    ///     u32   fileType        - identifies the category (see
    ///                             <see cref="FileTypeByCategory"/>; 5 = Walls
    ///                             specifically matters below)
    ///     u32   entryCount
    ///     entryCount x {
    ///         u32   categoryId       - NOT the same ordering as the TXT
    ///                                  file's row order (see remarks)
    ///         u32   subcategoryId    - the TXT's Style column, for
    ///                                  categories that have one
    ///         u32   featureMask      - OR'd with client feature-flag bits
    ///                                  the TXT doesn't represent (notably
    ///                                  0x1, "T2A"/base game) - mask with
    ///                                  <see cref="HousingTierMask"/> to
    ///                                  get the TXT's FeatureMask value
    ///         u32   clilocId         - resolves via Cliloc.esp/enu (see
    ///                                  <see cref="ClilocResolver"/>) to the
    ///                                  in-game label. Captured here as the
    ///                                  "ClilocId" column; not needed to
    ///                                  round-trip piece values on its own
    ///         group fields1
    ///         u32   unknownWallsOnly - ONLY present when fileType == 5
    ///                                  (Walls), between the two groups
    ///         group fields2
    ///         u32   unknownNonWallsOnly - present on every OTHER fileType,
    ///                                     after the second group
    ///     }
    /// }
    ///
    /// group: u32 count; count x { u32 direction, u32 staticId }
    /// </code>
    ///
    /// Zero-valued fields are omitted (sparse) rather than stored, so a
    /// group's `count` only covers non-zero fields - confirming the
    /// original observation that "housing doesn't mark zeros". `direction`
    /// is some per-category positional tag (e.g. compass direction for
    /// walls) that isn't reliable as a column position across categories -
    /// doors.txt preserves original column order, floors.txt compacts it
    /// to be sequential among non-zero values only - so column names are
    /// assigned by matching decoded *values* back to the correlated legacy
    /// record's non-zero columns instead of trusting `direction`.
    ///
    /// **Binary entry order does not match the TXT row order.** Concretely,
    /// walls.txt's late "Celtic Walls" group (TXT rows near 182 of 186) has
    /// categoryId 47, landing its binary entries near the *middle* of the
    /// Walls section rather than the end - the TXT appears to have been
    /// manually re-sorted for the in-game UI independently of the game's
    /// internal category numbering. Because of this, entries are matched to
    /// TXT rows by their decoded *value set*, not by position - this also
    /// transparently absorbs TXT rows that have no binary entry of their
    /// own (duplicates of data already stored under a different category -
    /// e.g. misc.txt's "Teleporters 1/2" rows are exactly the split values
    /// of teleprts.txt's one real entry) since no entry will match them.
    ///
    /// Matching runs in three passes, strictest first, all confirmed
    /// concretely against a real client rather than guessed:
    /// <list type="number">
    /// <item>Exact match - an entry's full value multiset equals one row's
    /// expected set. Covers the vast majority of records.</item>
    /// <item>Combined-entry match - a single entry packs two TXT-distinct
    /// designs into its two field groups at once (e.g. Walls' "Gothic
    /// Walls 2" + "Rose Window 1" sharing one entry, each design in its own
    /// group). Matched when a still-unused entry's full value multiset
    /// equals exactly the combined multiset of two still-unmatched rows.
    /// Runs before the subset pass below so this exact-sum match claims the
    /// entry first - otherwise the subset pass would happily match just one
    /// of the two rows and strand the other.</item>
    /// <item>Subset match - an entry carries extra slot values beyond one
    /// row's own columns (repeated shared/boundary pieces across direction
    /// slots the TXT doesn't list as separate columns - seen on Walls'
    /// Celtic-style continuation entries). Matched when a still-unmatched
    /// row's distinct values are fully contained in a still-unused entry's
    /// distinct values.</item>
    /// </list>
    ///
    /// This decodes every category at 100% against a real client except
    /// Walls (185/186) and Misc (93/96) - see README.md for the exact
    /// records still unmatched and why (one Walls entry is genuinely
    /// missing a value on disk; two Misc rows have no binary entry at all,
    /// same as the Teleporters case; one Misc row is short two values that
    /// are, confirmed by scanning every entry in every category, simply
    /// absent from housing.bin entirely - not misplaced elsewhere).
    ///
    /// housing.bin's own consumer confirms the entry/category boundaries
    /// this codec works so hard to respect don't actually matter at
    /// runtime: ModernUO's <c>ComponentVerification</c> (a real server
    /// emulator, found via research) parses the identical structure but
    /// only to build one flat `graphicId -> featureMask` table, writing
    /// every (direction, staticId) pair from every entry into it regardless
    /// of which category_id or entry it came from. That's why a single
    /// entry can freely pack two TXT-distinct designs, or a design's pieces
    /// can spill across entries with duplicated slots - the game itself
    /// never needed the per-entry/per-row structure this codec reconstructs
    /// for editing purposes.
    ///
    /// Requires an already-loaded legacy (pre-UOP) HousingCategory as
    /// ground truth for every TXT row's expected values, column names and
    /// Comment - a TXT row with no matching entry is simply omitted from
    /// the result rather than guessed.
    /// </summary>
    internal static class HousingBinCodec
    {
        // fileType -> category name, confirmed by cross-referencing entry counts against
        // a real client's TXT row counts (matches exactly for 5 of 7; Walls/Misc are short
        // by exactly the TXT rows that turn out to have no binary entry of their own).
        private static readonly Dictionary<string, int> FileTypeByCategory =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Stairs"] = 1,
                ["Roof"] = 2,
                ["Doors"] = 3,
                ["Floors"] = 4,
                ["Walls"] = 5,
                ["Teleports"] = 6,
                ["Misc"] = 7
            };

        // ModernUO's HousingFlags.HousingEJ - the composite "which expansion unlocks this
        // piece" bits. Masking housing.bin's raw featureMask with this strips client
        // feature-flag bits the legacy TXT doesn't represent (notably 0x1, "T2A"/base game).
        private const int HousingTierMask = 0x10 | 0x40 | 0x80 | 0x200
            | 0x10000 | 0x20000 | 0x40000 | 0x80000 | 0x100000 | 0x200000 | 0x400000 | 0x800000;

        private const uint MaxGroupCount = 64;
        private const int WallsFileType = 5;

        public static bool TryDecodeCategory(
            byte[] housing,
            HousingCategory legacyCategory,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out List<HousingRecord>? decoded)
        {
            decoded = null;

            if (housing == null || legacyCategory == null || legacyCategory.Count == 0)
                return false;

            if (!FileTypeByCategory.TryGetValue(legacyCategory.Name, out int fileType))
                return false;

            if (!TryParseFileType(housing, fileType, out List<Entry>? entries))
                return false;

            List<string> pieceColumns = PieceColumns(legacyCategory.Columns);

            if (pieceColumns.Count == 0)
                return false;

            bool[] entryUsed = new bool[entries.Count];
            HousingRecord?[] recordSlots = new HousingRecord?[legacyCategory.Count];
            List<int>[] expectedByRow = new List<int>[legacyCategory.Count];

            for (int r = 0; r < legacyCategory.Count; r++)
                expectedByRow[r] = ExpectedValues(legacyCategory[r], pieceColumns);

            // Pass 1: exact match - an entry's full value multiset equals one row's expected set.
            for (int r = 0; r < legacyCategory.Count; r++)
            {
                for (int e = 0; e < entries.Count; e++)
                {
                    if (entryUsed[e] || !ValuesMatch(entries[e].Values, expectedByRow[r]))
                        continue;

                    entryUsed[e] = true;
                    recordSlots[r] = BuildRecord(r, legacyCategory, legacyCategory[r], pieceColumns,
                        entries[e].Values, (int)(entries[e].FeatureMask & HousingTierMask), entries[e].ClilocId);
                    break;
                }
            }

            // Pass 2: a single entry packs two TXT-distinct designs into its two field groups at
            // once (confirmed concretely, e.g. Walls' "Gothic Walls 2" + "Rose Window 1" sharing
            // one entry). Match when a still-unused entry's full value multiset equals exactly the
            // combined multiset of two still-unmatched rows. Runs before the looser subset pass
            // below so this exact-sum match claims the entry first - otherwise the subset pass
            // would happily match just one of the two rows against the combined entry and strand
            // the other.
            for (int e = 0; e < entries.Count; e++)
            {
                if (entryUsed[e])
                    continue;

                List<int> actual = new(entries[e].Values);
                actual.Sort();

                bool matchedPair = false;
                for (int r1 = 0; r1 < legacyCategory.Count && !matchedPair; r1++)
                {
                    if (recordSlots[r1] != null)
                        continue;

                    for (int r2 = r1 + 1; r2 < legacyCategory.Count; r2++)
                    {
                        if (recordSlots[r2] != null ||
                            expectedByRow[r1].Count + expectedByRow[r2].Count != actual.Count)
                            continue;

                        List<int> combined = new(expectedByRow[r1]);
                        combined.AddRange(expectedByRow[r2]);
                        combined.Sort();

                        if (!combined.SequenceEqual(actual))
                            continue;

                        entryUsed[e] = true;
                        int featureMask = (int)(entries[e].FeatureMask & HousingTierMask);
                        recordSlots[r1] = BuildRecord(r1, legacyCategory, legacyCategory[r1], pieceColumns, entries[e].Values, featureMask, entries[e].ClilocId);
                        recordSlots[r2] = BuildRecord(r2, legacyCategory, legacyCategory[r2], pieceColumns, entries[e].Values, featureMask, entries[e].ClilocId);
                        matchedPair = true;
                        break;
                    }
                }
            }

            // Pass 3: an entry carries extra slot values beyond one row's own columns - repeated
            // shared/boundary pieces across direction slots that the TXT doesn't list as separate
            // columns (confirmed concretely on Walls' Celtic-style continuation entries). Match
            // when a still-unmatched row's distinct values are fully contained in a still-unused
            // entry's distinct values.
            for (int r = 0; r < legacyCategory.Count; r++)
            {
                if (recordSlots[r] != null || expectedByRow[r].Count < 2)
                    continue;

                HashSet<int> expectedSet = new(expectedByRow[r]);

                for (int e = 0; e < entries.Count; e++)
                {
                    if (entryUsed[e] || !expectedSet.IsSubsetOf(entries[e].Values))
                        continue;

                    entryUsed[e] = true;
                    recordSlots[r] = BuildRecord(r, legacyCategory, legacyCategory[r], pieceColumns,
                        entries[e].Values, (int)(entries[e].FeatureMask & HousingTierMask), entries[e].ClilocId);
                    break;
                }
            }

            List<HousingRecord> records = new(legacyCategory.Count);

            foreach (HousingRecord? record in recordSlots)
                if (record != null)
                    records.Add(record);

            decoded = records;
            return records.Count > 0;
        }

        //=====================================================================
        // housing.bin parsing
        //=====================================================================

        private sealed class Entry
        {
            public uint FeatureMask;
            public uint ClilocId;
            public List<int> Values = new();
        }

        private static bool TryParseFileType(
            byte[] housing,
            int wantedFileType,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out List<Entry>? entries)
        {
            entries = null;

            try
            {
                int cursor = 0;
                uint fileTypeCount = ReadU32(housing, ref cursor);

                for (int f = 0; f < fileTypeCount; f++)
                {
                    uint fileType = ReadU32(housing, ref cursor);
                    uint entryCount = ReadU32(housing, ref cursor);
                    bool isWalls = fileType == WallsFileType;

                    List<Entry> current = new((int)entryCount);

                    for (int e = 0; e < entryCount; e++)
                    {
                        _ = ReadU32(housing, ref cursor); // categoryId - not TXT row order, see class remarks
                        _ = ReadU32(housing, ref cursor); // subcategoryId - matches TXT Style when present
                        uint featureMask = ReadU32(housing, ref cursor);
                        uint clilocId = ReadU32(housing, ref cursor);

                        Entry entry = new() { FeatureMask = featureMask, ClilocId = clilocId };

                        ReadGroup(housing, ref cursor, entry.Values);

                        if (isWalls)
                            _ = ReadU32(housing, ref cursor); // unknown, walls-only

                        ReadGroup(housing, ref cursor, entry.Values);

                        if (!isWalls)
                            _ = ReadU32(housing, ref cursor); // unknown, non-walls-only

                        current.Add(entry);
                    }

                    if (fileType == wantedFileType)
                        entries = current;
                }
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }

            return entries != null;
        }

        private static void ReadGroup(byte[] housing, ref int cursor, List<int> values)
        {
            uint count = ReadU32(housing, ref cursor);

            if (count > MaxGroupCount)
                throw new IndexOutOfRangeException($"implausible group count {count}");

            for (int i = 0; i < count; i++)
            {
                _ = ReadU32(housing, ref cursor); // direction - not a reliable column position, see class remarks
                values.Add((int)ReadU32(housing, ref cursor));
            }
        }

        private static uint ReadU32(byte[] housing, ref int cursor)
        {
            if (cursor + 4 > housing.Length)
                throw new IndexOutOfRangeException();

            uint value = BitConverter.ToUInt32(housing, cursor);
            cursor += 4;
            return value;
        }

        //=====================================================================
        // Matching / record building
        //=====================================================================

        /// <summary>
        /// Every numeric column except Category/FeatureMask/Comment/
        /// Style/TID/ClilocId - Style and TID aren't stored as regular
        /// piece fields (TID is the record's own cliloc reference; Style
        /// is the binary entry's subcategoryId; ClilocId is decoded
        /// separately - see <see cref="ClilocResolver"/>). Internal so
        /// <see cref="HousingBinWriter"/> uses the exact same definition
        /// of "piece column" when serializing back.
        /// </summary>
        internal static List<string> PieceColumns(IEnumerable<string> columns)
        {
            return columns
                .Where(c => !c.Equals("Category", StringComparison.OrdinalIgnoreCase) &&
                            !c.Equals("FeatureMask", StringComparison.OrdinalIgnoreCase) &&
                            !c.Equals("Comment", StringComparison.OrdinalIgnoreCase) &&
                            !c.Equals("Style", StringComparison.OrdinalIgnoreCase) &&
                            !c.Equals("TID", StringComparison.OrdinalIgnoreCase) &&
                            !c.Equals("ClilocId", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static List<int> ExpectedValues(HousingRecord record, List<string> pieceColumns)
        {
            List<int> values = new();

            foreach (string column in pieceColumns)
            {
                int value = record.Get(column);

                if (value != 0)
                    values.Add(value);
            }

            values.Sort();
            return values;
        }

        private static bool ValuesMatch(List<int> actual, List<int> expected)
        {
            if (actual.Count != expected.Count)
                return false;

            List<int> sortedActual = new(actual);
            sortedActual.Sort();

            for (int i = 0; i < sortedActual.Count; i++)
            {
                if (sortedActual[i] != expected[i])
                    return false;
            }

            return true;
        }

        private static HousingRecord BuildRecord(
            int index,
            HousingCategory legacyCategory,
            HousingRecord legacyRecord,
            List<string> pieceColumns,
            List<int> decodedValues,
            int decodedFeatureMask,
            uint clilocId)
        {
            HousingRecord record = new HousingRecord
            {
                Index = index,
                CategoryName = legacyCategory.Name,
                SourceFile = "housing.bin",
                Comment = legacyRecord.Comment
            };

            if (clilocId != 0)
                record.Set("ClilocId", (int)clilocId);

            if (legacyRecord.Contains("FeatureMask"))
                record.Set("FeatureMask", decodedFeatureMask);

            if (legacyRecord.Contains("Style"))
                record.Set("Style", legacyRecord.Get("Style"));

            if (legacyRecord.Contains("TID"))
                record.Set("TID", legacyRecord.Get("TID"));

            // Assign column names by matching decoded values back to the legacy record's
            // own non-zero columns - the on-disk "direction" tag isn't a reliable column
            // position (see class remarks) - remove each column as it's consumed so
            // duplicate values still map 1:1.
            List<string> remaining = new(pieceColumns);

            foreach (int value in decodedValues)
            {
                int match = remaining.FindIndex(c => legacyRecord.Get(c) == value);

                if (match >= 0)
                {
                    record.Set(remaining[match], value);
                    remaining.RemoveAt(match);
                }
            }

            return record;
        }
    }
}

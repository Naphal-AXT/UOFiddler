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
using System.Linq;

namespace UoFiddler.Plugin.HousingEditor.Classes
{
    /// <summary>
    /// Serializes a project's housing categories back into a fresh
    /// housing.bin byte array, using the same structure confirmed and
    /// documented in <see cref="HousingBinCodec"/>.
    ///
    /// The `direction` tag and the group1/group2 split are now written
    /// using the real convention confirmed 2026-07-18 against a real
    /// Classic client (see docs/FILEFORMATS.md in the sibling ThePiperBox
    /// repo for the full evidence): `direction` is the fixed 1-based
    /// position of a piece's column within its own group's on-disk column
    /// order - not compacted when a column is zero. For Doors/Floors/
    /// Roof/Teleports/Misc that order is simply the TXT declaration order
    /// (a single group, fields2 empty). Walls splits its TXT columns at a
    /// fixed point: the first 8 (South1..Post, the wall-structure pieces)
    /// go in fields1, the remaining window-variant columns
    /// (WindowS..SecondAltWindowE) go in fields2 - both in TXT order.
    /// Stairs is the one category whose fields1 order is NOT the TXT
    /// order: `Block` (the TXT's first column) is moved to the very end
    /// of fields1 instead of staying first; fields2 is the trailing
    /// `Multi*` TXT columns, in TXT order.
    ///
    /// This is still not guaranteed byte-identical to Origin's own writer
    /// - `categoryId` uses the record's own list index (entry order and
    /// original categoryId numbering aren't recoverable from the TXT
    /// data), and a small number of real rows didn't fit this rule
    /// cleanly during verification (2/58 Floors, 5/96 Misc, 41/186 Walls -
    /// see docs/FILEFORMATS.md) - but every category's `direction`
    /// convention is now reproduced deliberately instead of a sequential
    /// placeholder, which is a real, functionally-motivated improvement
    /// confirmed via ModernUO's own <c>ComponentVerification.
    /// LoadFromHousingBin</c>: the live game flattens every
    /// (direction, staticId) pair into one `graphicId -&gt; featureMask`
    /// table regardless of entry/group/category_id, so structural
    /// validity was never at risk - this change is about matching the
    /// real file's shape as closely as now confirmed, not about fixing a
    /// functional bug.
    /// </summary>
    internal static class HousingBinWriter
    {
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

        private const int WallsFileType = 5;

        // Confirmed 2026-07-18 against a real client: Walls splits its TXT
        // columns (South1, South2, South3, Corner, East1, East2, East3,
        // Post, WindowS, AltWindowS, WindowE, AltWindowE,
        // SecondAltWindowS, SecondAltWindowE) at a fixed point - the first
        // 8 (wall structure) go in fields1, the rest (window variants) in
        // fields2, both still in TXT order.
        private const int WallsGroup1Size = 8;

        // Stairs' fields1 order is a fixed permutation, NOT the TXT
        // column order - "Block" (TXT's first column) moves to the end.
        // fields2 is the trailing Multi* TXT columns, in TXT order.
        private static readonly string[] StairsGroup1Order =
            { "North", "East", "South", "West", "Squared1", "Squared2", "Rounded1", "Rounded2", "Block" };

        private static readonly string[] StairsGroup2Order =
            { "MultiNorth", "MultiEast", "MultiSouth", "MultiWest" };

        /// <summary>
        /// Builds a fresh housing.bin (decompressed) from every recognized
        /// category in the project (Doors/Walls/Floors/Stairs/Roof/
        /// Teleports/Misc - any other category is skipped, there is no
        /// eighth fileType). Categories are emitted in fileType order
        /// (1..7) - the on-disk section order doesn't affect how any real
        /// consumer reads the file, it only groups sections by their own
        /// fileType header.
        /// </summary>
        public static byte[] Write(HousingProject project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            List<HousingCategory> categories = project.Categories
                .Where(c => FileTypeByCategory.ContainsKey(c.Name))
                .OrderBy(c => FileTypeByCategory[c.Name])
                .ToList();

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);

            writer.Write((uint)categories.Count);

            foreach (HousingCategory category in categories)
            {
                int fileType = FileTypeByCategory[category.Name];
                bool isWalls = fileType == WallsFileType;

                writer.Write((uint)fileType);
                writer.Write((uint)category.Count);

                List<string> pieceColumns = HousingBinCodec.PieceColumns(category.Columns);
                (List<string> group1Order, List<string> group2Order) = FieldOrder(category.Name, pieceColumns, isWalls);

                foreach (HousingRecord record in category)
                    WriteRecord(writer, record, group1Order, group2Order, isWalls);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// The fixed, confirmed on-disk column order for each of a
        /// category's two groups - see the class remarks for the evidence
        /// behind each case.
        /// </summary>
        private static (List<string> group1, List<string> group2) FieldOrder(
            string categoryName, List<string> pieceColumns, bool isWalls)
        {
            if (categoryName.Equals("Stairs", StringComparison.OrdinalIgnoreCase))
                return (StairsGroup1Order.ToList(), StairsGroup2Order.ToList());

            if (isWalls)
                return (pieceColumns.Take(WallsGroup1Size).ToList(), pieceColumns.Skip(WallsGroup1Size).ToList());

            return (pieceColumns, EmptyGroup);
        }

        private static void WriteRecord(
            BinaryWriter writer,
            HousingRecord record,
            List<string> group1Order,
            List<string> group2Order,
            bool isWalls)
        {
            uint categoryId = (uint)record.Index;
            uint subcategoryId = (uint)(record.Contains("Style") ? record.Get("Style") : 0);
            uint featureMask = (uint)(record.Contains("FeatureMask") ? record.Get("FeatureMask") : 0);
            uint clilocId = (uint)(record.Contains("ClilocId") ? record.Get("ClilocId") : 0);

            writer.Write(categoryId);
            writer.Write(subcategoryId);
            writer.Write(featureMask);
            writer.Write(clilocId);

            WriteGroup(writer, record, group1Order);

            if (isWalls)
                writer.Write((uint)0); // unknown, walls-only

            WriteGroup(writer, record, group2Order);

            if (!isWalls)
                writer.Write((uint)0); // unknown, non-walls-only
        }

        private static readonly List<string> EmptyGroup = new();

        /// <summary>
        /// Writes every non-zero column in <paramref name="order"/> as a
        /// (direction, value) pair - `direction` is that column's fixed
        /// 1-based position in <paramref name="order"/>, NOT compacted
        /// when an earlier column in the same order is zero (confirmed
        /// against a real client - see class remarks).
        /// </summary>
        private static void WriteGroup(BinaryWriter writer, HousingRecord record, List<string> order)
        {
            List<(int direction, int value)> pairs = new();

            for (int i = 0; i < order.Count; i++)
            {
                int value = record.Get(order[i]);
                if (value != 0)
                    pairs.Add((i + 1, value));
            }

            writer.Write((uint)pairs.Count);

            foreach ((int direction, int value) in pairs)
            {
                writer.Write((uint)direction);
                writer.Write((uint)value);
            }
        }
    }
}

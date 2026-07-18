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
    /// This is a **functional** writer, not a byte-exact one: it doesn't
    /// try to reproduce Origin's original entry order, categoryId values,
    /// group1/group2 split, or per-piece "direction" tags, because none of
    /// those affect how the file is actually consumed. Confirmed via
    /// ModernUO's own <c>ComponentVerification.LoadFromHousingBin</c> (see
    /// the codec's remarks and README.md): the live game just flattens
    /// every (direction, staticId) pair from every entry into one flat
    /// `graphicId -&gt; featureMask` table, regardless of which entry,
    /// group, or category_id it came from. So this writer puts every
    /// non-zero piece value into fields1 with a sequential placeholder
    /// direction, leaves fields2 empty, and uses the record's own list
    /// index as categoryId - structurally valid, and functionally
    /// identical to any other valid layout a real consumer would accept.
    ///
    /// One category (Teleports) round-trips through housing.bin's real
    /// consumer differently from the other six - ModernUO's parser routes
    /// its group2 into a *separate* multi-id table (Stairs is the only
    /// other one) rather than the flat item table. Since this writer
    /// always uses fields1/empty-fields2, Teleports pieces land in the
    /// same table Doors/Floors/etc. do, not Stairs' multi-id table - the
    /// two happen to share the same wire format either way (see
    /// <see cref="HousingBinCodec"/> - `isStairs` only changes which table
    /// group2's staticIds are written into, not the byte layout itself),
    /// so this is a labeling distinction inside a real server's own code,
    /// not a structural requirement of the file.
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

                foreach (HousingRecord record in category)
                    WriteRecord(writer, record, pieceColumns, isWalls);
            }

            return stream.ToArray();
        }

        private static void WriteRecord(
            BinaryWriter writer,
            HousingRecord record,
            List<string> pieceColumns,
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

            List<int> pieces = new();
            foreach (string column in pieceColumns)
            {
                int value = record.Get(column);
                if (value != 0)
                    pieces.Add(value);
            }

            WriteGroup(writer, pieces);

            if (isWalls)
                writer.Write((uint)0); // unknown, walls-only

            WriteGroup(writer, EmptyGroup);

            if (!isWalls)
                writer.Write((uint)0); // unknown, non-walls-only
        }

        private static readonly List<int> EmptyGroup = new();

        private static void WriteGroup(BinaryWriter writer, List<int> values)
        {
            writer.Write((uint)values.Count);

            for (int i = 0; i < values.Count; i++)
            {
                writer.Write((uint)i); // direction placeholder - not read positionally by any known consumer
                writer.Write((uint)values[i]);
            }
        }
    }
}

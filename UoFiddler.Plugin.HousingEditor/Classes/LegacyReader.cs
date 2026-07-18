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
using System.IO;

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Loads the 7 legacy Housing TXT files (pre-UOP clients) into a
    /// HousingProject. Each file is parsed generically by TxtTableReader,
    /// so column layout differences between files (e.g. misc.txt carrying
    /// extra fields for curved walls) are preserved as-is instead of being
    /// forced into a fixed schema.
    ///
    /// A real Ultima Online Classic client also ships metrics.txt,
    /// mobtypes.txt, Prof.txt, suppinfo.txt, Tile1024.txt and Tile256.txt
    /// alongside these - tested against TxtTableReader and deliberately
    /// left out:
    ///     - metrics.txt is client hardware telemetry (CPU/GPU/OS), not
    ///       housing data at all.
    ///     - mobtypes.txt and Prof.txt don't fit the numeric-columns-plus-
    ///       trailing-Comment shape TxtTableReader assumes (a string TYPE
    ///       column, and a Begin/End key-value block respectively) -
    ///       TxtTableReader.Read throws FormatException on them.
    ///     - Tile1024.txt/Tile256.txt and suppinfo.txt use a different
    ///       layout again (headerless 2-column graphic/hue pairs; a
    ///       leading marker column TxtTableReader can't parse) - these
    ///       "succeed" without a header/type line to skip, silently
    ///       mis-reading real data as the header row, which is worse than
    ///       a clean failure. They'd need their own dedicated readers.
    /// </summary>
    public static class LegacyReader
    {
        private static readonly (string FileName, string Category)[] Files =
        {
            ("doors.txt",       "Doors"),
            ("walls.txt",       "Walls"),
            ("floors.txt",      "Floors"),
            ("stairs.txt",      "Stairs"),
            ("roof.txt",        "Roof"),
            ("misc.txt",        "Misc"),
            ("teleprts.txt",    "Teleports"),
            ("teleporter.txt",  "Teleports"),
            ("teleporters.txt", "Teleports")
        };

        public static HousingProject Load(string clientPath)
        {
            if (String.IsNullOrWhiteSpace(clientPath))
                throw new ArgumentNullException(nameof(clientPath));

            if (!Directory.Exists(clientPath))
                throw new DirectoryNotFoundException(clientPath);

            HousingProject project = new HousingProject
            {
                ClientPath = clientPath,
                ClientType = ClientType.Legacy
            };

            foreach ((string fileName, string categoryName) in Files)
            {
                string file = Path.Combine(clientPath, fileName);

                if (!File.Exists(file))
                    continue;

                LoadCategory(project, file, categoryName);
            }

            return project;
        }

        private static void LoadCategory(
            HousingProject project,
            string fileName,
            string categoryName)
        {
            TxtTable table = TxtTableReader.Read(fileName);

            HousingCategory category =
                project.AddCategory(categoryName, Path.GetFileName(fileName));

            category.TypeLine = table.TypeLine;
            category.Columns.AddRange(table.Columns);

            foreach (TxtRow row in table.Rows)
            {
                HousingRecord record = category.AddRecord();

                record.SourceLine = row.SourceLine;
                record.Comment = row.Comment;

                foreach (string column in table.Columns)
                {
                    if (row.Values.TryGetValue(column, out int value))
                        record.Set(column, value);
                }
            }
        }
    }
}

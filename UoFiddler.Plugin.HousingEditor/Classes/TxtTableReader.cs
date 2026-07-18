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
using System.Globalization;
using System.IO;

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Generic TSV reader used by the Housing plugin.
    ///
    /// Expected format:
    ///
    /// Line 1 : column types (ignored)
    /// Line 2 : optional blank lines
    /// Line N : column names
    /// Remaining lines : data
    ///
    /// The parser is generic and does not know anything about
    /// doors.txt, walls.txt, roof.txt, etc.
    /// </summary>
    public static class TxtTableReader
    {
        public static TxtTable Read(string fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            if (!File.Exists(fileName))
                throw new FileNotFoundException(fileName);

            string[] lines = File.ReadAllLines(fileName);

            if (lines.Length == 0)
                throw new InvalidDataException(fileName);

            TxtTable table = new TxtTable
            {
                FileName = Path.GetFileName(fileName),
                TypeLine = lines[0]
            };

            int line = 0;

            //
            // Skip type definition.
            //
            line++;

            //
            // Skip blank lines.
            //
            while (line < lines.Length &&
                   String.IsNullOrWhiteSpace(lines[line]))
            {
                line++;
            }

            if (line >= lines.Length)
                throw new InvalidDataException(
                    "Header not found.");

            //
            // Header
            //
            string[] header =
                lines[line].Split('\t');

            foreach (string column in header)
            {
                table.Columns.Add(column.Trim());
            }

            line++;

            //
            // Data
            //
            for (; line < lines.Length; line++)
            {
                string current = lines[line];

                if (String.IsNullOrWhiteSpace(current))
                    continue;

                TxtRow row = new TxtRow
                {
                    SourceLine = line + 1
                };

                string[] values =
                    current.Split('\t');

                int count = Math.Min(
                    values.Length,
                    table.Columns.Count);

                for (int i = 0; i < count; i++)
                {
                    string column = table.Columns[i];

                    string value = values[i].Trim();

                    if (column.Equals(
                        "Comment",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        row.Comment = value;
                        continue;
                    }

                    row.Values[column] =
                        ParseInteger(value);
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static int ParseInteger(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return 0;

            if (value.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
            {
                return Int32.Parse(
                    value.Substring(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return Int32.Parse(
                value,
                CultureInfo.InvariantCulture);
        }
    }

    public sealed class TxtTable
    {
        public string FileName { get; set; } = String.Empty;

        /// <summary>
        /// Original, unparsed first line (column type hints). Kept verbatim
        /// so it can be written back out unchanged when saving.
        /// </summary>
        public string TypeLine { get; set; } = String.Empty;

        public List<string> Columns { get; } =
            new List<string>();

        public List<TxtRow> Rows { get; } =
            new List<TxtRow>();

        public bool HasColumn(string name)
        {
            return Columns.Contains(name);
        }

        public int ColumnIndex(string name)
        {
            return Columns.FindIndex(
                x => x.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class TxtRow
    {
        public int SourceLine { get; set; }

        public Dictionary<string, int> Values { get; } =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        public string Comment { get; set; } =
            String.Empty;

        public bool Contains(string column)
        {
            return Values.ContainsKey(column);
        }

        public int Get(string column)
        {
            return Values.TryGetValue(
                column,
                out int value)
                ? value
                : 0;
        }
    }
}

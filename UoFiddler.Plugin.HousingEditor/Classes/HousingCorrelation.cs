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
using System.Text;

namespace UoFiddler.Plugin.HousingEditor.Classes
{
    /// <summary>
    /// Builds the flat, denormalized dataset used to attack housing.bin:
    /// one CSV row per record from every legacy TXT, with every column
    /// that appears anywhere in the project as its own CSV column. This
    /// is the "known good" side of the comparison - diff it against a
    /// hex/structure dump of a housing.bin decompressed from a client of
    /// a similar version to spot the equivalent binary field layout.
    /// </summary>
    internal static class HousingCorrelation
    {
        public static void Export(HousingProject project, string fileName)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            List<string> columns = project.GetColumns().ToList();

            using StreamWriter writer =
                new StreamWriter(fileName, false, Encoding.UTF8);

            writer.Write("Category,SourceFile,Index,Comment");

            foreach (string column in columns)
            {
                writer.Write(',');
                writer.Write(EscapeCsv(column));
            }

            writer.WriteLine();

            foreach (HousingRecord record in project.Records)
            {
                writer.Write(EscapeCsv(record.CategoryName));
                writer.Write(',');
                writer.Write(EscapeCsv(record.SourceFile));
                writer.Write(',');
                writer.Write(record.Index);
                writer.Write(',');
                writer.Write(EscapeCsv(record.Comment));

                foreach (string column in columns)
                {
                    writer.Write(',');

                    if (record.Contains(column))
                        writer.Write(record.Get(column));
                }

                writer.WriteLine();
            }
        }

        /// <summary>
        /// Dumps the decompressed housing.bin bytes as-is, for offline
        /// comparison with a hex editor / external tooling against the
        /// CSV produced by Export.
        /// </summary>
        public static void ExportRawBinary(byte[] data, string fileName)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            File.WriteAllBytes(fileName, data);
        }

        private static string EscapeCsv(string value)
        {
            if (String.IsNullOrEmpty(value))
                return String.Empty;

            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}

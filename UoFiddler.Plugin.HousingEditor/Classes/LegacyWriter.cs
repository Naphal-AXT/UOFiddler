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
using System.Linq;
using System.Text;

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Writes a HousingCategory back to its original tab-separated TXT
    /// layout. Mirrors TxtTableReader/LegacyReader so a load-edit-save
    /// round trip reproduces the original file byte-for-byte for
    /// untouched records (zero values are written as "0", matching how
    /// the legacy files already represent them).
    /// </summary>
    public static class LegacyWriter
    {
        public static void Save(HousingCategory category, string fileName)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            using StreamWriter writer =
                new StreamWriter(fileName, false, Encoding.UTF8);

            writer.WriteLine(category.TypeLine);
            writer.WriteLine(String.Join("\t", category.Columns));

            bool hasComment = category.Columns.Any(
                c => c.Equals("Comment", StringComparison.OrdinalIgnoreCase));

            foreach (HousingRecord record in category)
            {
                string[] fields = category.Columns
                    .Where(c => !c.Equals(
                        "Comment",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(c => record.Get(c).ToString())
                    .ToArray();

                writer.Write(String.Join("\t", fields));

                if (hasComment)
                {
                    writer.Write('\t');
                    writer.Write(record.Comment);
                }

                writer.WriteLine();
            }
        }

        /// <summary>
        /// Saves every category in the project back to its source file,
        /// inside <paramref name="clientPath"/>.
        /// </summary>
        public static void SaveAll(HousingProject project, string clientPath)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            foreach (HousingCategory category in project.Categories)
            {
                if (String.IsNullOrWhiteSpace(category.SourceFile))
                    continue;

                string path = Path.Combine(clientPath, category.SourceFile);

                Save(category, path);
            }
        }
    }
}

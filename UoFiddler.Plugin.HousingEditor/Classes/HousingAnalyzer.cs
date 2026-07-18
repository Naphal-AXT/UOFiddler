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
using System.Linq;
using System.Text;

namespace UoFiddler.Plugin.HousingEditor.Classes
{
    /// <summary>
    /// Produces a quick integrity/summary report for a loaded
    /// HousingProject: per-category counts and duplicate records (same
    /// values across every column - usually a copy/paste mistake in the
    /// source TXT, or a genuine legacy-vs-housing.bin drift once the
    /// binary side is loaded too).
    /// </summary>
    internal static class HousingAnalyzer
    {
        public static string Analyze(HousingProject project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            StringBuilder report = new StringBuilder();

            report.AppendLine(
                $"Client type: {project.ClientType}");

            report.AppendLine(
                $"Categories: {project.CategoryCount}, Records: {project.RecordCount:N0}");

            report.AppendLine();

            foreach (HousingCategory category in project.Categories)
            {
                int duplicates = category.Records
                    .GroupBy(record => String.Join(
                        "|",
                        category.Columns.Select(
                            column => record.Get(column))))
                    .Count(group => group.Count() > 1);

                report.AppendLine(
                    $"{category.Name,-12} " +
                    $"{category.Count,6:N0} records  " +
                    $"{category.Columns.Count,2} columns  " +
                    $"{duplicates,4} duplicate value-sets");
            }

            return report.ToString();
        }
    }
}

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

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Represents one loaded Housing project.
    ///
    /// A project may originate from:
    ///     - Legacy TXT files
    ///     - MultiCollection.uop (housing.bin)
    ///
    /// Internally both formats are represented by the same object model.
    /// </summary>
    public sealed class HousingProject
    {
        private readonly List<HousingCategory> _categories;

        public HousingProject()
        {
            _categories = new List<HousingCategory>();
        }

        /// <summary>
        /// Client root folder.
        /// </summary>
        public string ClientPath { get; set; } = String.Empty;

        /// <summary>
        /// Legacy or UOP.
        /// </summary>
        public ClientType ClientType { get; set; }

        /// <summary>
        /// Decompressed housing.bin bytes, populated when ClientType is
        /// Uop. Empty for legacy projects. Reverse-engineering its per-
        /// object layout is still pending real sample data (see
        /// HousingCorrelation.ExportRawBinary to dump it for offline
        /// analysis against a legacy TXT export of the same content).
        /// </summary>
        public byte[] RawHousingBin { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Categories contained in the project.
        /// </summary>
        public IReadOnlyList<HousingCategory> Categories => _categories;

        /// <summary>
        /// Gets MultiCollection.uop path.
        /// </summary>
        public string MultiCollectionPath =>
            Path.Combine(ClientPath, "MultiCollection.uop");

        /// <summary>
        /// Gets Analysis folder.
        /// </summary>
        public string AnalysisPath =>
            Path.Combine(ClientPath, "Analysis");

        /// <summary>
        /// Gets extracted housing.bin path.
        /// </summary>
        public string HousingBinPath =>
            Path.Combine(AnalysisPath, "housing.bin");

        /// <summary>
        /// Total number of loaded categories.
        /// </summary>
        public int CategoryCount => _categories.Count;

        /// <summary>
        /// Total number of loaded records.
        /// </summary>
        public int RecordCount
        {
            get
            {
                int count = 0;

                foreach (HousingCategory category in _categories)
                    count += category.Count;

                return count;
            }
        }

        /// <summary>
        /// Returns every record.
        /// </summary>
        public IEnumerable<HousingRecord> Records
        {
            get
            {
                foreach (HousingCategory category in _categories)
                {
                    foreach (HousingRecord record in category)
                        yield return record;
                }
            }
        }

        /// <summary>
        /// Adds a category.
        /// </summary>
        public HousingCategory AddCategory(
            string name,
            string sourceFile)
        {
            HousingCategory category = new HousingCategory
            {
                Name = name,
                SourceFile = sourceFile
            };

            _categories.Add(category);

            return category;
        }

        /// <summary>
        /// Removes a category.
        /// </summary>
        public bool RemoveCategory(HousingCategory category)
        {
            return _categories.Remove(category);
        }

        /// <summary>
        /// Clears the project.
        /// </summary>
        public void Clear()
        {
            _categories.Clear();
        }

        /// <summary>
        /// Finds a category by name.
        /// </summary>
        public HousingCategory? FindCategory(string name)
        {
            return _categories.FirstOrDefault(
                x => x.Name.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns every distinct column used by the project.
        /// </summary>
        public IEnumerable<string> GetColumns()
        {
            HashSet<string> columns =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (HousingCategory category in _categories)
            {
                foreach (string column in category.GetColumns())
                    columns.Add(column);
            }

            return columns.OrderBy(x => x);
        }

        /// <summary>
        /// Searches every record containing a value.
        /// </summary>
        public IEnumerable<HousingRecord> FindValue(int value)
        {
            foreach (HousingCategory category in _categories)
            {
                foreach (HousingRecord record in category.FindValue(value))
                    yield return record;
            }
        }

        /// <summary>
        /// Searches records that contain a column.
        /// </summary>
        public IEnumerable<HousingRecord> FindColumn(string column)
        {
            foreach (HousingCategory category in _categories)
            {
                foreach (HousingRecord record in category.FindColumn(column))
                    yield return record;
            }
        }

        /// <summary>
        /// Returns all records ordered by category.
        /// </summary>
        public List<HousingRecord> ToList()
        {
            return Records.ToList();
        }

        /// <summary>
        /// Creates the analysis folder if necessary.
        /// </summary>
        public void EnsureAnalysisFolder()
        {
            if (!Directory.Exists(AnalysisPath))
            {
                Directory.CreateDirectory(AnalysisPath);
            }
        }

        public override string ToString()
        {
            return $"{ClientType} - {CategoryCount} categories - {RecordCount:N0} records";
        }
    }

    public enum ClientType
    {
        Unknown,
        Legacy,
        Uop
    }
}
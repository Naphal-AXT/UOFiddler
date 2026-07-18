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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Represents one housing category.
    ///
    /// Examples:
    ///     Doors
    ///     Walls
    ///     Floors
    ///     Stairs
    ///     Roof
    ///     Misc
    ///     Teleports
    /// </summary>
    public sealed class HousingCategory : IEnumerable<HousingRecord>
    {
        private readonly List<HousingRecord> _records;

        public HousingCategory()
        {
            _records = new List<HousingRecord>();
        }

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Source file.
        /// Example:
        ///     doors.txt
        /// </summary>
        public string SourceFile { get; set; } = String.Empty;

        /// <summary>
        /// Original first line of the TXT (column type hints), kept
        /// verbatim so LegacyWriter can round-trip it unchanged.
        /// </summary>
        public string TypeLine { get; set; } = String.Empty;

        /// <summary>
        /// Column names exactly as they appear in the TXT.
        /// </summary>
        public List<string> Columns { get; } = new();

        /// <summary>
        /// Records contained in this category.
        /// </summary>
        public IReadOnlyList<HousingRecord> Records => _records;

        /// <summary>
        /// Number of records.
        /// </summary>
        public int Count => _records.Count;

        /// <summary>
        /// Adds a new empty record.
        /// </summary>
        public HousingRecord AddRecord()
        {
            HousingRecord record = new HousingRecord
            {
                Index = _records.Count,
                CategoryName = Name,
                SourceFile = SourceFile
            };

            _records.Add(record);

            return record;
        }

        /// <summary>
        /// Adds an existing record.
        /// </summary>
        public void Add(HousingRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            record.Index = _records.Count;
            record.CategoryName = Name;
            record.SourceFile = SourceFile;

            _records.Add(record);
        }

        /// <summary>
        /// Removes a record.
        /// </summary>
        public bool Remove(HousingRecord record)
        {
            if (record == null)
                return false;

            bool removed = _records.Remove(record);

            if (removed)
                Reindex();

            return removed;
        }

        /// <summary>
        /// Removes all records.
        /// </summary>
        public void Clear()
        {
            _records.Clear();
        }

        /// <summary>
        /// Gets a record by index.
        /// </summary>
        public HousingRecord this[int index] => _records[index];

        /// <summary>
        /// Returns every distinct column used by this category.
        /// </summary>
        public IEnumerable<string> GetColumns()
        {
            HashSet<string> names =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (HousingRecord record in _records)
            {
                foreach (string column in record.Columns)
                    names.Add(column);
            }

            return names.OrderBy(x => x);
        }

        /// <summary>
        /// Finds all records containing a given value.
        /// </summary>
        public IEnumerable<HousingRecord> FindValue(int value)
        {
            foreach (HousingRecord record in _records)
            {
                foreach (int field in record.Values.Values)
                {
                    if (field == value)
                    {
                        yield return record;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Finds records that contain a column.
        /// </summary>
        public IEnumerable<HousingRecord> FindColumn(string column)
        {
            foreach (HousingRecord record in _records)
            {
                if (record.Contains(column))
                    yield return record;
            }
        }

        /// <summary>
        /// Updates sequential indexes.
        /// </summary>
        public void Reindex()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                _records[i].Index = i;
                _records[i].CategoryName = Name;
                _records[i].SourceFile = SourceFile;
            }
        }

        public IEnumerator<HousingRecord> GetEnumerator()
        {
            return _records.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return $"{Name} ({Count:N0})";
        }
    }
}
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
using System.ComponentModel;
using System.Linq;

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Represents one housing definition independently of its source.
    ///
    /// Supported sources:
    ///     - doors.txt
    ///     - walls.txt
    ///     - floors.txt
    ///     - stairs.txt
    ///     - roof.txt
    ///     - misc.txt
    ///     - teleprts.txt
    ///     - housing.bin
    /// </summary>
    public sealed class HousingRecord
    {
        public HousingRecord()
        {
            Values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            UnknownFields = new Dictionary<int, uint>();
            RawData = Array.Empty<byte>();
        }

        //=========================================================
        // Source
        //=========================================================

        [Browsable(false)]
        public int Index { get; set; }

        [Browsable(false)]
        public string CategoryName { get; set; } = String.Empty;

        [Browsable(false)]
        public string SourceFile { get; set; } = String.Empty;

        [Browsable(false)]
        public int SourceLine { get; set; }

        //=========================================================
        // Generic values
        //=========================================================

        /// <summary>
        /// All numeric fields indexed by their original TXT column name.
        /// Example:
        ///     Category
        ///     South1
        ///     North
        ///     FeatureMask
        ///     Style
        ///     TID
        /// </summary>
        [Browsable(false)]
        public Dictionary<string, int> Values { get; }

        /// <summary>
        /// Original comment.
        /// </summary>
        [Category("General")]
        [DisplayName("Comment")]
        public string Comment { get; set; } = String.Empty;

        /// <summary>
        /// In-game label resolved from housing.bin's ClilocId column via
        /// Cliloc.esp/Cliloc.enu (see <see cref="Classes.ClilocResolver"/>).
        /// Empty for records with no ClilocId or an unresolved one.
        /// </summary>
        [Category("General")]
        [DisplayName("Cliloc Name")]
        public string ClilocName { get; set; } = String.Empty;

        //=========================================================
        // housing.bin
        //=========================================================

        /// <summary>
        /// Original binary record.
        /// </summary>
        [Browsable(false)]
        public byte[] RawData { get; set; }

        /// <summary>
        /// Unknown binary fields discovered during reverse engineering.
        /// </summary>
        [Browsable(false)]
        public Dictionary<int, uint> UnknownFields { get; }

        //=========================================================
        // Helpers
        //=========================================================

        [Browsable(false)]
        public IEnumerable<string> Columns => Values.Keys;

        [Browsable(false)]
        public int ColumnCount => Values.Count;

        public bool Contains(string column)
        {
            return Values.ContainsKey(column);
        }

        public int Get(string column)
        {
            if (Values.TryGetValue(column, out int value))
                return value;

            return 0;
        }

        public void Set(string column, int value)
        {
            if (String.IsNullOrWhiteSpace(column))
                throw new ArgumentNullException(nameof(column));

            Values[column] = value;
        }

        public bool Remove(string column)
        {
            return Values.Remove(column);
        }

        public void ClearValues()
        {
            Values.Clear();
        }

        public bool TryGetValue(string column, out int value)
        {
            return Values.TryGetValue(column, out value);
        }

        public void SetUnknown(int index, uint value)
        {
            UnknownFields[index] = value;
        }

        public bool TryGetUnknown(int index, out uint value)
        {
            return UnknownFields.TryGetValue(index, out value);
        }

        public void ClearUnknown()
        {
            UnknownFields.Clear();
        }

        /// <summary>
        /// Creates a deep copy.
        /// </summary>
        public HousingRecord Clone()
        {
            HousingRecord record = new HousingRecord
            {
                Index = Index,
                CategoryName = CategoryName,
                SourceFile = SourceFile,
                SourceLine = SourceLine,
                Comment = Comment,
                ClilocName = ClilocName,
                RawData = (byte[])RawData.Clone()
            };

            foreach (KeyValuePair<string, int> pair in Values)
            {
                record.Values.Add(pair.Key, pair.Value);
            }

            foreach (KeyValuePair<int, uint> pair in UnknownFields)
            {
                record.UnknownFields.Add(pair.Key, pair.Value);
            }

            return record;
        }

        /// <summary>
        /// Returns the first field that looks like a graphic.
        /// Used only by the TreeView until the correlation engine
        /// identifies the real mapping.
        /// </summary>
        public int PrimaryGraphic
        {
            get
            {
                foreach (string key in new[]
                {
                    "Piece",
                    "Piece1",
                    "South1",
                    "Graphic",
                    "ItemID"
                })
                {
                    if (Values.TryGetValue(key, out int value))
                        return value;
                }

                return Values.Count > 0 ? Values.First().Value : 0;
            }
        }

        public override string ToString()
        {
            return $"{CategoryName} [{Index}]";
        }
    }
}
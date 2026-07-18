// /***************************************************************************
//  *
//  * $Author: Turley
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
using System.IO;
using System.Text;
using System.Windows.Forms;
using UoFiddler.Plugin.HousingEditor.Classes;

namespace UoFiddler.Plugin.HousingEditor.UserControls
{
    public partial class HousingEditorControl : UserControl
    {
        private const int MaxHexDumpBytes = 1_048_576;

        private readonly BindingList<FieldRow> _fields = new();

        private HousingRecord? _selectedRecord;

        public HousingProject? Project { get; private set; }

        public HousingEditorControl()
        {
            InitializeComponent();

            treeView.AfterSelect += TreeViewAfterSelect;

            btnOpen.Click += BtnOpenClick;
            btnAnalyze.Click += BtnAnalyzeClick;
            btnExport.Click += BtnExportClick;
            btnExportRaw.Click += BtnExportRawClick;
            btnCorrelate.Click += BtnCorrelateClick;
            btnWriteBin.Click += BtnWriteBinClick;
            btnRepackUop.Click += BtnRepackUopClick;
            btnAddRecord.Click += BtnAddRecordClick;
            btnDeleteRecord.Click += BtnDeleteRecordClick;
            btnSave.Click += BtnSaveClick;

            dataGridView.CellEndEdit += DataGridViewCellEndEdit;
        }

        //=====================================================================
        // Toolbar
        //=====================================================================

        private void BtnOpenClick(object? sender, EventArgs e)
        {
            using FolderBrowserDialog dlg = new FolderBrowserDialog
            {
                Description = "Ultima Online Client"
            };

            if (!String.IsNullOrEmpty(Ultima.Files.RootDir) &&
                Directory.Exists(Ultima.Files.RootDir))
            {
                dlg.SelectedPath = Ultima.Files.RootDir;
            }

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                Project = HousingProjectLoader.Load(dlg.SelectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            RefreshTree();

            lblStatus.Text = Project.ToString();
        }

        private void BtnAnalyzeClick(object? sender, EventArgs e)
        {
            if (Project == null)
                return;

            string report = HousingAnalyzer.Analyze(Project);

            MessageBox.Show(
                report,
                "Analysis",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void BtnExportClick(object? sender, EventArgs e)
        {
            if (Project == null)
                return;

            using SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            HousingCorrelation.Export(Project, dlg.FileName);

            lblStatus.Text = $"Exported CSV to {dlg.FileName}";
        }

        private void BtnExportRawClick(object? sender, EventArgs e)
        {
            if (Project == null || Project.ClientType != ClientType.Uop)
            {
                MessageBox.Show(
                    "Open a post-UOP client first (one containing MultiCollection.uop).",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            using SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "housing.bin (*.bin)|*.bin",
                FileName = "housing.bin"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            HousingCorrelation.ExportRawBinary(Project.RawHousingBin, dlg.FileName);

            lblStatus.Text = $"Exported {Project.RawHousingBin.Length:N0} raw bytes to {dlg.FileName}";
        }

        private void BtnCorrelateClick(object? sender, EventArgs e)
        {
            if (Project == null || Project.ClientType != ClientType.Uop)
            {
                MessageBox.Show(
                    "Open a post-UOP client first (one containing MultiCollection.uop).",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            using FolderBrowserDialog dlg = new FolderBrowserDialog
            {
                Description = "Pre-UOP (legacy TXT) client of a similar version, used as ground truth"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            HousingProject legacyProject;

            try
            {
                legacyProject = LegacyReader.Load(dlg.SelectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            List<string> results = new();
            int totalDecoded = 0;
            int totalExpected = 0;

            foreach (HousingCategory legacyCategory in legacyProject.Categories)
            {
                if (Project.FindCategory(legacyCategory.Name) != null)
                    continue; // already decoded (or added) in a previous run

                totalExpected += legacyCategory.Count;

                if (HousingBinCodec.TryDecodeCategory(Project.RawHousingBin, legacyCategory, out List<HousingRecord>? decoded))
                {
                    HousingCategory category = Project.AddCategory(legacyCategory.Name, "housing.bin");
                    category.Columns.AddRange(legacyCategory.Columns);

                    foreach (HousingRecord record in decoded)
                    {
                        if (record.Contains("ClilocId"))
                            record.ClilocName = ClilocResolver.Resolve(Project.ClientPath, record.Get("ClilocId"));

                        category.Add(record);
                    }

                    totalDecoded += decoded.Count;

                    results.Add(decoded.Count == legacyCategory.Count
                        ? $"{legacyCategory.Name}: {decoded.Count}/{legacyCategory.Count} (complete)"
                        : $"{legacyCategory.Name}: {decoded.Count}/{legacyCategory.Count} (stopped early - see README)");
                }
                else
                {
                    results.Add($"{legacyCategory.Name}: not decodable");
                }
            }

            RefreshTree();

            lblStatus.Text = $"Correlated: {totalDecoded}/{totalExpected} records decoded.";

            MessageBox.Show(
                $"Decoded {totalDecoded}/{totalExpected} records from housing.bin using {dlg.SelectedPath} as reference.\n\n" +
                String.Join("\n", results),
                "Correlate",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void BtnWriteBinClick(object? sender, EventArgs e)
        {
            if (Project == null || Project.ClientType != ClientType.Uop)
            {
                MessageBox.Show(
                    "Open a post-UOP client first (one containing MultiCollection.uop).",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            bool hasDecodedCategory = Project.Categories.Any(
                c => c.SourceFile.Equals("housing.bin", StringComparison.OrdinalIgnoreCase));

            if (!hasDecodedCategory)
            {
                MessageBox.Show(
                    "Run Correlate... first so there is decoded housing.bin data to write back.",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            using SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "housing.bin (*.bin)|*.bin",
                FileName = "housing.bin"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            byte[] data = HousingBinWriter.Write(Project);
            File.WriteAllBytes(dlg.FileName, data);

            lblStatus.Text = $"Wrote {data.Length:N0} bytes to {dlg.FileName}";

            MessageBox.Show(
                $"Wrote a fresh housing.bin ({data.Length:N0} bytes) to {dlg.FileName}.\n\n" +
                "Only includes categories decoded via Correlate... - a category " +
                "that failed to decode, or was never correlated, is simply absent " +
                "from the file rather than written empty. This is the raw " +
                "decompressed bytes, not yet packed into a UOP container - use " +
                "Repack UOP for a complete MultiCollection.uop.",
                "Write housing.bin",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void BtnRepackUopClick(object? sender, EventArgs e)
        {
            if (Project == null || Project.ClientType != ClientType.Uop)
            {
                MessageBox.Show(
                    "Open a post-UOP client first (one containing MultiCollection.uop).",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            bool hasDecodedCategory = Project.Categories.Any(
                c => c.SourceFile.Equals("housing.bin", StringComparison.OrdinalIgnoreCase));

            if (!hasDecodedCategory)
            {
                MessageBox.Show(
                    "Run Correlate... first so there is decoded housing.bin data to write back.",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            if (!MultiCollectionRepacker.CanRepack(Project.ClientPath))
            {
                MessageBox.Show(
                    "This client doesn't have multi.mul/multi.idx next to MultiCollection.uop - " +
                    "repacking needs them to supply the other multi/boat entries. " +
                    "Use Write housing.bin instead for the raw bytes.",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            using SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "MultiCollection.uop (*.uop)|*.uop",
                FileName = "MultiCollection.uop"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            byte[] housingBinData = HousingBinWriter.Write(Project);

            try
            {
                MultiCollectionRepacker.Repack(Project.ClientPath, housingBinData, dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            lblStatus.Text = $"Repacked MultiCollection.uop to {dlg.FileName}";

            MessageBox.Show(
                $"Wrote a complete MultiCollection.uop to {dlg.FileName}, combining this " +
                "client's own multi.mul/multi.idx (the other multi/boat entries) with the " +
                "housing.bin just written from your edits.\n\n" +
                "This never overwrites the client's own MultiCollection.uop - copy it over " +
                "manually once you've verified it.",
                "Repack UOP",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void BtnAddRecordClick(object? sender, EventArgs e)
        {
            HousingCategory? category = GetSelectedCategory();

            if (category == null)
            {
                MessageBox.Show(
                    "Select a category (or a record inside one) first.",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            category.AddRecord();

            RefreshTree();

            lblStatus.Text = $"Added record to {category.Name}";
        }

        private void BtnDeleteRecordClick(object? sender, EventArgs e)
        {
            if (treeView.SelectedNode?.Tag is not HousingRecord record ||
                treeView.SelectedNode.Parent?.Tag is not HousingCategory category)
            {
                MessageBox.Show(
                    "Select a record to delete.",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            category.Remove(record);

            RefreshTree();

            lblStatus.Text = $"Deleted record from {category.Name}";
        }

        private void BtnSaveClick(object? sender, EventArgs e)
        {
            if (Project == null)
                return;

            if (Project.ClientType != ClientType.Legacy)
            {
                MessageBox.Show(
                    "Saving is only implemented for pre-UOP (legacy TXT) clients. " +
                    "housing.bin round-trip writing needs its binary layout reverse " +
                    "engineered first.",
                    "Housing Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            DialogResult confirm = MessageBox.Show(
                $"This overwrites the TXT files in {Project.ClientPath}. Continue?",
                "Housing Editor",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            LegacyWriter.SaveAll(Project, Project.ClientPath);

            lblStatus.Text = "Saved.";
        }

        //=====================================================================
        // Tree
        //=====================================================================

        private void RefreshTree()
        {
            treeView.BeginUpdate();

            treeView.Nodes.Clear();

            if (Project != null)
            {
                if (Project.ClientType == ClientType.Uop)
                {
                    TreeNode rawNode = new TreeNode(
                        $"housing.bin ({Project.RawHousingBin.Length:N0} bytes, raw)")
                    {
                        Tag = Project.RawHousingBin
                    };

                    treeView.Nodes.Add(rawNode);
                }

                // Legacy projects always show categories; Uop projects show
                // them too once decoded via Correlate.
                foreach (HousingCategory category in Project.Categories)
                {
                    TreeNode node = new TreeNode(category.ToString())
                    {
                        Tag = category
                    };

                    foreach (HousingRecord record in category.Records)
                    {
                        TreeNode child = new TreeNode(
                            $"{record.Index:D4}  0x{record.PrimaryGraphic:X4}")
                        {
                            Tag = record
                        };

                        node.Nodes.Add(child);
                    }

                    treeView.Nodes.Add(node);
                }
            }

            treeView.ExpandAll();

            treeView.EndUpdate();
        }

        private void TreeViewAfterSelect(object? sender, TreeViewEventArgs e)
        {
            switch (e.Node?.Tag)
            {
                case HousingRecord record:
                    ShowRecord(record);
                    break;

                case byte[] rawBytes:
                    ShowRawBytes(rawBytes);
                    break;

                default:
                    _selectedRecord = null;
                    propertyGrid.SelectedObject = null;
                    dataGridView.ReadOnly = true;
                    dataGridView.DataSource = null;
                    break;
            }
        }

        private void ShowRecord(HousingRecord record)
        {
            _selectedRecord = record;

            propertyGrid.SelectedObject = record;

            HousingCategory? category = Project?.FindCategory(record.CategoryName);

            _fields.Clear();

            if (category != null)
            {
                foreach (string column in category.Columns)
                {
                    if (column.Equals("Comment", StringComparison.OrdinalIgnoreCase))
                        continue;

                    _fields.Add(new FieldRow(column, record.Get(column)));
                }
            }

            if (!String.IsNullOrEmpty(record.ClilocName))
                _fields.Add(new FieldRow("Cliloc Name", record.ClilocName));

            dataGridView.ReadOnly = false;
            dataGridView.DataSource = _fields;

            DataGridViewColumn? nameColumn = dataGridView.Columns["Name"];

            if (nameColumn != null)
                nameColumn.ReadOnly = true;

            for (int i = 0; i < _fields.Count; i++)
            {
                if (_fields[i].IsReadOnly)
                    dataGridView.Rows[i].ReadOnly = true;
            }
        }

        private void ShowRawBytes(byte[] data)
        {
            _selectedRecord = null;

            propertyGrid.SelectedObject = null;

            int length = Math.Min(data.Length, MaxHexDumpBytes);

            List<HexRow> rows = BuildHexDump(data, length);

            dataGridView.ReadOnly = true;
            dataGridView.DataSource = rows;

            lblStatus.Text = length < data.Length
                ? $"Showing first {length:N0} of {data.Length:N0} bytes"
                : $"{data.Length:N0} bytes";
        }

        private HousingCategory? GetSelectedCategory()
        {
            return treeView.SelectedNode?.Tag switch
            {
                HousingCategory category => category,
                HousingRecord => treeView.SelectedNode.Parent?.Tag as HousingCategory,
                _ => null
            };
        }

        //=====================================================================
        // Editing
        //=====================================================================

        private void DataGridViewCellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_selectedRecord == null)
                return;

            if (e.RowIndex < 0 || e.RowIndex >= _fields.Count)
                return;

            FieldRow field = _fields[e.RowIndex];

            if (field.IsReadOnly)
                return;

            _selectedRecord.Set(field.Name, Convert.ToInt32(field.Value));
        }

        //=====================================================================
        // Hex dump
        //=====================================================================

        private static List<HexRow> BuildHexDump(byte[] data, int length)
        {
            const int bytesPerRow = 16;

            List<HexRow> rows = new(length / bytesPerRow + 1);

            for (int offset = 0; offset < length; offset += bytesPerRow)
            {
                int count = Math.Min(bytesPerRow, length - offset);

                StringBuilder hex = new StringBuilder(bytesPerRow * 3);
                StringBuilder ascii = new StringBuilder(bytesPerRow);

                for (int i = 0; i < count; i++)
                {
                    byte b = data[offset + i];

                    hex.Append(b.ToString("X2"));
                    hex.Append(' ');

                    ascii.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
                }

                rows.Add(new HexRow(
                    offset.ToString("X8"),
                    hex.ToString(),
                    ascii.ToString()));
            }

            return rows;
        }

        private sealed class FieldRow
        {
            public FieldRow(string name, int value)
            {
                Name = name;
                Value = value;
                IsReadOnly = false;
            }

            /// <summary>
            /// Read-only display row (e.g. "Cliloc Name") - not backed by a
            /// Values entry, so there's nothing for DataGridViewCellEndEdit
            /// to write back via record.Set().
            /// </summary>
            public FieldRow(string name, string displayValue)
            {
                Name = name;
                Value = displayValue;
                IsReadOnly = true;
            }

            public string Name { get; set; }

            public object Value { get; set; }

            [Browsable(false)]
            public bool IsReadOnly { get; }
        }

        private sealed class HexRow
        {
            public HexRow(string offset, string hex, string ascii)
            {
                Offset = offset;
                Hex = hex;
                Ascii = ascii;
            }

            public string Offset { get; set; }

            public string Hex { get; set; }

            public string Ascii { get; set; }
        }
    }
}

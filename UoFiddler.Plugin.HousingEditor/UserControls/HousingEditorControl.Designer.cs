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

namespace UoFiddler.Plugin.HousingEditor.UserControls
{
    partial class HousingEditorControl
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnOpen;
        private System.Windows.Forms.ToolStripButton btnAnalyze;
        private System.Windows.Forms.ToolStripButton btnExport;
        private System.Windows.Forms.ToolStripButton btnExportRaw;
        private System.Windows.Forms.ToolStripButton btnCorrelate;
        private System.Windows.Forms.ToolStripButton btnWriteBin;
        private System.Windows.Forms.ToolStripButton btnRepackUop;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton btnAddRecord;
        private System.Windows.Forms.ToolStripButton btnDeleteRecord;
        private System.Windows.Forms.ToolStripButton btnSave;

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.SplitContainer splitRight;

        private System.Windows.Forms.TreeView treeView;
        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.PropertyGrid propertyGrid;

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            toolStrip = new System.Windows.Forms.ToolStrip();
            btnOpen = new System.Windows.Forms.ToolStripButton();
            btnAnalyze = new System.Windows.Forms.ToolStripButton();
            btnExport = new System.Windows.Forms.ToolStripButton();
            btnExportRaw = new System.Windows.Forms.ToolStripButton();
            btnCorrelate = new System.Windows.Forms.ToolStripButton();
            btnWriteBin = new System.Windows.Forms.ToolStripButton();
            btnRepackUop = new System.Windows.Forms.ToolStripButton();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            btnAddRecord = new System.Windows.Forms.ToolStripButton();
            btnDeleteRecord = new System.Windows.Forms.ToolStripButton();
            btnSave = new System.Windows.Forms.ToolStripButton();

            splitMain = new System.Windows.Forms.SplitContainer();
            splitRight = new System.Windows.Forms.SplitContainer();

            treeView = new System.Windows.Forms.TreeView();
            dataGridView = new System.Windows.Forms.DataGridView();
            propertyGrid = new System.Windows.Forms.PropertyGrid();

            statusStrip = new System.Windows.Forms.StatusStrip();
            lblStatus = new System.Windows.Forms.ToolStripStatusLabel();

            SuspendLayout();

            //
            // toolStrip
            //
            toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
            {
                btnOpen,
                btnAnalyze,
                btnExport,
                btnExportRaw,
                btnCorrelate,
                btnWriteBin,
                btnRepackUop,
                toolStripSeparator1,
                btnAddRecord,
                btnDeleteRecord,
                btnSave
            });

            toolStrip.Dock = System.Windows.Forms.DockStyle.Top;

            btnOpen.Text = "Open";
            btnAnalyze.Text = "Analyze";
            btnExport.Text = "Export CSV";
            btnExportRaw.Text = "Export Raw";
            btnCorrelate.Text = "Correlate...";
            btnWriteBin.Text = "Write housing.bin";
            btnRepackUop.Text = "Repack UOP";
            btnAddRecord.Text = "Add";
            btnDeleteRecord.Text = "Delete";
            btnSave.Text = "Save";

            //
            // splitMain
            //
            splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            splitMain.SplitterDistance = 280;

            //
            // splitRight
            //
            splitRight.Dock = System.Windows.Forms.DockStyle.Fill;
            splitRight.Orientation = System.Windows.Forms.Orientation.Vertical;
            splitRight.SplitterDistance = 700;

            //
            // tree
            //
            treeView.Dock = System.Windows.Forms.DockStyle.Fill;

            //
            // grid
            //
            dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            dataGridView.ReadOnly = false;
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.RowHeadersVisible = false;
            dataGridView.AutoSizeColumnsMode =
                System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;

            //
            // propertyGrid
            //
            propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;

            //
            // status
            //
            statusStrip.Items.Add(lblStatus);
            lblStatus.Text = "Ready";

            splitMain.Panel1.Controls.Add(treeView);

            splitRight.Panel1.Controls.Add(dataGridView);
            splitRight.Panel2.Controls.Add(propertyGrid);

            splitMain.Panel2.Controls.Add(splitRight);

            Controls.Add(splitMain);
            Controls.Add(statusStrip);
            Controls.Add(toolStrip);

            Name = "HousingEditorControl";
            Dock = System.Windows.Forms.DockStyle.Fill;

            ResumeLayout(false);
            PerformLayout();
        }
    }
}

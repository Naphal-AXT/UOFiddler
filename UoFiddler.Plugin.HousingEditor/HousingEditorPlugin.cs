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

using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using UoFiddler.Controls.Plugin;
using UoFiddler.Controls.Plugin.Interfaces;
using UoFiddler.Plugin.HousingEditor.UserControls;

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Main entry point for the Housing Editor plugin.
    /// </summary>
    public sealed class HousingEditorPlugin : PluginBase
    {
        private HousingEditorControl? _editorControl;

        public override IPluginHost Host { get; set; } = null!;

        public override string Name => "Housing Editor";

        public override string Description =>
            "Legacy Housing / housing.bin editor";

        public override string Author =>
            "Naphal-AXT";

        public override string Version =>
            "1.0.0";

        public override void Initialize()
        {
            Logger.LogInformation("Housing Editor initialized.");
        }

        public override void Unload()
        {
            Logger.LogInformation("Housing Editor unloaded.");
        }

        public override void ModifyTabPages(TabControl tabControl)
        {
            if (_editorControl == null)
            {
                _editorControl = new HousingEditorControl
                {
                    Dock = DockStyle.Fill
                };
            }

            foreach (TabPage page in tabControl.TabPages)
            {
                if (page.Text == "Housing Editor")
                    return;
            }

            TabPage tab = new TabPage
            {
                Text = "Housing Editor"
            };

            tab.Controls.Add(_editorControl);

            tabControl.TabPages.Add(tab);
        }

        public override void ModifyPluginToolStrip(
            ToolStripDropDownButton toolStrip)
        {
            ToolStripMenuItem item =
                new ToolStripMenuItem(Name);

            item.Click += (_, _) =>
            {
                MessageBox.Show(
                    "Housing Editor plugin loaded.",
                    Name,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };

            toolStrip.DropDownItems.Add(item);
        }
    }
}
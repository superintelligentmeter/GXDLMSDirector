// Modified by Superintelligent Meter contributors, 2026-10-29: offline UI review and dialog correctness fixes. GPL-2.0-only; existing notices retained.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GXDLMSDirector.Macro
{
    public partial class GXMacroCountDlg : Form
    {
        public GXMacroCountDlg()
        {
            InitializeComponent();
            CountTb.Text = Properties.Settings.Default.MacroEditorInvokeCount.ToString();
        }

        private void OKBtn_Click(object sender, EventArgs e)
        {
            try
            {
                int value = int.Parse(CountTb.Text);
                if (value < 1)
                {
                    throw new Exception("Invalid macro invoke count.");
                }
                Properties.Settings.Default.MacroEditorInvokeCount = value;
            }
            catch (Exception ex)
            {
                DialogResult = DialogResult.None;
                MessageBox.Show(this, ex.Message);
            }
        }
    }
}

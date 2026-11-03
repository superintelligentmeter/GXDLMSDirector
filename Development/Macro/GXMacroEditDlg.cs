// Modified by Superintelligent Meter contributors, 2026-10-30: measure native confirmation groups without changing actions. GPL-2.0-only; original notices retained.
// Modified by Superintelligent Meter contributors, 2026-10-30: align and bound native property inputs. GPL-2.0-only; existing notices retained.
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GXDLMSDirector.Macro
{
    public partial class GXMacroEditDlg : Form
    {
        GXMacro _macro;
        public GXMacroEditDlg(GXMacro macro)
        {
            InitializeComponent();
            Gurux.DLMS.UI.NativeDialogFooter.Apply(this, OKBtn, CancelBtn);
            DirectorPropertyRows.AlignNativeRows(NameTb.Parent, (NameLbl, NameTb), (DescriptionLbl, DescriptionTb));
            _macro = macro;
            NameTb.Text = macro.Name;
            DescriptionTb.Text = macro.Description;
            EnabledCb.Checked = !macro.Disable;
        }

        private void OKBtn_Click(object sender, EventArgs e)
        {
            try
            {
                _macro.Name = NameTb.Text;
                if (string.IsNullOrEmpty(_macro.Name))
                {
                    _macro.Name = null;
                }
                _macro.Disable = !EnabledCb.Checked;
                _macro.Description = DescriptionTb.Text;
                if (string.IsNullOrEmpty(_macro.Description))
                {
                    _macro.Description = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message);
            }
        }
    }
}

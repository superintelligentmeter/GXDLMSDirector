//
// --------------------------------------------------------------------------
//  Gurux Ltd
//
//
//
//
// Version:         $Revision: 9442 $,
//                  $Date: 2017-05-23 15:21:03 +0300 (ti, 23 touko 2017) $
//                  $Author: gurux01 $
//
// Copyright (c) Gurux Ltd
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This file is a part of Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// More information of Gurux DLMS/COSEM Director: https://www.gurux.org/GXDLMSDirector
//
// This code is licensed under the GNU General Public License v2.
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------

using Gurux.DLMS.Objects;
using Gurux.DLMS.UI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace GXDLMSDirector
{
    public partial class GXValuesDlg : Form
    {
        private readonly Dictionary<Type, List<IGXDLMSView>> Views;
        private readonly Dictionary<GXDLMSObject, IGXDLMSView> OwnedViews = new Dictionary<GXDLMSObject, IGXDLMSView>();
        private bool disposedOwnedViews;

        public GXValuesDlg(Dictionary<Type, List<IGXDLMSView>> views, string path)
        {
            InitializeComponent();
            Views = views;
            ApplyLayout();
            foreach (GXDLMSObject it in GXDLMSObjectCollection.Load(path))
            {
                ListViewItem li = new ListViewItem(it.LogicalName);
                li.Tag = it;
                ObjectList.Items.Add(li);
            }
            if (ObjectList.Items.Count != 0)
            {
                ObjectList.Items[0].Selected = true;
            }
        }

        private IGXDLMSView CreateOwnedView(GXDLMSObject target)
        {
            if (OwnedViews.TryGetValue(target, out IGXDLMSView existing))
            {
                return existing;
            }
            IGXDLMSView template = GXDlmsUi.GetView(Views, target);
            var owned = (IGXDLMSView)Activator.CreateInstance(template.GetType());
            var form = (Form)owned;
            bool registered = false;
            try
            {
                OwnedViews.Add(target, owned);
                registered = true;
                form.TopLevel = false;
                form.TopMost = false;
                form.FormBorderStyle = FormBorderStyle.None;
                form.Dock = DockStyle.Fill;
                ModernPresentation.Apply(form);
                GXDlmsUi.Init(owned, (_, _) => { });
                owned.Target = target;
                GXDlmsUi.ObjectChanged(owned, null, target, connected: false, showSnapshotValues: true);
                foreach (Control control in Descendants(form))
                {
                    if (control is GXValueField field)
                    {
                        field.ReadOnly = true;
                    }
                    else if (control is GXButton button)
                    {
                        button.Enabled = false;
                    }
                }
                return owned;
            }
            catch
            {
                if (registered && OwnedViews.TryGetValue(target, out IGXDLMSView registeredView) && ReferenceEquals(registeredView, owned))
                {
                    OwnedViews.Remove(target);
                }
                form.Dispose();
                throw;
            }
        }

        private static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (Control nested in Descendants(child))
                {
                    yield return nested;
                }
            }
        }

        private void DisposeOwnedViews()
        {
            if (disposedOwnedViews)
            {
                return;
            }
            disposedOwnedViews = true;
            ObjectPanelFrame.Controls.Clear();
            foreach (IGXDLMSView view in OwnedViews.Values)
            {
                ((Form)view).Dispose();
            }
            OwnedViews.Clear();
        }
        private void ApplyLayout()
        {
            SuspendLayout();
            var table = ModernPresentation.CompactSurface(this);
            table.AutoSize = false;
            table.Dock = DockStyle.Fill;
            table.RowCount = 1;
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.TabIndex = 0;
            panel1.Dock = DockStyle.Fill;
            panel1.Margin = Padding.Empty;
            table.Controls.Add(panel1, 0, 0);
            table.SetColumnSpan(panel1, 3);
            ObjectList.Width = (int)(160 * DeviceDpi / 96f);
            ObjectList.AccessibleName = "Saved objects";
            ObjectPanelFrame.AccessibleName = "Selected object values";
            ObjectList.SizeChanged += (_, _) => DescriptionColumnHeader.Width = Math.Max(1, ObjectList.ClientSize.Width);
            DescriptionColumnHeader.Width = ObjectList.ClientSize.Width;
            Controls.Add(table);
            // The existing Close button is both the accept and cancel action.
            var footer = ModernPresentation.DialogFooter(this, OKBtn, OKBtn);
            footer.TabIndex = 1;
            OKBtn.TabIndex = 0;
            ClientSize = new System.Drawing.Size((int)(840 * DeviceDpi / 96f), (int)(480 * DeviceDpi / 96f));
            ModernPresentation.Apply(this);
            ResumeLayout(true);
        }

        private void OKBtn_Click(object sender, System.EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// User has selected new OBIS code.
        /// </summary>
        private void ObjectList_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected)
            {
                return;
            }
            try
            {
                GXDLMSObject obj = (GXDLMSObject)e.Item.Tag;
                IGXDLMSView SelectedView = CreateOwnedView(obj);
                ObjectPanelFrame.Controls.Clear();
                ObjectPanelFrame.Controls.Add(((Form)SelectedView));
                ((Form)SelectedView).Show();
            }
            catch (Exception Ex)
            {
                GXDLMS.Common.Error.ShowError(this, Ex);
            }
        }
    }
}

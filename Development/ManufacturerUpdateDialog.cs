// Modified for the Intelmeter fork, 2026-10-17: DPI-aware native prompt. GPL-2.0-only; original notices retained.
using System;
using System.Drawing;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    internal static class ManufacturerUpdateDialog
    {
        internal static Form CreatePrompt()
        {
            var dialog = new Form
            {
                AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi,
                Font = Gurux.DLMS.UI.ApplicationTypography.BodyFont, Text = Properties.Resources.GXDLMSDirectorTxt,
                ClientSize = new Size(430, 165), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, BackColor = SystemColors.Window
            };
            var message = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(20, 20, 20, 12) };
            message.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46)); message.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var icon = SystemIcons.Question.ToBitmap(); dialog.Disposed += (_, _) => icon.Dispose();
            message.Controls.Add(new PictureBox { Image = icon, SizeMode = PictureBoxSizeMode.CenterImage, Dock = DockStyle.Fill }, 0, 0);
            message.Controls.Add(new Label { Text = Properties.Resources.UpdateManufacturersOnlineTxt, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseCompatibleTextRendering = false, AccessibleName = "Manufacturer settings update" }, 1, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(12, 10, 12, 10), FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = SystemColors.Control, TabIndex = 1 };
            var yes = new Button { Text = "Yes", DialogResult = DialogResult.Yes, Size = new Size(78, 30), TabIndex = 0, UseVisualStyleBackColor = true };
            var no = new Button { Text = "No", DialogResult = DialogResult.No, Size = new Size(78, 30), TabIndex = 1, UseVisualStyleBackColor = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(78, 30), TabIndex = 2, UseVisualStyleBackColor = true };
            actions.Controls.Add(cancel); actions.Controls.Add(no); actions.Controls.Add(yes);
            dialog.Controls.Add(message); dialog.Controls.Add(actions); dialog.AcceptButton = yes; dialog.CancelButton = cancel;
            return dialog;
        }
        internal static DialogResult Confirm(Form owner)
        {
            if (owner.IsDisposed) return DialogResult.Cancel;
            if (owner.InvokeRequired) return (DialogResult)owner.Invoke(new Func<DialogResult>(() => Confirm(owner)));
            using var dialog = CreatePrompt(); return dialog.ShowDialog(owner.TopLevelControl as Form ?? owner);
        }
    }
}

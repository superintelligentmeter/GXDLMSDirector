// superintelligentmeter contributors, 2026-10-17. DPI-aware dashboard save confirmation. GPL-2.0-only.
using System.Drawing;
using System.Windows.Forms;
using Gurux.DLMS.UI;
namespace GXDLMSDirector
{
    internal sealed class DashboardSavePrompt : Form
    {
        internal DashboardSavePrompt()
        {
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Font = Gurux.DLMS.UI.ApplicationTypography.BodyFont; Text = "Dashboards";
            ClientSize = new Size(400, 150); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            BackColor = SystemColors.Window;
            var message = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(20, 16, 20, 12) };
            message.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46)); message.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var icon = SystemIcons.Question.ToBitmap(); Disposed += (_, _) => icon.Dispose();
            message.Controls.Add(new PictureBox { Image = icon, SizeMode = PictureBoxSizeMode.CenterImage, Dock = DockStyle.Fill }, 0, 0);
            message.Controls.Add(new Label { Text = "Save workspace dashboard changes?", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseCompatibleTextRendering = false }, 1, 0);
            var actions = new ActionButtonPanel { Name = "DashboardPromptActions", Dock = DockStyle.Bottom, AutoSize = true, BackColor = SystemColors.Control, TabIndex = 1 };
            Button Action(string text, DialogResult result, int tab) => new Button { Text = text, DialogResult = result, Size = new Size(82, 30), TabIndex = tab, UseVisualStyleBackColor = true };
            var yes = Action("Yes", DialogResult.Yes, 0); var no = Action("No", DialogResult.No, 1); var cancel = Action("Cancel", DialogResult.Cancel, 2);
            actions.Controls.Add(cancel); actions.Controls.Add(no); actions.Controls.Add(yes);
            ButtonLayoutPolicy.Apply(actions, new[] { yes, no, cancel }, minimumWidth: 80, height: 28,
                horizontalInset: 12, topInset: 8, bottomInset: 8);
            Controls.Add(message); Controls.Add(actions); AcceptButton = yes; CancelButton = cancel;
        }
    }
}

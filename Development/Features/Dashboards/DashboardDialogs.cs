// Intelmeter contributors, 2026-10-17. Native dashboard-owned dialogs. GPL-2.0-only.
using System;
using System.Drawing;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    internal abstract class DashboardDialog : Form
    {
        protected readonly Panel Content = new() { Dock = DockStyle.Fill, Padding = new Padding(16) };
        protected readonly DashboardButton Confirm = new() { Text = "OK", DialogResult = DialogResult.OK, BackColor = Color.White, ForeColor = Color.FromArgb(24, 48, 78) };
        protected DashboardDialog(Font font, string title, Size size)
        {
            AutoScaleMode = AutoScaleMode.Dpi; Font = font; Text = title; ClientSize = size;
            FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; BackColor = Color.White;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(10, 6, 10, 10) };
            var cancel = new DashboardButton { Text = "Cancel", DialogResult = DialogResult.Cancel, BackColor = Color.White, ForeColor = Color.FromArgb(24, 48, 78), Size = new Size(82, 30), TabIndex = 1 };
            Confirm.Size = new Size(82, 30); Confirm.TabIndex = 0;
            actions.Controls.Add(cancel); actions.Controls.Add(Confirm); actions.TabIndex = 1; Content.TabIndex = 0;
            Controls.Add(Content); Controls.Add(actions); AcceptButton = Confirm; CancelButton = cancel;
        }
    }
    internal sealed class DashboardNameDialog : DashboardDialog
    {
        private readonly TextBox input;
        internal string Value => input.Text.Trim();
        internal DashboardNameDialog(Font font, string title, string value) : base(font, title, new Size(410, 145))
        {
            var label = new Label { Text = "Dashboard name", Dock = DockStyle.Top, Height = 25, TabStop = false };
            input = new TextBox { Text = value, MaxLength = 100, Dock = DockStyle.Top, AccessibleName = "Dashboard name", TabIndex = 0 };
            var field = DashboardFieldHost.Wrap(input); field.Dock = DockStyle.Top; field.Height = 34; Content.Controls.Add(field); Content.Controls.Add(label);
            input.TextChanged += (_, _) => Confirm.Enabled = !string.IsNullOrWhiteSpace(input.Text);
            Confirm.Enabled = !string.IsNullOrWhiteSpace(input.Text);
            Shown += (_, _) => { input.Focus(); input.SelectAll(); };
        }
    }
    internal sealed class DashboardRelinkDialog : DashboardDialog
    {
        internal readonly ComboBox From = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Saved meter binding", TabIndex = 0 };
        internal readonly ComboBox To = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Replace with loaded meter", TabIndex = 1 };
        internal DashboardRelinkDialog(Font font, string[] sources, object[] meters) : base(font, "Relink meter for this dashboard", new Size(490, 240))
        {
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 25, 40, 25, 40, 28 }) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            fields.Controls.Add(new Label { Text = "Saved meter binding", Dock = DockStyle.Fill }, 0, 0); fields.Controls.Add(DashboardFieldHost.Wrap(From), 0, 1);
            fields.Controls.Add(new Label { Text = "Replace with loaded meter", Dock = DockStyle.Fill }, 0, 2); fields.Controls.Add(DashboardFieldHost.Wrap(To), 0, 3);
            fields.Controls.Add(new Label { Text = "Only bindings in the current dashboard will change.", Dock = DockStyle.Fill, AutoEllipsis = true }, 0, 4);
            Content.Controls.Add(fields); From.Margin = To.Margin = new Padding(0, 0, 0, 8);
            From.Items.AddRange(sources); To.Items.AddRange(meters); Confirm.Text = "Relink";
            void Update() { Confirm.Enabled = From.SelectedIndex >= 0 && To.SelectedIndex >= 0; }
            From.SelectedIndexChanged += (_, _) => Update(); To.SelectedIndexChanged += (_, _) => Update();
            if (From.Items.Count > 0) From.SelectedIndex = 0; if (To.Items.Count > 0) To.SelectedIndex = 0; Update();
        }
    }
}

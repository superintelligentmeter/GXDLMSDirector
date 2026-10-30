// Superintelligent Meter contributors, 2026-10-30. Stable legacy settings layout. GPL-2.0-only.
using System.Drawing;
using System.Windows.Forms;
using Gurux.DLMS.UI;

namespace GXDLMSDirector
{
    internal static class SettingsDialogLayout
    {
        internal static void Apply(Form form, TabControl tabs, Button accept, Button cancel, params Control[] leadingActions)
        {
            // Apply the established typography before arranging the existing controls.
            ApplicationTypography.Apply(form);
            form.PerformAutoScale();
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.AutoScaleDimensions = new SizeF(form.DeviceDpi, form.DeviceDpi);
            form.SuspendLayout();
            try
            {
                int S(int value) => (int)System.Math.Round(value * form.DeviceDpi / 96d);
                var footer = new TableLayoutPanel { Name = "SettingsActions", Dock = DockStyle.Fill, AutoSize = true,
                    ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(S(8)) };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                FlowLayoutPanel Actions() => new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
                void AddAction(FlowLayoutPanel row, Control control)
                {
                    control.Dock = DockStyle.None; control.Anchor = AnchorStyles.None;
                    control.AutoSize = true; control.Margin = new Padding(0, 0, S(6), 0);
                    if (control is Button button) { button.AutoSizeMode = AutoSizeMode.GrowAndShrink; button.MinimumSize = new Size(S(75), S(24)); }
                    row.Controls.Add(control);
                }
                var left = Actions(); foreach (Control control in leadingActions) AddAction(left, control);
                var right = Actions(); AddAction(right, accept); AddAction(right, cancel);
                footer.Controls.Add(left, 0, 0); footer.Controls.Add(right, 2, 0);
                var root = new TableLayoutPanel { Name = "SettingsLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tabs.Dock = DockStyle.Fill; tabs.Margin = Padding.Empty; tabs.TabIndex = 0;
                footer.TabIndex = 1; root.Controls.Add(tabs, 0, 0); root.Controls.Add(footer, 0, 1);
                form.Controls.Add(root); root.BringToFront();
                form.AcceptButton = accept; form.CancelButton = cancel;
                // Identical explicit fonts make nested ContainerControls autoscale
                // separately during DPI changes. Inherit the same font instead;
                // retain deliberately different family, size or style overrides.
                void InheritMatchingFonts(Control parent)
                {
                    foreach (Control child in parent.Controls)
                    {
                        if (child.Font.Equals(parent.Font)) child.ResetFont();
                        if (child is not UpDownBase) InheritMatchingFonts(child);
                    }
                }
                InheritMatchingFonts(form);
            }
            finally { form.ResumeLayout(true); }
        }
    }
}

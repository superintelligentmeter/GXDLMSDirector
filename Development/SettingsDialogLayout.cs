// Superintelligent Meter contributors, 2026-10-30. Stable legacy settings layout. GPL-2.0-only.
using System.Drawing;
using System.Windows.Forms;
using Gurux.DLMS.UI;

namespace GXDLMSDirector
{
    internal static class SettingsDialogLayout
    {
        // Measured native operation rows retain separate leading/trailing workflows.
        // When associated with a list/editor, inherit its real outer horizontal rails.
        internal static void ApplySplitActions(Panel panel, Button[] leadingButtons, Button[] trailingButtons,
            Control associatedSurface = null, string name = "NativeSplitActions")
        {
            var row = new TableLayoutPanel { Name = name, Dock = DockStyle.Fill, AutoSize = true,
                ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var leading = new ActionButtonPanel { Name = name + "Leading", Dock = DockStyle.Fill };
            var trailing = new ActionButtonPanel { Name = name + "Trailing", Dock = DockStyle.Fill };
            ButtonLayoutPolicy.Apply(leading, leadingButtons, alignRight: false, minimumWidth: 80,
                height: 28, topInset: 0, bottomInset: 0, equalWidths: false);
            ButtonLayoutPolicy.Apply(trailing, trailingButtons, minimumWidth: 80,
                height: 28, topInset: 0, bottomInset: 0, wrap: false);
            row.Controls.Add(leading, 0, 0); row.Controls.Add(trailing, 1, 0);
            panel.Padding = Padding.Empty; panel.Controls.Add(row);
            bool fitting = false;
            Point OuterOffset(Control control)
            {
                var offset = Point.Empty;
                for (Control current = control; current != null; current = current.Parent) offset.Offset(current.Location);
                return offset;
            }
            void Fit()
            {
                if (fitting || panel.IsDisposed || panel.ClientSize.Width < 1 ||
                    panel.FindForm()?.WindowState == FormWindowState.Minimized) return;
                fitting = true;
                try
                {
                    int Scale(int value) => (int)System.Math.Round(value * panel.DeviceDpi / 96d);
                    int left = Scale(12), right = Scale(12);
                    if (associatedSurface != null && associatedSurface.Parent != null)
                    {
                        left = System.Math.Max(0, OuterOffset(associatedSurface).X - OuterOffset(panel).X);
                        right = System.Math.Max(0, panel.ClientSize.Width - left - associatedSurface.Width);
                    }
                    row.Padding = new Padding(left, Scale(8), right, Scale(10));
                    int height = row.GetPreferredSize(new Size(panel.ClientSize.Width, 0)).Height;
                    if (panel.Height != height) panel.Height = height;
                }
                finally { fitting = false; }
            }
            panel.Layout += (_, _) => Fit(); panel.SizeChanged += (_, _) => Fit();
            panel.DpiChangedAfterParent += (_, _) => Fit();
            leading.SizeChanged += (_, _) => Fit(); trailing.SizeChanged += (_, _) => Fit();
            if (associatedSurface != null)
            {
                associatedSurface.LocationChanged += (_, _) => Fit();
                associatedSurface.SizeChanged += (_, _) => Fit();
                associatedSurface.ParentChanged += (_, _) => Fit();
            }
            Fit();
        }

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
                void AddAction(FlowLayoutPanel row, Control control, bool leading = false)
                {
                    control.Dock = DockStyle.None; control.Anchor = AnchorStyles.None;
                    control.AutoSize = true; control.Margin = new Padding(0, 0, S(leading ? 8 : 6), 0);
                    if (control is Button button)
                    {
                        if (leading) ButtonLayoutPolicy.ApplyToButton(button, minimumWidth: 80, height: 28);
                        else { button.AutoSizeMode = AutoSizeMode.GrowAndShrink; button.MinimumSize = new Size(S(75), S(24)); }
                    }
                    row.Controls.Add(control);
                }
                var left = Actions(); foreach (Control control in leadingActions) AddAction(left, control, leading: true);
                var right = new ActionButtonPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Anchor = AnchorStyles.Left, Margin = Padding.Empty };
                AddAction(right, accept); AddAction(right, cancel);
                // Confirmations are a pure button group; keep the leading mixed
                // Initial Settings / Show as hex controls in their native workflow.
                ButtonLayoutPolicy.Apply(right, new[] { accept, cancel }, minimumWidth: 80, height: 28,
                    horizontalInset: 0, topInset: 0, bottomInset: 0, wrap: false);
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

// Superintelligent Meter contributors, 2026-10-31. GPL-2.0-only.
using System;
using System.Drawing;
using System.Windows.Forms;
using Gurux.Common;
using Gurux.DLMS.UI;

namespace GXDLMSDirector
{
    internal static class LibraryVersionsLayout
    {
        internal static void Apply(LibraryVersionsDlg dialog)
        {
            var list = (ListView)dialog.Controls["listView1"];
            var copy = (Button)dialog.Controls["CopyBtn"];
            var cancel = (Button)dialog.Controls["CancelBtn"];
            ApplicationTypography.Apply(dialog);
            dialog.PerformAutoScale();
            dialog.AutoScaleMode = AutoScaleMode.Dpi;
            dialog.AutoScaleDimensions = new SizeF(dialog.DeviceDpi, dialog.DeviceDpi);
            int S(int value) => (int)Math.Round(value * dialog.DeviceDpi / 96d);
            dialog.SuspendLayout();
            try
            {
                var layout = new TableLayoutPanel
                {
                    Name = "LibraryVersionsLayout", Dock = DockStyle.Fill,
                    ColumnCount = 1, RowCount = 2, Margin = Padding.Empty,
                    Padding = new Padding(S(12))
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var actions = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1,
                    Margin = new Padding(0, S(12), 0, 0), TabIndex = 1
                };
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var leading = new ActionButtonPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
                var trailing = new ActionButtonPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 1 };
                ButtonLayoutPolicy.Apply(leading, new[] { copy }, alignRight: false, minimumWidth: 80,
                    height: 28, horizontalInset: 0, topInset: 0, bottomInset: 0, wrap: false);
                ButtonLayoutPolicy.Apply(trailing, new[] { cancel }, minimumWidth: 80,
                    height: 28, horizontalInset: 0, topInset: 0, bottomInset: 0, wrap: false);
                actions.Controls.Add(leading, 0, 0);
                actions.Controls.Add(trailing, 1, 0);
                list.Dock = DockStyle.Fill;
                list.Margin = Padding.Empty;
                list.TabIndex = 0;
                layout.Controls.Add(list, 0, 0);
                layout.Controls.Add(actions, 0, 1);
                dialog.Controls.Add(layout);
                dialog.CancelButton = cancel;
                dialog.Padding = Padding.Empty;
                dialog.ClientSize = new Size(S(640), S(420));
                dialog.MinimumSize = new Size(S(420) + dialog.Width - dialog.ClientSize.Width,
                    S(260) + dialog.Height - dialog.ClientSize.Height);
                // Let descendants inherit the current monitor's font rather than
                // independently scaling copies of the startup font.
                void InheritFont(Control parent)
                {
                    foreach (Control child in parent.Controls)
                    {
                        if (child.Font.Equals(parent.Font)) child.ResetFont();
                        InheritFont(child);
                    }
                }
                InheritFont(dialog);
            }
            finally { dialog.ResumeLayout(true); }

            bool sizingColumns = false;
            void FitColumns(bool measure)
            {
                if (sizingColumns || list.Columns.Count != 3 || !list.IsHandleCreated) return;
                sizingColumns = true;
                try
                {
                    if (measure)
                    {
                        list.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
                        list.Columns[0].Width = Math.Max(S(180), Math.Min(S(320), list.Columns[0].Width));
                        list.AutoResizeColumn(1, ColumnHeaderAutoResizeStyle.ColumnContent);
                        list.Columns[1].Width = Math.Max(S(110), list.Columns[1].Width);
                    }
                    // Long locations remain available through native horizontal scrolling.
                    list.Columns[2].Width = Math.Max(S(180), list.ClientSize.Width
                        - list.Columns[0].Width - list.Columns[1].Width - SystemInformation.VerticalScrollBarWidth);
                }
                finally { sizingColumns = false; }
            }
            dialog.Shown += (_, _) => FitColumns(true);
            list.ClientSizeChanged += (_, _) => FitColumns(false);
            list.DpiChangedAfterParent += (_, _) => FitColumns(true);
        }
    }
}

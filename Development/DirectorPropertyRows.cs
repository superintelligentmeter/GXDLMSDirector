// Superintelligent Meter contributors, 2026-10-30. Consistent native property rows. GPL-2.0-only.
using System.Drawing;
using System.Windows.Forms;
using Gurux.DLMS.UI;

namespace GXDLMSDirector
{
    internal static class DirectorPropertyRows
    {
        // Existing simple dialogs retain their vertical and multiline layout. Only
        // the field rail and ordinary editor measure change when the window widens.
        internal static void AlignNativeRows(Control owner, params (Label Label, Control Editor)[] rows)
        {
            bool arranging = false;
            void Fit()
            {
                if (arranging || owner.IsDisposed || rows.Length == 0) return;
                arranging = true;
                try
                {
                    int S(int value) => (int)System.Math.Round(value * owner.DeviceDpi / 96d);
                    int left = rows[0].Label.Left;
                    int available = System.Math.Max(1, owner.ClientSize.Width - left - S(16));
                    int rail = System.Math.Min(S(EditorLayoutPolicy.LabelColumnWidth),
                        System.Math.Max(S(EditorLayoutPolicy.MinimumLabelColumnWidth), available / 3));
                    foreach (var row in rows)
                    {
                        bool multiline = row.Editor is TextBox text && text.Multiline;
                        int width = System.Math.Max(1, available - rail);
                        if (!multiline) width = System.Math.Min(width, S(EditorLayoutPolicy.FieldWidth));
                        row.Label.MaximumSize = new Size(System.Math.Max(1, rail - S(8)), 0);
                        row.Editor.Left = left + rail;
                        row.Editor.Width = width;
                    }
                }
                finally { arranging = false; }
            }
            owner.Layout += (_, _) => Fit();
            owner.DpiChangedAfterParent += (_, _) => Fit();
            owner.FontChanged += (_, _) => Fit();
            Fit();
        }

        internal static TableLayoutPanel Create(Control parent, string name)
        {
            var table = new TableLayoutPanel { Name = name, Dock = DockStyle.Top,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 0, Margin = Padding.Empty };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, EditorLayoutPolicy.LabelColumnWidth));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            parent.Controls.Add(table);
            table.BringToFront();
            EditorLayoutPolicy.ConfigurePropertyTable(table);
            return table;
        }

        internal static void Row(TableLayoutPanel table, Label label, Control editor)
        {
            int gap = (int)System.Math.Round(8 * table.DeviceDpi / 96d);
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            label.AutoSize = true; label.Dock = DockStyle.None; label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 0, gap, gap);
            editor.Dock = DockStyle.None; editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            // Native ComboBox strips MaximumSize.Height; top anchoring avoids a false zero-height centering offset.
            if (editor is ComboBox) editor.Anchor |= AnchorStyles.Top;
            editor.Margin = new Padding(0, 0, 0, gap);
            label.TabIndex = row * 2; editor.TabIndex = row * 2 + 1;
            table.Controls.Add(label, 0, row); table.Controls.Add(editor, 1, row);
            if (editor is ComboBox) MeasureRow(table, table.RowStyles[row], editor, label);
        }

        internal static void Span(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.None; control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(0, 0, 0, (int)System.Math.Round(8 * table.DeviceDpi / 96d));
            control.TabIndex = row * 2;
            table.Controls.Add(control, 0, row); table.SetColumnSpan(control, 2);
            if (!control.AutoSize)
            {
                control.Dock = DockStyle.Fill;
                MeasureRow(table, table.RowStyles[row], control, null);
            }
        }

        // TableLayoutPanel's AutoSize rows do not measure non-AutoSize native combo
        // and fixed group controls. Reserve their actual preferred height explicitly;
        // recalculate after font/DPI/width changes without scaling a previous result.
        private static void MeasureRow(TableLayoutPanel table, RowStyle style, Control control, Label label)
        {
            bool fitting = false;
            void Fit()
            {
                if (fitting || table.IsDisposed) return;
                fitting = true;
                try
                {
                    int height = control.GetPreferredSize(new Size(control.Width, 0)).Height + control.Margin.Vertical;
                    if (label != null) height = System.Math.Max(height, label.PreferredSize.Height + label.Margin.Vertical);
                    style.SizeType = SizeType.Absolute;
                    if (System.Math.Abs(style.Height - height) > 1) style.Height = height;
                }
                finally { fitting = false; }
            }
            table.Layout += (_, _) => Fit();
            table.FontChanged += (_, _) => Fit();
            table.DpiChangedAfterParent += (_, _) => Fit();
            Fit();
        }

        internal static Control WithAccessory(Control editor, Control accessory)
        {
            var row = new TableLayoutPanel { Name = editor.Name + "Row", AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            editor.Dock = DockStyle.None; editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            editor.Margin = Padding.Empty; editor.TabIndex = 0;
            accessory.Dock = DockStyle.None; accessory.Anchor = AnchorStyles.Left;
            accessory.Margin = new Padding((int)System.Math.Round(8 * editor.DeviceDpi / 96d), 0, 0, 0); accessory.TabIndex = 1;
            row.Controls.Add(editor, 0, 0); row.Controls.Add(accessory, 1, 0);
            return row;
        }

        // Bounded identifiers and paired addresses remain compact inside the same field rail.
        internal static Control Compact(Control editor)
        {
            var row = new FlowLayoutPanel { Name = editor.Name + "Compact", AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty };
            editor.Dock = DockStyle.None; editor.Anchor = AnchorStyles.None; editor.Margin = Padding.Empty;
            row.Controls.Add(editor); return row;
        }
    }
}

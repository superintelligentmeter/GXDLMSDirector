// Intelmeter contributors, 2026-10-17. Inline OBIS picker layout. GPL-2.0-only.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Gurux.DLMS.Objects;
namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm
    {
        private static string ObjectLabel(GXDLMSObject obj) => string.IsNullOrWhiteSpace(obj.Description) || string.Equals(obj.Description, "Invalid", StringComparison.OrdinalIgnoreCase) ? obj.ObjectType.ToString() : obj.Description;
        private void UpdatePickerAction()
        {
            if (pickerAdd != null) pickerAdd.Enabled = selectedObjects.Count > 0 && selectedObjects.Count <= 32 && attribute.SelectedItem != null && !string.IsNullOrWhiteSpace(widgetTitle.Text);
        }
        private void BuildPicker()
        {
            var headerRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = Scale(40), ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var title = new Label { Text = "Add OBIS objects", Font = strongFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            var close = Command("Close", () => { picker.Visible = false; ResizeWidgets(); }); close.Anchor = AnchorStyles.Right; close.Margin = Padding.Empty;
            headerRow.Controls.Add(title, 0, 0); headerRow.Controls.Add(close, 1, 0);
            var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = Scale(46), ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            selectedCount = new Label { Text = "0 selected", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = muted };
            pickerAdd = Command("Add to dashboard", AddPicked); pickerAdd.Anchor = AnchorStyles.Right; pickerAdd.Margin = Padding.Empty; pickerAdd.BackColor = Color.FromArgb(0, 120, 215); pickerAdd.ForeColor = Color.White;
            footer.Controls.Add(selectedCount, 0, 0); footer.Controls.Add(pickerAdd, 1, 0);
            pickerViewport = new DashboardSurface { Dock = DockStyle.Fill, AutoScroll = false };
            picker.Controls.Add(pickerViewport); picker.Controls.Add(footer); picker.Controls.Add(headerRow);
            headerRow.TabIndex = 0; pickerViewport.TabIndex = 1; footer.TabIndex = 2;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 13, Margin = Padding.Empty, Padding = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 22, 42, 22, 42, 28, 0, 0, 22, 42, 22, 42, 22, 42 }) layout.RowStyles.Add(height == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, Scale(height)));
            Label Caption(string text) => new Label { Text = text, ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
            meterPicker = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Meter", Margin = new Padding(0, 0, 0, Scale(8)) };
            meterPicker.SelectedIndexChanged += (_, _) => { device = (meterPicker.SelectedItem as MeterChoice)?.Meter; if (search != null) search.Clear(); FillPicker(); };
            search = new TextBox { AccessibleName = "Search meter objects", Dock = DockStyle.Fill, PlaceholderText = "Name or OBIS code", Margin = new Padding(0, 0, 0, Scale(8)) };
            pickerSummary = new Label { Dock = DockStyle.Fill, ForeColor = muted, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft };
            objectPicker = new ListView { AccessibleName = "Objects from selected meter", Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.Nonclickable, ShowItemToolTips = true, CheckBoxes = true, FullRowSelect = true, BorderStyle = BorderStyle.None, HideSelection = false, Margin = Padding.Empty };
            objectPicker.Columns.Add("Object"); objectPicker.Columns.Add("OBIS"); objectPicker.Columns.Add("Status");
            var rowHeight = new ImageList { ImageSize = new Size(1, Scale(28)) }; objectPicker.SmallImageList = rowHeight; objectPicker.Disposed += (_, _) => rowHeight.Dispose();
            objectPicker.SizeChanged += (_, _) =>
            {
                int available = Math.Max(60, objectPicker.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
                int obis = Math.Min(Scale(108), available / 3), state = Math.Min(Scale(60), available / 4);
                objectPicker.Columns[0].Width = available - obis - state; objectPicker.Columns[1].Width = obis; objectPicker.Columns[2].Width = state;
            };
            pickerReason = new Label { Visible = false, AutoEllipsis = true, Dock = DockStyle.Fill, ForeColor = muted, Margin = Padding.Empty, Padding = new Padding(0, Scale(5), 0, 0), Text = "Select an object to see its availability." };
            attribute = new ComboBox { AccessibleName = "Attribute", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 0, Scale(8)) };
            display = new ComboBox { AccessibleName = "Display as", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 0, Scale(8)) }; display.Items.AddRange(new object[] { "Value tiles", "Trend chart", "Table" }); display.SelectedIndex = 0;
            widgetTitle = new TextBox { AccessibleName = "Widget title", Dock = DockStyle.Fill, Text = "Measurements", MaxLength = 100, Margin = new Padding(0, 0, 0, Scale(8)) };
            Control[] rows = { Caption("Meter"), DashboardFieldHost.Wrap(meterPicker), Caption("Search"), DashboardFieldHost.Wrap(search), pickerSummary, objectPicker, pickerReason, Caption("Attribute"), DashboardFieldHost.Wrap(attribute), Caption("Display as"), DashboardFieldHost.Wrap(display), Caption("Title"), DashboardFieldHost.Wrap(widgetTitle) };
            for (int i = 0; i < rows.Length; i++) { rows[i].TabIndex = i; layout.Controls.Add(rows[i], 0, i); }
            pickerViewport.Controls.Add(layout);
            foreach (Control input in new Control[] { meterPicker, search, objectPicker, attribute, display, widgetTitle }) input.Font = Font;
            void Fit()
            {
                // Keep the frame stationary. Only the object list scrolls; short windows
                // put labels beside their fields so every action remains reachable.
                bool compact = pickerViewport.ClientSize.Height < Scale(500);
                layout.SuspendLayout();
                try
                {
                    layout.ColumnStyles[0].Width = compact ? Scale(76) : 0;
                    int[] heights = compact ? new[] { 30, 30, 22, 0, 0, 30, 30, 30, 0, 0, 0, 0, 0 } : new[] { 22, 42, 22, 42, 28, 0, 0, 22, 42, 22, 42, 22, 42 };
                    int[] rowMap = { 0, 0, 1, 1, 2, 3, 4, 5, 5, 6, 6, 7, 7 };
                    int listRow = compact ? 3 : 5;
                    for (int i = 0; i < heights.Length; i++) { layout.RowStyles[i].SizeType = i == listRow ? SizeType.Percent : SizeType.Absolute; layout.RowStyles[i].Height = i == listRow ? 100 : Scale(heights[i]); }
                    for (int i = 0; i < rows.Length; i++)
                    {
                        bool field = rows[i] is DashboardFieldHost;
                        bool caption = i == 0 || i == 2 || i == 7 || i == 9 || i == 11;
                        layout.SetCellPosition(rows[i], new TableLayoutPanelCellPosition(compact && field ? 1 : 0, compact ? rowMap[i] : i));
                        layout.SetColumnSpan(rows[i], compact && (field || caption) ? 1 : 2);
                        if (field) rows[i].Margin = new Padding(0, 0, 0, Scale(compact ? 2 : 8));
                    }
                }
                finally { layout.ResumeLayout(true); }
            }
            pickerViewport.SizeChanged += (_, _) => Fit(); Fit();
            attribute.SelectedIndexChanged += (_, _) => UpdatePickerAction(); widgetTitle.TextChanged += (_, _) => UpdatePickerAction();
            search.TextChanged += (_, _) => FillPicker();
            objectPicker.SelectedIndexChanged += (_, _) => { if (objectPicker.SelectedItems.Count > 0) { pickerReason.Text = objectPicker.SelectedItems[0].ToolTipText; dashboardTips.SetToolTip(pickerReason, pickerReason.Text); } };
            objectPicker.ItemCheck += (_, e) =>
            {
                if (fillingPicker || e.NewValue != CheckState.Checked) return;
                string reason = UnavailableReason((GXDLMSObject)objectPicker.Items[e.Index].Tag);
                if (reason != null) { e.NewValue = CheckState.Unchecked; pickerReason.Text = reason; }
            };
            objectPicker.ItemChecked += (_, e) => { if (fillingPicker) return; var o = (GXDLMSObject)e.Item.Tag; if (e.Item.Checked) selectedObjects.Add(o); else selectedObjects.Remove(o); UpdateAttributes(); };
            UpdatePickerAction();
        }
    }
}

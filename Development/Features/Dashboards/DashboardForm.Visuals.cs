// Intelmeter contributors, 2026-10-17. Dashboard module. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Gurux.DLMS.Objects;
namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm
    {
        private void BuildTable(Panel box, DashboardWidget w)
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White };
            grid.AccessibleName = w.Title + " measurements"; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BorderStyle = BorderStyle.None; grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 247, 251);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ink;
            grid.DefaultCellStyle.ForeColor = ink; grid.GridColor = border;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.RowTemplate.Height = Scale(25); grid.ColumnHeadersHeight = Scale(28);
            foreach (string name in new[] { "Measurement", "OBIS", "Attribute", "Value", "Status", "Last success", "Meter" }) grid.Columns.Add(name, name);
            int[] minimums = { 150, 115, 65, 100, 120, 145, 150 };
            for (int i = 0; i < grid.Columns.Count; i++) { grid.Columns[i].MinimumWidth = Scale(minimums[i]); grid.Columns[i].FillWeight = i == 0 || i == 6 ? 150 : 100; }
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            foreach (var b in w.Bindings) grid.Rows.Add(b.Label, b.Obis, b.Attribute);
            grid.ClearSelection(); grid.CurrentCell = null;
            box.Controls.Add(grid);
            refreshViews.Add(() => { for (int i = 0; i < w.Bindings.Count; i++) { var b = w.Bindings[i]; var s = State(b.Key); grid.Rows[i].Cells[6].Value = MeterLabel(b); grid.Rows[i].Cells[3].Value = s.Text + " " + s.Unit; grid.Rows[i].Cells[4].Value = Quality(b); grid.Rows[i].Cells[5].Value = s.Success?.ToLocalTime().ToString("G") ?? "Never"; } });
        }
        private static string ShortChartLabel(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        private void BuildChart(Panel box, DashboardWidget w)
        {
            var chart = new Chart { MinimumSize = new Size(1, 1), Dock = DockStyle.Fill, AccessibleName = w.Title + " trend chart" };
            chart.ChartAreas.Add(new ChartArea("Live")); chart.Legends.Add(new Legend { Docking = Docking.Bottom, Alignment = StringAlignment.Center });
            var area = chart.ChartAreas[0]; area.AxisX.Title = "Local time | latest 1,000 samples per attribute"; area.AxisX.LabelStyle.Format = "HH:mm:ss"; area.AxisY.Title = "Value (see series units)"; area.AxisY.IsStartedFromZero = false;
            foreach (var b in w.Bindings) chart.Series.Add(new Series(b.Key) { ChartType = SeriesChartType.Line, XValueType = ChartValueType.DateTime, BorderWidth = 2 });
            box.Controls.Add(chart);
            var empty = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = muted, BackColor = Color.White, Padding = new Padding(16), Text = "No numeric readings yet. Read now or enable Monitor to start the trend." };
            box.Controls.Add(empty); empty.BringToFront();
            var lastCounts = new Dictionary<string, DateTime?>();
            var areas = new Dictionary<string, string>();
            refreshViews.Add(() =>
            {
                bool hasNumeric = w.Bindings.Any(b => State(b.Key).Numeric && State(b.Key).Success.HasValue);
                empty.Visible = !hasNumeric; chart.Visible = hasNumeric;
                if (!hasNumeric) return;
                foreach (var b in w.Bindings)
                {
                    var s = State(b.Key); var series = chart.Series[b.Key];
                    series.Enabled = s.Numeric && s.Success.HasValue;
                    if (!series.Enabled) continue;
                    string unit = string.IsNullOrEmpty(s.Unit) ? "Unit unspecified" : s.Unit;
                    if (!areas.TryGetValue(unit, out var areaName))
                    {
                        areaName = areas.Count == 0 ? "Live" : "Unit" + Guid.NewGuid().ToString("N");
                        if (areas.Count != 0) chart.ChartAreas.Add(new ChartArea(areaName));
                        var pane = chart.ChartAreas[areaName];
                        pane.AxisX.Title = "Time"; pane.AxisX.MajorGrid.Enabled = false; pane.AxisY.MajorGrid.LineColor = border; pane.AxisX.LineColor = border; pane.AxisY.LineColor = border; pane.AxisX.LabelStyle.Format = "HH:mm:ss";
                        pane.AxisY.Title = unit; pane.AxisY.IsStartedFromZero = false;
                        areas[unit] = areaName;
                    }
                    series.ChartArea = areaName; series.MarkerStyle = s.Points.Count(p => !double.IsNaN(p.Value)) == 1 ? MarkerStyle.Circle : MarkerStyle.None; series.MarkerSize = 5;
                    series.LegendText = ShortChartLabel(MeterLabel(b), 30) + " / " + ShortChartLabel(b.Label, 32); series.LegendToolTip = MeterLabel(b) + " / " + b.Label; series.ToolTip = b.Label + ": #VALY " + s.Unit;
                    series.Color = new[] { Color.FromArgb(0, 120, 215), Color.FromArgb(0, 155, 150), Color.SlateGray }[w.Bindings.IndexOf(b) % 3];
                    DateTime? newest = s.Points.Count == 0 ? null : s.Points[s.Points.Count - 1].Time;
                    if (lastCounts.TryGetValue(b.Key, out var previous) && previous == newest) continue;
                    lastCounts[b.Key] = newest;
                    series.Points.Clear();
                    foreach (var point in s.Points.Where(p => p.Time >= DateTime.UtcNow.AddMinutes(-chartMinutes)))
                        if (double.IsNaN(point.Value)) series.Points.Add(new DataPoint(point.Time.ToLocalTime().ToOADate(), 0) { IsEmpty = true });
                        else series.Points.AddXY(point.Time.ToLocalTime(), point.Value);
                }
                foreach (var pane in chart.ChartAreas)
                {
                    pane.AxisX.Minimum = DateTime.Now.AddMinutes(-chartMinutes).ToOADate();
                    pane.AxisX.Maximum = DateTime.Now.ToOADate();
                    pane.AxisX.LabelStyle.Format = "HH:mm";
                }
                string activeArea = chart.Series.First(x => x.Enabled).ChartArea;
                foreach (var series in chart.Series.Where(x => !x.Enabled)) series.ChartArea = activeArea;
                // Distinct units have separate axes; never plot volts and amps on one scale.
                foreach (var pane in chart.ChartAreas.Cast<ChartArea>().ToArray())
                    if (!chart.Series.Any(x => x.Enabled && x.ChartArea == pane.Name)) { chart.ChartAreas.Remove(pane); areas.Remove(areas.First(x => x.Value == pane.Name).Key); }
            });
        }
    }
}

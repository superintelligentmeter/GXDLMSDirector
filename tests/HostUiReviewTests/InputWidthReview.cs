// Superintelligent Meter contributors, 2026-10-30. Native editor width/alignment review. GPL-2.0-only.
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using Gurux.DLMS.UI;

internal static partial class Program
{
    static bool inputReview;
    static readonly List<object> InputResults = new();
    static void CaptureInputWidths(Form form, string name, string state)
    {
        var tables = Descendants(form).OfType<TableLayoutPanel>().Where(t => t.Visible &&
            new[] { "NotificationFields", "ManufacturerGeneralFields", "ManufacturerSecurityFields" }.Contains(t.Name)).ToArray();
        foreach (var table in tables)
        {
            double scale = table.DeviceDpi / 96d;
            string context = name + " " + state + " " + table.Name;
            Check(table.Width <= Math.Ceiling(EditorLayoutPolicy.SectionWidth * scale) + 1, context + ": property section respects the DPI-aware width limit");
            var editors = table.Controls.Cast<Control>().Where(c => table.GetColumn(c) == 1 && table.GetColumnSpan(c) == 1).ToArray();
            Check(editors.All(c => c.Width <= Math.Ceiling(EditorLayoutPolicy.FieldWidth * scale) + 1), context + ": ordinary fields stay bounded");
            var rows = table.Controls.Cast<Control>().Where(c => c.Visible).GroupBy(c => table.GetRow(c)).OrderBy(g => g.Key).ToArray();
            if (!rows.Zip(rows.Skip(1), (a, b) => a.Max(c => c.Bottom) <= b.Min(c => c.Top)).All(ok => ok)) Console.WriteLine(string.Join(" | ", table.Controls.Cast<Control>().Select(c => c.Name + "=" + c.Bounds + " min=" + c.MinimumSize + " max=" + c.MaximumSize + " pref=" + c.PreferredSize + " row=" + table.GetRow(c))));
            Check(rows.Zip(rows.Skip(1), (a, b) => a.Max(c => c.Bottom) <= b.Min(c => c.Top)).All(ok => ok),
                context + ": measured native input rows do not overlap following rows");
            Check(editors.Select(c => c.Left).Distinct().Count() <= 1 && editors.Select(c => c.Right).Distinct().Count() <= 1, context + ": peer field groups share both edges");
        }
        string[] peerNames = name switch
        {
            "MacroDelay" => new[] { "NameTb", "DelayTb" },
            "MacroEdit" => new[] { "NameTb" },
            "PlcRegister" => new[] { "ActiveInitiatorTb", "SystemTitleTb", "MACAddressTb" },
            _ => Array.Empty<string>()
        };
        if (peerNames.Length > 0)
        {
            var peers = peerNames.Select(n => (Control)Field(form, n)).ToArray();
            Check(peers.All(c => c.Width <= Math.Ceiling(EditorLayoutPolicy.FieldWidth * c.DeviceDpi / 96d) + 1)
                && peers.Select(c => c.Left).Distinct().Count() == 1 && peers.Select(c => c.Right).Distinct().Count() == 1,
                name + " " + state + ": native scalar peers share bounded edges");
        }
        if (name == "Settings")
        {
            var keys = new[] { "SystemTitleTB", "BlockCipherKeyTB", "AuthenticationKeyTB" }.Select(n => (Control)Field(form, n)).ToArray();
            if (keys.All(c => c.Visible))
            {
                var boxes = keys.Select(c => c.RectangleToScreen(c.ClientRectangle)).ToArray();
                Check(boxes.Select(c => c.Left).Distinct().Count() == 1 && boxes.Select(c => c.Right).Distinct().Count() == 1,
                    name + " " + state + ": all notification key editors share input edges");
            }
        }
        if (name == "ObisCode")
        {
            var selector = (Control)Field(form, "InterfaceCB"); var grid = (Control)Field(form, "ObisPropertyGrid");
            Check(selector.Left == grid.Left && selector.Width <= Math.Ceiling(EditorLayoutPolicy.FieldWidth * form.DeviceDpi / 96d) + 1,
                name + " " + state + ": selector aligns with the data grid and remains bounded");
        }
        var inputs = Descendants(form).Where(c => c.Visible && (c is TextBoxBase || c is ComboBox || c is NumericUpDown || c is DateTimePicker))
            .Select(c => new { c.Name, type = c.GetType().Name, c.DeviceDpi, widthLogical = c.Width * 96d / c.DeviceDpi,
                bounds = c.Bounds.ToString(), screenBounds = c.RectangleToScreen(c.ClientRectangle).ToString(),
                parent = c.Parent.Name, parentType = c.Parent.GetType().Name, c.Enabled, font = c.Font.SizeInPoints,
                accessible = c.AccessibleName, scrollAncestors = Ancestors(c).OfType<ScrollableControl>().Where(p => p.AutoScroll).Select(p => p.Name).ToArray() }).ToArray();
        InputResults.Add(new { screen = name, state, form.DeviceDpi, inputs });
        File.WriteAllText(Path.Combine(output, "input-width-review.json"), JsonSerializer.Serialize(InputResults, new JsonSerializerOptions { WriteIndented = true }));
    }
    static IEnumerable<Control> Ancestors(Control c) { for (var p = c.Parent; p != null; p = p.Parent) yield return p; }
}

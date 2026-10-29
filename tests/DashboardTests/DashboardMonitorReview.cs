// Intelmeter contributors, 2026-10-17. Real mixed-monitor dashboard regression. GPL-2.0-only.
using System.Drawing;
using System.Windows.Forms;
using GXDLMSDirector;
internal static partial class Program
{
    private static void SettleDisplay()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 250) { Application.DoEvents(); Thread.Sleep(10); }
    }
    private static void ReviewMixedMonitors(Form main, DashboardForm form, DashboardDefinition definition, string folder)
    {
        var displays = Screen.AllScreens.OrderByDescending(screen => screen.Primary).ToArray();
        var visitedDpi = new HashSet<int>();
        var binding = definition.Widgets[0].Bindings[0]; binding.Size = new DashboardCardSize { Width = 300, Height = 180 }; Call(form, "Render");
        string originalLayout = System.Text.Json.JsonSerializer.Serialize(definition);
        for (int cycle = 0; cycle < 2; cycle++) foreach (var display in displays.Concat(displays.Take(1)))
        {
            main.WindowState = FormWindowState.Normal;
            var area = display.WorkingArea; main.Bounds = new Rectangle(area.Left + 24, area.Top + 24, Math.Min(1500, area.Width - 48), Math.Min(940, area.Height - 48));
            SettleDisplay(); visitedDpi.Add(main.DeviceDpi);
            Check(Math.Abs(form.Font.SizeInPoints - main.Font.SizeInPoints) < 0.1, "Normal dashboard matches application font after actual monitor move");
            Call(form, "Render"); SettleDisplay();
            Check(Descendants(form).OfType<Label>().Where(label => Equals(label.Tag, "DashboardValue")).All(label => Math.Abs(label.Font.SizeInPoints / main.Font.SizeInPoints - 19f / 9f) < 0.02), "Rebuilt tiles use the application's current font scale");
            // Open the previously hidden pane after moving, before any maximize
            // can incidentally repair its deferred child DPI messages.
            Call(form, "OpenPicker", (object)null); SettleDisplay();
            var openedPicker = (Panel)Field(form, "picker");
            File.AppendAllText(Path.Combine(folder, "picker-font-observations.txt"), display.DeviceName + "; " + string.Join("; ", Descendants(openedPicker).Select(control => control.GetType().Name + "=" + control.Font.SizeInPoints)) + Environment.NewLine);
            Check(Descendants(openedPicker).All(control => Math.Abs(control.Font.SizeInPoints - form.Font.SizeInPoints) < 0.1), "Reopened picker typography matches dashboard before maximize");
            var openedHeading = (Control)Field(form, "heading"); var openedActions = (Control)Field(form, "headingActions");
            Check(!openedHeading.Bounds.IntersectsWith(openedActions.Bounds), "Dashboard heading cannot cover the first header button");
            Capture(main, Path.Combine(folder, "picker-open-" + cycle + "-" + main.DeviceDpi + ".png"));
            foreach (bool open in new[] { true, false })
            {
                if (open) Call(form, "OpenPicker", (object)null); else ((Panel)Field(form, "picker")).Visible = false;
                main.WindowState = FormWindowState.Minimized; SettleDisplay();
                main.WindowState = FormWindowState.Maximized; SettleDisplay();
                string name = "monitor-" + cycle + "-" + display.DeviceName.Replace("\\", "").Replace(".", "") + "-" + main.DeviceDpi + "-" + open;
                Capture(main, Path.Combine(folder, name + ".png"));
                File.AppendAllText(Path.Combine(folder, "monitor-observations.txt"), name + "; main=" + main.DeviceDpi + "; dashboard=" + form.DeviceDpi + "; host=" + main.Font.SizeInPoints + "; tree=" + ((Control)Field(main, "ObjectTree")).Font.SizeInPoints + "; body=" + form.Font.SizeInPoints + "; title=" + ((Label)Field(form, "heading")).Font.SizeInPoints + Environment.NewLine);
                Check(form.DeviceDpi == main.DeviceDpi, "Embedded dashboard follows real monitor DPI: " + name);
                Check(Math.Abs(form.Font.SizeInPoints - ((Control)Field(main, "ObjectTree")).Font.SizeInPoints) < 0.1 && Math.Abs(((Label)Field(form, "heading")).Font.SizeInPoints / form.Font.SizeInPoints - 15f / 9f) < 0.02, "Dashboard matches application tree and preserves heading ratio: " + name);
                var header = (Panel)Field(form, "header"); var actions = (Panel)Field(form, "headingActions");
                Check(header.Visible && header.Width == form.ClientSize.Width - form.Padding.Horizontal, "Monitor transition retains full dashboard header: " + name);
                Check(actions.Controls.Cast<Control>().All(control => actions.ClientRectangle.Contains(control.Bounds)), "Header buttons fit their action row: " + name);
                var originalActionBounds = actions.Controls.Cast<Control>().Select(control => control.Bounds).ToArray();
                var edit = (Button)Field(form, "editLayoutButton");
                edit.PerformClick(); SettleDisplay();
                Check(actions.Controls.Cast<Control>().Select(control => control.Bounds).SequenceEqual(originalActionBounds), "Edit/Done toggle keeps every header button stationary: " + name);
                edit.PerformClick(); SettleDisplay();
                Check(actions.Controls.Cast<Control>().Select(control => control.Bounds).SequenceEqual(originalActionBounds), "Returning from edit keeps every header button stationary: " + name);
                var commands = (Panel)Field(form, "commands"); var frame = (Panel)Field(form, "monitoringFrame");
                Check(frame.ClientRectangle.Contains(commands.Bounds) && commands.Controls.Cast<Control>().All(control => commands.ClientRectangle.Contains(control.Bounds)), "Monitoring controls fit after monitor change: " + name);
                var interval = (Control)Field(form, "intervalField"); var read = (Control)Field(form, "readNow"); var monitor = (Control)Field(form, "monitor");
                var lastAction = actions.Controls[actions.Controls.Count - 1];
                Check(Math.Abs(lastAction.PointToScreen(new Point(lastAction.Width, 0)).X - monitor.PointToScreen(new Point(monitor.Width, 0)).X) <= 2, "Header and monitoring buttons share the right edge: " + name);
                Check(Math.Abs(interval.Top + interval.Height / 2 - read.Top - read.Height / 2) <= 1 && Math.Abs(read.Top + read.Height / 2 - monitor.Top - monitor.Height / 2) <= 1, "Refresh field and buttons share a center line: " + name);
                var cards = ((System.Collections.IEnumerable)Field(form, "layoutCards")).Cast<Control>().ToArray();
                var cardMap = (System.Collections.IDictionary)Field(form, "cardItems");
                var savedCard = cardMap.Keys.Cast<Control>().Single(card => ReferenceEquals(cardMap[card].GetType().GetProperty("Binding").GetValue(cardMap[card]), binding));
                Check(Descendants(savedCard).OfType<Label>().Where(label => Equals(label.Tag, "DashboardValue")).All(label => Math.Abs(label.Font.SizeInPoints / form.Font.SizeInPoints - 19f / 9f) < 0.02), "Tile value font preserves its application-relative size: " + name);
                Check(Descendants(form).OfType<DashboardButton>().All(button => Math.Abs(button.Height - 32 * form.DeviceDpi / 96.0) <= 1), "Shared button heights do not accumulate DPI scaling: " + name);
                Check(Math.Abs(savedCard.Width - 300 * form.DeviceDpi / 96.0) <= 2, "Saved tile width scales once: " + name);
                Check(Math.Abs(savedCard.Height - 180 * form.DeviceDpi / 96.0) <= 2, "Saved tile height scales once: " + name);
                Check(System.Text.Json.JsonSerializer.Serialize(definition) == originalLayout, "Monitor moves preserve saved layout dimensions and order: " + name);
                if (open)
                {
                    var viewport = (Panel)Field(form, "pickerViewport");
                    Check(!viewport.VerticalScroll.Visible, "Monitor transition does not restore outer picker scrolling: " + name);
                    foreach (string field in new[] { "meterPicker", "search", "attribute", "display", "widgetTitle", "objectPicker" })
                    {
                        var editor = (Control)Field(form, field); var control = editor.Parent is DashboardFieldHost ? editor.Parent : editor;
                        Check(viewport.ClientRectangle.Contains(new Rectangle(viewport.PointToClient(control.PointToScreen(Point.Empty)), control.Size)), "Picker field fits on real monitor: " + field + " " + name);
                    }
                }
                main.WindowState = FormWindowState.Normal; SettleDisplay();
            }
        }
        // A never-opened pane creates its native child handles at the destination
        // DPI; cover that separately from reopening an already-created pane.
        using (var fresh = new DashboardForm(null, storagePath: Path.Combine(folder, "fresh-hidden-picker.json")))
        {
            var origin = displays[0].WorkingArea;
            fresh.StartPosition = FormStartPosition.Manual; fresh.Bounds = new Rectangle(origin.Left + 30, origin.Top + 30, 1100, 800); fresh.Show(); SettleDisplay();
            var destination = displays.Last().WorkingArea;
            fresh.Location = new Point(destination.Left + 30, destination.Top + 30); SettleDisplay();
            Call(fresh, "OpenPicker", (object)null); SettleDisplay();
            var firstPicker = (Panel)Field(fresh, "picker");
            Check(Descendants(firstPicker).All(control => Math.Abs(control.Font.SizeInPoints - fresh.Font.SizeInPoints) < 0.1), "First-opened picker after monitor move matches dashboard typography");
            Capture(fresh, Path.Combine(folder, "picker-first-open-destination.png"));
        }
        File.AppendAllText(Path.Combine(folder, "monitor-observations.txt"), "Distinct actual DPI values: " + string.Join(",", visitedDpi) + Environment.NewLine);
        if (displays.Length > 1) Check(visitedDpi.Count > 1, "Test exercised real monitors at different DPI values");
        binding.Size = null; typeof(DashboardForm).GetField("dirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(form, false);
    }
}

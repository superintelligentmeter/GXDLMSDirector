// Intelmeter contributors, 2026-10-17. Realistic dashboard screen fixtures. GPL-2.0-only.
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GXDLMSDirector;
using Gurux.DLMS.Objects;
using Gurux.DLMS.Enums;
internal static partial class Program
{
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Capture(Control control, string path)
    {
        Application.DoEvents(); control.Refresh();
        using var shot = new Bitmap(control.Width, control.Height); control.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size)); shot.Save(path);
    }
    private static void ReviewDashboardScreens(string directory)
    {
        string folder = Path.Combine(directory, "ui-review"); Directory.CreateDirectory(folder);
        using (var noMeters = new DashboardForm(null, storagePath: Path.Combine(folder, "no-meters.json")))
        {
            noMeters.Size = new Size(900, 650); noMeters.Show(); Application.DoEvents(); Call(noMeters, "OpenPicker", (object)null);
            Capture(noMeters, Path.Combine(folder, "00-no-meters.png"));
            Check(((Label)Field(noMeters, "pickerReason")).Text.Contains("Load a meter"), "Empty picker explains how to load a meter");
            Check(!((Button)Field(noMeters, "pickerAdd")).Enabled, "Empty picker cannot add an invalid measurement");
        }
        using (var prompt = ManufacturerUpdateDialog.CreatePrompt())
        {
            prompt.Show(); Application.DoEvents(); Capture(prompt, Path.Combine(folder, "12-manufacturer-prompt.png"));
            Check(prompt.AutoScaleMode == AutoScaleMode.Dpi && prompt.Font.SizeInPoints == 9, "Manufacturer prompt uses DPI-aware application typography");
            var actions = Descendants(prompt).OfType<Button>().ToArray();
            Check(actions.Length == 3 && actions.All(button => button.Parent.ClientRectangle.Contains(button.Bounds)), "Manufacturer prompt shows all three native actions without clipping");
            Check(((Button)prompt.AcceptButton).DialogResult == DialogResult.Yes && ((Button)prompt.CancelButton).DialogResult == DialogResult.Cancel, "Manufacturer prompt preserves Yes and Cancel keyboard actions");
            prompt.Close();
        }
        foreach (var choice in new[] { DialogResult.Cancel, DialogResult.No, DialogResult.Yes })
        {
            string blockedParent = Path.Combine(folder, "save-prompt-blocker-" + choice); File.WriteAllText(blockedParent, "block directory creation");
            string savePath = Path.Combine(blockedParent, "workspace.json");
            using var workspace = new DashboardForm(null, storagePath: savePath);
            workspace.Show(); Application.DoEvents();
            typeof(DashboardForm).GetField("dirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(workspace, true);
            using var choose = new System.Windows.Forms.Timer { Interval = 100 };
            choose.Tick += (_, _) =>
            {
                var prompt = Application.OpenForms.OfType<DashboardSavePrompt>().Single(); choose.Stop();
                if (choice == DialogResult.Cancel)
                {
                    Capture(prompt, Path.Combine(folder, "13-dashboard-save-prompt.png"));
                    Check(ReferenceEquals(prompt.Owner, workspace), "Save prompt belongs to the actual top-level window");
                    Check(prompt.AutoScaleMode == AutoScaleMode.Dpi && prompt.Font.SizeInPoints == workspace.Font.SizeInPoints, "Save prompt uses the current body font and DPI layout");
                    Check(Descendants(prompt).OfType<Button>().All(button => button.Parent.ClientRectangle.Contains(button.Bounds)), "Save prompt actions are fully visible");
                    Check(((Button)prompt.AcceptButton).DialogResult == DialogResult.Yes && ((Button)prompt.CancelButton).DialogResult == DialogResult.Cancel, "Save prompt preserves Enter and Escape actions");
                }
                Descendants(prompt).OfType<Button>().Single(button => button.DialogResult == choice).PerformClick();
            };
            choose.Start();
            Check(workspace.PrepareToClose() == (choice == DialogResult.No), "Save prompt close behavior: " + choice);
            Check(!File.Exists(savePath), "Failed autosave never claims a written dashboard: " + choice);
            typeof(DashboardForm).GetField("dirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(workspace, false);
        }
        using var a = new DisposableDevice(); using var b = new DisposableDevice();
        a.Value.Name = "Main building · Incoming supply meter"; b.Value.Name = "Workshop · Distribution board meter";
        for (int n = 0; n < 78; n++)
        {
            var obj = new GXDLMSRegister("1.0." + (n + 1) + ".7.0.255") { Description = n == 0 ? "Phase L1 voltage — main incoming supply" : "Measurement " + (n + 1) + " · feeder monitoring" };
            if (n % 13 == 0) for (int i = 1; i <= 3; i++) obj.SetAccess(i, AccessMode.NoAccess);
            a.Value.Objects.Add(obj);
        }
        b.Value.Objects.Add(new GXDLMSRegister("1.0.32.7.0.255") { Description = "Phase L1 voltage — workshop" });
        using var main = new OfflineMainForm(); main.DashboardStoragePath = Path.Combine(folder, "workspace.json");
        main.Show(); main.Size = new Size(1500, 940); Application.DoEvents();
        ((IList)Field(main, "Devices")).Add(a.Value); ((IList)Field(main, "Devices")).Add(b.Value);
        Call(main, "ShowDashboardScreen", true); Call(main, "DisplayDashboard", a.Value, null);
        var form = (DashboardForm)Field(main, "activeDashboard");
        var doc = (DashboardDocument)Field(form, "document"); var definition = doc.Dashboards[0]; definition.Name = "Building energy and supply quality overview";
        Capture(main, Path.Combine(folder, "01-empty.png"));
        var voltage = Binding(a.Value.Objects[1]); voltage.Label = "Incoming supply L1 voltage with a long measurement name";
        var workshop = Binding(b.Value.Objects[0]);
        definition.Widgets.Add(new DashboardWidget { Title = "Supply values", Bindings = new() { voltage, workshop, Binding(a.Value.Objects[2]) } });
        definition.Widgets.Add(new DashboardWidget { Kind = "Trend", Title = "Voltage trends across both buildings", Bindings = new() { voltage, workshop } });
        definition.Widgets.Add(new DashboardWidget { Kind = "Table", Title = "Supply measurements", Bindings = new() { voltage, workshop, Binding(a.Value.Objects[3]) } });
        Call(form, "BindDashboards", definition);
        Call(form, "ApplyReadings", new List<DashboardReading> { new(voltage.Key, "230.123456789012", "V", 230.12, null, DateTime.UtcNow.AddSeconds(-90)), new(workshop.Key, "[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]", "", null, "Meter did not respond after multiple transport retries. Check the connection and retry.", DateTime.UtcNow) });
        Call(form, "RefreshViews"); Capture(main, Path.Combine(folder, "02-populated.png"));
        Call(form, "OpenPicker", (object)null); Capture(main, Path.Combine(folder, "03-picker-many-objects.png"));
        foreach (string field in new[] { "meterPicker", "search", "attribute", "display", "widgetTitle", "interval" })
        {
            var editor = (Control)Field(form, field);
            Check(editor.Parent is DashboardFieldHost && editor.Font.SizeInPoints == form.Font.SizeInPoints, "Rounded field retains native editor and font: " + field);
        }
        var meterCombo = (ComboBox)Field(form, "meterPicker");
        meterCombo.Focus(); meterCombo.DroppedDown = true; Application.DoEvents();
        Check(meterCombo.DroppedDown, "Rounded meter field retains native dropdown behavior"); meterCombo.DroppedDown = false;
        var largePicker = (Panel)Field(form, "picker"); var largeCenter = (Panel)Field(form, "workspaceCenter");
        var initialCards = ((IEnumerable)Field(form, "layoutCards")).Cast<Control>().ToArray();
        Check(initialCards[0].Top == initialCards[1].Top && initialCards[1].Top == initialCards[2].Top, "Reference layout shows three value cards beside the picker at desktop size");
        Check(largePicker.Visible && largePicker.Width > 0 && largeCenter.Right <= largePicker.Left, "Desktop picker and dashboard occupy separate non-overlapping regions");
        for (int cycle = 0; cycle < 2; cycle++)
        {
            main.Size = new Size(900, 650); Application.DoEvents();
            main.WindowState = FormWindowState.Minimized; Application.DoEvents();
            main.WindowState = FormWindowState.Maximized; Application.DoEvents();
            Capture(main, Path.Combine(folder, "14-restored-maximized-" + cycle + ".png"));
            foreach (string chrome in new[] { "header", "monitoringFrame", "noticeBlock", "workspaceCenter" })
                Check(((Control)Field(form, chrome)).Visible, "Window-state transition restores " + chrome + " cycle " + cycle);
            var restoredHeader = (Control)Field(form, "header"); var restoredNotice = (Control)Field(form, "noticeBlock"); var restoredBody = (Control)Field(form, "workspaceBody");
            Check(restoredHeader.Width == form.ClientSize.Width - form.Padding.Horizontal, "Restored header fills current window width");
            Check(restoredBody.Top >= restoredNotice.Bottom, "Restored cards start below dashboard chrome");
            var restoredCommands = (Panel)Field(form, "commands");
            var restoredFrame = (Panel)Field(form, "monitoringFrame");
            Check(restoredFrame.ClientRectangle.Contains(restoredCommands.Bounds), "Restored monitoring table fits inside its frame");
            Check(restoredCommands.Controls.Cast<Control>().All(control => restoredCommands.ClientRectangle.Contains(control.Bounds)), "Restored monitoring commands are fully inside their row");
            var center = (Panel)Field(form, "workspaceCenter"); var side = (Panel)Field(form, "picker");
            Check(center.Right <= side.Left, "Restored picker does not overlap cards");
            main.WindowState = FormWindowState.Normal; main.Size = new Size(1500, 940); Application.DoEvents();
        }
        ((TextBox)Field(form, "search")).Text = "no-object-matches-this"; Capture(main, Path.Combine(folder, "04-picker-no-results.png"));
        ((TextBox)Field(form, "search")).Clear();
        ((Panel)Field(form, "picker")).Visible = false;
        main.WindowState = FormWindowState.Minimized; Application.DoEvents(); main.WindowState = FormWindowState.Maximized; Application.DoEvents();
        Check(((Control)Field(form, "header")).Visible && ((Control)Field(form, "workspaceBody")).Top >= ((Control)Field(form, "noticeBlock")).Bottom, "Restore without picker retains header and card separation");
        Capture(main, Path.Combine(folder, "15-restored-without-picker.png"));
        main.WindowState = FormWindowState.Normal; main.Size = new Size(1500, 940); Application.DoEvents();
        Call(form, "SetEditLayout", true);
        Capture(main, Path.Combine(folder, "05-edit-layout.png"));
        using (var nameDialog = new DashboardNameDialog(form.Font, "Dashboard name", "Supply quality overview"))
        {
            nameDialog.Show(main); Application.DoEvents(); Capture(nameDialog, Path.Combine(folder, "09-name-dialog.png"));
            Check(nameDialog.AcceptButton != null && nameDialog.CancelButton != null, "Name dialog supports Enter and Escape actions");
            Descendants(nameDialog).OfType<TextBox>().Single().Text = " ";
            Check(!((Button)nameDialog.AcceptButton).Enabled, "Name dialog blocks blank names");
            nameDialog.Close();
        }
        using (var relink = new DashboardRelinkDialog(form.Font, new[] { a.Value.Name + " [saved ID]", b.Value.Name + " [saved ID]" }, new object[] { a.Value.Name, b.Value.Name }))
        {
            relink.Show(main); Application.DoEvents(); Capture(relink, Path.Combine(folder, "10-relink-dialog.png"));
            Check(relink.From.SelectedIndex == 0 && relink.To.SelectedIndex == 0 && relink.CancelButton != null, "Relink dialog has usable selections and Cancel");
            relink.Close();
        }
        using (var relink = new DashboardRelinkDialog(form.Font, Array.Empty<string>(), Array.Empty<object>()))
            Check(!((Button)relink.AcceptButton).Enabled, "Relink dialog blocks missing source/target selections");
        main.Size = new Size(900, 650); Application.DoEvents(); Call(form, "OpenPicker", (object)null);
        Capture(main, Path.Combine(folder, "06-picker-small-window.png"));
        Check(((ListView)Field(form, "objectPicker")).Height >= form.DeviceDpi * 60 / 96, "Small picker retains a usable object list");
        Check(!((Panel)Field(form, "workspaceCenter")).Visible && ((Panel)Field(form, "picker")).Dock == DockStyle.Fill, "Small-window picker uses the workspace without squeezing monitoring controls");
        Check(((TextBox)Field(form, "search")).AccessibleName.Length > 0 && ((ComboBox)Field(form, "attribute")).AccessibleName.Length > 0, "Picker fields expose accessible names");
        var picker = (Panel)Field(form, "picker"); ((Panel)Field(form, "pickerViewport")).AutoScrollPosition = new Point(0, 10000);
        Capture(main, Path.Combine(folder, "07-picker-small-footer.png"));
        picker.Visible = false; Application.DoEvents(); Capture(main, Path.Combine(folder, "08-dashboard-small-window.png"));
        Check(((Panel)Field(form, "workspaceCenter")).Visible, "Closing picker restores dashboard content");
        var monitoringCommands = (Panel)Field(form, "commands");
        Check(monitoringCommands.Controls.Cast<Control>().All(c => c.Visible && monitoringCommands.ClientRectangle.Contains(c.Bounds)), "Monitoring controls remain visible after closing the small-window picker");
        var table = Descendants(form).OfType<DataGridView>().Single();
        Check(table.Columns.Cast<DataGridViewColumn>().All(c => c.MinimumWidth >= 65), "Table columns retain readable minimum widths");
        var editCards = ((Panel)Field(form, "widgets")).Controls.Cast<Control>().Where(c => c is Panel).ToArray();
        Check(editCards.All(c => c.TabStop), "Edit mode permits keyboard card focus");
        Check(((Button)Field(form, "editLayoutButton")).Text == "Done", "Edit mode exposes a clear Done action");
        var firstDescriptor = ((IEnumerable)Call(form, "OrderedCards")).Cast<object>().First();
        var moveKey = new KeyEventArgs(Keys.Control | Keys.Right);
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(editCards[0], new object[] { moveKey });
        Check(moveKey.Handled && ((IEnumerable)Call(form, "OrderedCards")).Cast<object>().ElementAt(1).Equals(firstDescriptor), "Keyboard reorders a focused card without adding buttons");
        var movedCard = ((IEnumerable)Field(form, "layoutCards")).Cast<Control>().ElementAt(1);
        var resizeKey = new KeyEventArgs(Keys.Control | Keys.Shift | Keys.Right);
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movedCard, new object[] { resizeKey });
        Check(resizeKey.Handled && voltage.Size != null, "Keyboard provides a resize alternative to the corner grip");

        foreach (var input in new Control[] { (ComboBox)Field(form, "meterPicker"), (TextBox)Field(form, "search"), (ListView)Field(form, "objectPicker") })
            File.AppendAllText(Path.Combine(folder, "font-observations.txt"), input.GetType().Name + "=" + input.Font.SizeInPoints + "; dashboard=" + form.Font.SizeInPoints + "; DPI=" + form.DeviceDpi + Environment.NewLine);
        typeof(DashboardForm).GetField("dirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, false);
        Call(form, "OpenPicker", (object)null);
        foreach (var windowSize in new[] { new Size(900, 650), new Size(1500, 940), new Size(1100, 900) })
        {
            main.Size = windowSize; Application.DoEvents();
            var viewport = (Panel)Field(form, "pickerViewport");
            Check(!viewport.AutoScroll && !viewport.VerticalScroll.Visible, "Picker frame has no outer scrollbar at " + windowSize.Width);
            Check(((ListView)Field(form, "objectPicker")).Height >= form.DeviceDpi * 60 / 96, "Object list remains usable at " + windowSize.Width);
            foreach (string field in new[] { "meterPicker", "search", "attribute", "display", "widgetTitle", "objectPicker" })
            {
                var editor = (Control)Field(form, field); var control = editor.Parent is DashboardFieldHost ? editor.Parent : editor;
                var bounds = new Rectangle(viewport.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
                Check(viewport.ClientRectangle.Contains(bounds) && control.Height > 0, "Picker field remains visible without scrolling: " + field + " at " + windowSize.Width);
            }
            var addButton = (Button)Field(form, "pickerAdd");
            ((Panel)Field(form, "pickerViewport")).AutoScrollPosition = new Point(0, 10000); Application.DoEvents();
            var centerPoint = new Point(addButton.Left + addButton.Width / 2, addButton.Top + addButton.Height / 2);
            Check(addButton.Visible && addButton.Parent.ClientRectangle.Contains(addButton.Bounds) && ReferenceEquals(addButton.Parent.GetChildAtPoint(centerPoint), addButton), "Add command remains inside its footer and unobscured at " + windowSize.Width);
            var closeButton = Descendants((Panel)Field(form, "picker")).OfType<Button>().Single(button => button.Text == "Close");
            Check(closeButton.Parent.ClientRectangle.Contains(closeButton.Bounds), "Close command remains inside its header at " + windowSize.Width);
        }
        main.Size = new Size(1500, 940); Application.DoEvents();
        var list = (ListView)Field(form, "objectPicker");
        list.Items.Cast<ListViewItem>().First(item => item.SubItems[2].Text == "Ready").Checked = true;
        var addCommand = (Button)Field(form, "pickerAdd");
        ((Panel)Field(form, "pickerViewport")).AutoScrollPosition = new Point(0, 10000); Capture(main, Path.Combine(folder, "11-add-command-selected.png"));
        int widgetCount = definition.Widgets.Count; addCommand.PerformClick();
        Check(definition.Widgets.Count == widgetCount + 1, "Visible Add to dashboard command adds the checked measurement");
        typeof(DashboardForm).GetField("dirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, false);
        Check(a.Value.Objects.Count == 78, "UI review uses realistic 78-object meter without modifying user data");
        ReviewMixedMonitors(main, form, definition, folder);
        ReviewDashboardAutosave(folder);
    }
}

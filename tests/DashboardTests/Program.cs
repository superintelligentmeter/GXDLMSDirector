// Intelmeter contributors, 2026-10-17. Offline dashboard regression tests. GPL-2.0-only.
using GXDLMSDirector;
using Gurux.DLMS;
using Gurux.DLMS.Objects;
using Gurux.DLMS.Enums;
using System.Reflection;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;

[System.Runtime.Versioning.SupportedOSPlatform("windows7.0")]
internal static partial class Program
{
    static int assertions;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); assertions++; Console.WriteLine("OK: " + message); }
    static void Invalid(Action action, string message) { try { action(); } catch (InvalidDataException) { assertions++; return; } throw new Exception(message); }
    static DashboardBinding Binding(GXDLMSObject o, int index = 2) => new() { MeterId = (o.Parent?.Tag as GXDLMSDevice)?.DashboardId ?? "", MeterName = (o.Parent?.Tag as GXDLMSDevice)?.Name ?? "", ClassId = (int)o.ObjectType, Obis = o.LogicalName, Attribute = index, Label = o.Description ?? "Measurement" };
    static object Field(object o, string name) => (o is MainForm ? typeof(MainForm) : o.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    static object Call(object o, string name, params object[] args) => (o is MainForm ? typeof(MainForm) : o.GetType()).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    [STAThread]
    static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Gurux.DLMS.UI.ApplicationTypography.Install();
            var directory = args.Length == 0 ? Path.Combine(Path.GetTempPath(), "DashboardTests-" + Guid.NewGuid().ToString("N")) : Path.GetFullPath(args[0]);
            Directory.CreateDirectory(directory);
            if (args.Length > 1 && args[1] == "--readme-screenshot") { ShowReadmeScreenshot(directory); return 0; }
            if (args.Length > 1 && (args[1] == "--restart-write" || args[1] == "--restart-read"))
            {
                ReviewDashboardRestart(directory, args[1] == "--restart-write");
                Console.WriteLine("PASS: " + assertions + " restart-phase assertions; " + args[1]); return 0;
            }
            if (args.Length > 1 && args[1] == "--ui-only") { ReviewDashboardScreens(directory); Console.WriteLine("PASS: dashboard UI screen review"); return 0; }
            Check(DashboardForm.KeepMenuOnScreen(new Rectangle(1850, 300, 240, 220), new Rectangle(0, 0, 1920, 1040)) == new Point(1680, 300), "More menu stays within right edge of owner monitor");
            Check(DashboardForm.KeepMenuOnScreen(new Rectangle(-50, 950, 240, 220), new Rectangle(-1920, 0, 1920, 1040)) == new Point(-240, 820), "More menu handles negative monitor coordinates and bottom edge");
            Check(DashboardForm.KeepMenuOnScreen(new Rectangle(100, 200, 240, 220), new Rectangle(0, 0, 1920, 1040)) == new Point(100, 200), "Menu position remains unchanged when it fits");
            Check(!DashboardFeature.ResolveEnabled("false", true) && DashboardFeature.ResolveEnabled("true", false), "Settings choice overrides startup default in both directions");
            Check(!DashboardFeature.ResolveEnabled("", false) && DashboardFeature.ResolveEnabled("", true), "Unset preference follows deployment default");
            bool? appliedSetting = null;
            using (var settingsPage = new DashboardSettingsPage(() => true, value => appliedSetting = value))
            {
                settingsPage.Initialize();
                var settingCheckbox = (CheckBox)Field(settingsPage, "enabled");
                Check(settingCheckbox.Checked && !settingsPage.Dirty, "Dashboard Settings initializes without pending changes");
                settingCheckbox.Checked = false;
                Check(settingsPage.Dirty && appliedSetting == null, "Changing Settings waits for OK before persisting");
                settingsPage.Apply();
                Check(appliedSetting == false && !settingsPage.Dirty, "Applying Settings persists dashboard preference");
                settingsPage.Size = new Size(480, 300); settingsPage.Show(); Application.DoEvents();
                using var settingsShot = new Bitmap(settingsPage.Width, settingsPage.Height); settingsPage.DrawToBitmap(settingsShot, new Rectangle(Point.Empty, settingsShot.Size)); settingsShot.Save(Path.Combine(directory, "dashboard-settings.png"));
            }
            int runningWorkers = 0, peakWorkers = 0;
            var manyMeters = Enumerable.Range(0, 16).Select(i => new DashboardBinding { MeterId = "bounded-" + i, ClassId = 3, Obis = "1.0.1.8.0.255", Attribute = 2 }).ToArray();
            var boundedReadings = DashboardReader.ReadMeters(manyMeters, (_, batch) =>
            {
                int active = System.Threading.Interlocked.Increment(ref runningWorkers);
                int oldPeak; do { oldPeak = peakWorkers; if (oldPeak >= active) break; } while (System.Threading.Interlocked.CompareExchange(ref peakWorkers, active, oldPeak) != oldPeak);
                try { System.Threading.Thread.Sleep(25); return batch.Select(b => new DashboardReading(b.Key, "1", "", 1, null, DateTime.UtcNow)).ToList(); }
                finally { System.Threading.Interlocked.Decrement(ref runningWorkers); }
            }, () => false).GetAwaiter().GetResult();
            Check(boundedReadings.Count == 16 && peakWorkers <= 4, "All meters read with bounded transport concurrency");
            var reg = new GXDLMSRegister("1.0.32.7.0.255") { Description = "L1 voltage" };
            var text = new GXDLMSData("0.0.96.1.0.255") { Description = "Meter identification", Value = "Meter A" };
            var b = Binding(reg);
            var doc = new DashboardDocument { Dashboards = new() { new DashboardDefinition { Widgets = new() { new DashboardWidget { Bindings = new() { b } } } } } };
            var file = Path.Combine(directory, "roundtrip.json");
            DashboardStore.Save(file, doc); var loaded = DashboardStore.Load(file);
            Check(loaded.Dashboards[0].Widgets[0].Bindings[0].Key == b.Key, "Binding roundtrip");
            Check(!File.ReadAllText(file).Contains("Password") && !File.ReadAllText(file).Contains("\"Key\""), "No credentials or computed keys");
            doc.Dashboards[0].Name = "Updated"; DashboardStore.Save(file, doc);
            Check(DashboardStore.Load(file).Dashboards[0].Name == "Updated", "Atomic replacement");
            var validBytes = File.ReadAllText(file); doc.Version = 3;
            Invalid(() => DashboardStore.Save(file, doc), "Reject unsupported version"); Check(File.ReadAllText(file) == validBytes, "Failed save preserves file"); doc.Version = 2;
            doc.Dashboards[0].RefreshSeconds = 0; Invalid(() => DashboardStore.Validate(doc), "Reject zero refresh"); doc.Dashboards[0].RefreshSeconds = 5;
            b.Obis = "1.0.999.7.0.255"; Invalid(() => DashboardStore.Validate(doc), "Reject malformed OBIS"); b.Obis = reg.LogicalName;
            doc.Dashboards[0].Widgets[0].Bindings.Add(b); Invalid(() => DashboardStore.Validate(doc), "Reject duplicate series"); doc.Dashboards[0].Widgets[0].Bindings.RemoveAt(1);
            var huge = Path.Combine(directory, "oversize.json"); File.WriteAllText(huge, new string(' ', 2 * 1024 * 1024 + 1)); Invalid(() => DashboardStore.Load(huge), "Bound import size"); File.Delete(huge);
            var calls = new List<int>(); var client = new GXDLMSClient();
            var readings = DashboardReader.Read(new[] { b, b }, _ => reg, (_, _) => true, (o, i) => { calls.Add(i); if (i == 3) { reg.Scaler = 0.1; reg.Unit = Unit.Voltage; } else client.UpdateValue(o, i, 2304); }, () => false);
            Check(calls.SequenceEqual(new[] { 3, 2 }), "Deduplication and scaler before value");
            Check(Math.Abs(readings.Single().Number.Value - 230.4) < .00001, "Scaling applied exactly once by unchanged DLMS package");
            Check(readings[0].Unit == GXDLMSConverter.GetUnit(Unit.Voltage), "Unit metadata");
            calls.Clear();
            DashboardReader.Read(new[] { Binding(reg, 3), b }, _ => reg, (_, _) => true, (_, i) => calls.Add(i), () => false);
            Check(calls.SequenceEqual(new[] { 3, 2 }), "Explicit scaler binding also deduplicated");
            readings = DashboardReader.Read(new[] { b }, _ => null, (_, _) => true, (_, _) => throw new Exception("Unexpected I/O"), () => false);
            Check(readings[0].Error.Contains("missing"), "Missing imported object");
            calls.Clear(); readings = DashboardReader.Read(new[] { b }, _ => reg, (_, _) => false, (_, i) => calls.Add(i), () => false);
            Check(readings[0].Error == "Access denied" && calls.Count == 0, "Denied reads do no I/O");
            readings = DashboardReader.Read(new[] { b, Binding(text) }, x => x.Obis == reg.LogicalName ? reg : text, (_, _) => true, (o, _) => { if (o == reg) throw new IOException("Timeout"); }, () => false);
            Check(readings.Count == 2 && readings[0].Error == "Timeout" && readings[1].Text == "Meter A" && readings[1].Number == null, "Per-attribute failure and nonnumeric values");
            var profile = new GXDLMSProfileGeneric("1.0.99.1.0.255");
            readings = DashboardReader.Read(new[] { Binding(profile) }, _ => profile, (_, _) => true, (_, _) => throw new Exception("Unexpected I/O"), () => false);
            Check(readings[0].Error.Contains("ranged"), "No unbounded profile buffer reads");
            calls.Clear(); readings = DashboardReader.Read(new[] { b }, _ => reg, (_, _) => true, (_, i) => calls.Add(i), () => true);
            Check(readings.Count == 0 && calls.Count == 0, "Cancellation before read");
            int count = 0; readings = DashboardReader.Read(new[] { b }, _ => reg, (_, _) => true, (_, _) => count++, () => count > 0);
            Check(count == 1 && readings.Count == 0, "Cancellation between metadata and value");
            text.Value = new object[] { 1, true, new byte[] { 0xAB, 0xCD } };
            readings = DashboardReader.Read(new[] { Binding(text) }, _ => text, (_, _) => true, (_, _) => { }, () => false);
            Check(readings[0].Text.Contains("ABCD"), "Structured value formatting");
            using var device = new DisposableDevice();
            device.Value.Name = "Meter A"; device.Value.Objects.Add(reg); device.Value.Objects.Add(text);
            b.MeterId = device.Value.DashboardId; b.MeterName = device.Value.Name;
            using var form = new DashboardForm(device.Value, storagePath: Path.Combine(directory, "single-fixture.json"));
            form.Show(); Application.DoEvents();
            var fixture = (DashboardDocument)Field(form, "document"); fixture.Dashboards.Clear();
            var d = new DashboardDefinition { Name = "Electrical overview (offline fixture)", Widgets = new() {
                new DashboardWidget { Title = "Instantaneous values", Kind = "Values", Bindings = new() { b, Binding(text) } },
                new DashboardWidget { Title = "Voltage trend", Kind = "Trend", Bindings = new() { b } },
                new DashboardWidget { Title = "Selected measurements", Kind = "Table", Bindings = new() { b, Binding(text) } }
            }};
            fixture.Dashboards.Add(d); Call(form, "BindDashboards", d);
            var sample = Call(form, "State", b.Key); var st = sample.GetType();
            st.GetField("Text").SetValue(sample, "230.4"); st.GetField("Unit").SetValue(sample, "Voltage"); st.GetField("Success").SetValue(sample, DateTime.UtcNow); st.GetField("Numeric").SetValue(sample, true);
            var points = (IList)st.GetField("Points").GetValue(sample); for (int i = 0; i < 60; i++) points.Add((DateTime.UtcNow.AddSeconds(i - 60), 230.0 + Math.Sin(i / 4.0)));
            var successTime = DateTime.UtcNow.AddSeconds(-3);
            Call(form, "ApplyReadings", new List<DashboardReading> { new(b.Key, "230.4", "Voltage", 230.4, null, successTime) });
            Call(form, "ApplyReadings", new List<DashboardReading> { new(b.Key, null, null, null, "Timed out", DateTime.UtcNow) });
            Check((string)st.GetField("Text").GetValue(sample) == "230.4" && (DateTime?)st.GetField("Success").GetValue(sample) == successTime, "Failure retains last successful value and timestamp");
            Check(double.IsNaN(((List<(DateTime Time, double Value)>)points).Last().Value), "Failure inserts chart gap");
            // A later success recovers the error without losing bounded live history.
            Call(form, "ApplyReadings", new List<DashboardReading> { new(b.Key, "230.5", "Voltage", 230.5, null, DateTime.UtcNow) });
            Check(st.GetField("Error").GetValue(sample) == null, "Read recovery clears error");
            for (int i = 0; i < 1010; i++) Call(form, "ApplyReadings", new List<DashboardReading> { new(b.Key, "230.5", "Voltage", 230.5, null, DateTime.UtcNow.AddMilliseconds(i)) });
            Check(points.Count == 1000, "Bounded sample retention");
            Call(form, "RefreshViews"); Application.DoEvents();
            foreach (var size in new[] { new Size(1200, 900), new Size(800, 600) })
            {
                form.Size = size; Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "dashboard-" + size.Width + ".png"));
                Check(!((Panel)Field(form, "widgets")).HorizontalScroll.Visible, "No horizontal workspace overflow at " + size.Width);
                Check(((Panel)Field(form, "widgets")).Controls.Count == 4, "All widget types render at " + size.Width);
            }
            var current = new GXDLMSRegister("1.0.31.7.0.255") { Description = "L1 current" }; device.Value.Objects.Add(current);
            d.Widgets[1].Bindings.Add(Binding(current)); Call(form, "Render");
            Call(form, "ApplyReadings", new List<DashboardReading> { new(Binding(current).Key, "12.5", "A", 12.5, null, DateTime.UtcNow) });
            Call(form, "RefreshViews");
            var charts = ((Panel)Field(form, "widgets")).Controls.Cast<Control>().SelectMany(x => x.Controls.Cast<Control>()).OfType<System.Windows.Forms.DataVisualization.Charting.Chart>().ToArray();
            Check(charts.Single().ChartAreas.Count == 2, "Mixed units have separate axes");
            Call(form, "ApplyReadings", new List<DashboardReading> { new(Binding(current).Key, "12.5", "Voltage", 12.5, null, DateTime.UtcNow) });
            Call(form, "RefreshViews");
            Check(charts.Single().ChartAreas.Count == 1, "Unit transition removes unused axis without failure");
            var monitor = (CheckBox)Field(form, "monitor"); monitor.Checked = true;
            var poll = (System.Threading.Tasks.Task)Call(form, "Poll"); while (!poll.IsCompleted) Application.DoEvents(); poll.GetAwaiter().GetResult();
            Check(!monitor.Checked, "Disconnected polling pauses");
            Check(((Label)Field(form, "status")).Text.Contains("Disconnected"), "Disconnected status visible");
            form.Close();
            AppContext.SetSwitch(DashboardFeature.SwitchName, false);
            using (var disabled = new OfflineMainForm())
            {
                disabled.Show(); Application.DoEvents();
                Check(Field(disabled, "dashboardWorkspace") == null, "Startup switch leaves original object workspace intact");
                Check(!((TreeView)Field(disabled, "ObjectTree")).Nodes.Cast<TreeNode>().Any(n => n.Text == "Dashboards"), "Startup switch removes dashboard navigation");
                Call(disabled, "RestoreDashboardNavigation");
                Check((bool)Call(disabled, "PrepareDashboardsForClose"), "Disabled feature has no read or save lifecycle");
            }
            AppContext.SetSwitch(DashboardFeature.SwitchName, true);
            using var main = new OfflineMainForm();
            main.DashboardStoragePath = Path.Combine(directory, "main-fixture.json");
            main.Show(); Application.DoEvents();
            using (var appSettings = new GXSettingsDlg(new Gurux.Net.GXNet(), (GXDLMSTranslator)Field(main, "eventsTranslator")))
            {
                var settingsTabs = appSettings.Controls.OfType<TabControl>().Single();
                var dashboardsSettingsTab = settingsTabs.TabPages.Cast<TabPage>().SingleOrDefault(p => p.Text == "Dashboards");
                Check(dashboardsSettingsTab != null, "Original Settings automatically discovers the optional Dashboards page");
                settingsTabs.SelectedTab = dashboardsSettingsTab;
                appSettings.Show(main); Application.DoEvents();
                using var settingsDialogShot = new Bitmap(appSettings.Width, appSettings.Height); appSettings.DrawToBitmap(settingsDialogShot, new Rectangle(Point.Empty, settingsDialogShot.Size)); settingsDialogShot.Save(Path.Combine(directory, "settings-dialog.png"));
                appSettings.Close();
            }

            var devices = (IList)Field(main, "Devices"); devices.Add(device.Value);
            var tree = (TreeView)Field(main, "ObjectTree");
            tree.Nodes[0].Nodes.Add(new TreeNode("Test meter") { Tag = device.Value });
            var workspace = (Panel)Field(main, "dashboardWorkspace");
            Check(!workspace.Controls.OfType<TabControl>().Any(), "Workspace has no Objects/Dashboards tab bar");
            Call(main, "ShowDashboardScreen", true);
            Call(main, "DisplayDashboard", device.Value, null);
            var embedded = (DashboardForm)Field(main, "activeDashboard");
            Call(embedded, "SetWorkspaceActive", false);
            Check(!((System.Windows.Forms.Timer)Field(embedded, "timer")).Enabled, "Hidden dashboards stop their refresh timer");
            Call(embedded, "SetWorkspaceActive", true);
            Check(((System.Windows.Forms.Timer)Field(embedded, "timer")).Enabled, "Returning to dashboard restarts refresh timer");
            Check(DashboardAccessPolicy.AttributeError(reg, 2, (_, i) => i != 3)?.Contains("Scaler/unit") == true, "Picker and reader share scaler-access eligibility");
            var catalog = new DashboardMeterCatalog(new[] { device.Value });
            Check(ReferenceEquals(catalog.Resolve(b), reg), "Lookup index resolves the original meter object instance");

            device.Value.Manufacturer = "TST";
            device.Value.Manufacturers = new Gurux.DLMS.ManufacturerSettings.GXManufacturerCollection();
            device.Value.Manufacturers.Add(new Gurux.DLMS.ManufacturerSettings.GXManufacturer { Identification = "TST", Name = "Offline test manufacturer" });
            Check(!((Control)Field(main, "objectWorkspacePage")).Visible, "Objects page hidden during meter-selection regression");
            Call(main, "SelectItem", device.Value);
            Check(((GroupBox)Field(main, "DeviceGb")).Text == device.Value.Name, "Meter selection routes correctly while Objects page hidden");
            Call(main, "SelectItem", Field(main, "Devices"));
            Check(!((ToolStripItem)Field(main, "DeleteMnu")).Enabled, "Device collection selection routes correctly while hidden");
            Call(main, "SelectItem", device.Value.Objects);
            Check(((ListView)Field(main, "ObjectValueView")).Items.Count == device.Value.Objects.Count, "Object collection selection routes correctly while hidden");

            Check(!embedded.TopLevel && embedded.TopLevelControl == main, "Dashboard embedded in main application");
            Check(ReferenceEquals(embedded.DialogOwner, main), "Dashboard native dialogs use the top-level DPI-aware owner");
            Check((bool)Call(main, "BeginDashboardRead", device.Value), "Dashboard acquires main read gate");
            Check(!((Control)Field(main, "toolStrip1")).Enabled, "Meter toolbar disabled during dashboard read");
            Check(!(bool)Call(main, "BeginDashboardRead", device.Value), "Overlapping read rejected");
            Check(!(bool)Call(main, "PrepareDashboardsForClose"), "Close blocked while read active");
            Call(main, "EndDashboardRead");
            Check(((Control)Field(main, "toolStrip1")).Enabled, "Meter toolbar restored after read");
            ((CheckBox)Field(embedded, "monitor")).Checked = true;
            Call(main, "ShowDashboardScreen", false);
            Check(!((CheckBox)Field(embedded, "monitor")).Checked, "Switching to Objects pauses monitoring");
            Call(main, "ShowDashboardScreen", true);
            Call(main, "DisplayDashboard", device.Value, null);
            Check(ReferenceEquals(embedded, Field(main, "activeDashboard")), "Dashboard state retained across screen switches");
            var energy = new GXDLMSRegister("1.0.1.8.0.255") { Description = "Import energy" };
            var power = new GXDLMSRegister("1.0.1.7.0.255") { Description = "Total active power" };
            var factor = new GXDLMSRegister("1.0.13.7.0.255") { Description = "Power factor" };
            device.Value.Objects.Add(energy); device.Value.Objects.Add(power); device.Value.Objects.Add(factor);
            var visual = new DashboardDefinition { Name = "Electrical overview", Widgets = new() {
                new DashboardWidget { Kind = "Values", Bindings = new() { b, Binding(current), Binding(power) } },
                new DashboardWidget { Kind = "Trend", Title = "Phase voltages", Bindings = new() { b } },
                new DashboardWidget { Kind = "Values", Bindings = new() { Binding(energy), Binding(factor) } },
                new DashboardWidget { Kind = "Table", Title = "Selected measurements", Bindings = new() { b, Binding(current), Binding(power), Binding(factor) } }
            }};
            var embeddedDoc = (DashboardDocument)Field(embedded, "document");
            embeddedDoc.Dashboards.Clear(); embeddedDoc.Dashboards.Add(visual); Call(embedded, "BindDashboards", visual);
            for (int n = 0; n < 60; n++) Call(embedded, "ApplyReadings", new List<DashboardReading> { new(b.Key, "230.4", "V", 230.4 + Math.Sin(n / 4.0), null, DateTime.UtcNow.AddSeconds(-300 + n * 5)) });
            Call(embedded, "ApplyReadings", new List<DashboardReading> {
                new(Binding(current).Key, "12.8", "A", 12.8, null, DateTime.UtcNow),
                new(Binding(power).Key, "8.76", "kW", 8.76, null, DateTime.UtcNow),
                new(Binding(energy).Key, "12,458.72", "kWh", 12458.72, null, DateTime.UtcNow),
                new(Binding(factor).Key, null, null, null, "Read failed", DateTime.UtcNow) });
            Call(embedded, "RefreshViews"); Call(main, "SyncDashboardTree", device.Value, embedded);
            Check(tree.Nodes.Cast<TreeNode>().Any(n => n.Text == "Dashboards"), "Dashboards is a sibling of Devices");
            tree.Nodes[0].ExpandAll();
            Call(embedded, "OpenPicker", reg);
            Check(((Panel)Field(embedded, "picker")).Visible, "OBIS picker embedded beside dashboard");
            Check(((ComboBox)Field(embedded, "attribute")).SelectedItem != null, "Picker exposes common readable attribute");
            var pickerControls = (Panel)Field(embedded, "picker");
            Check(((ListView)Field(embedded, "objectPicker")).Columns.Cast<ColumnHeader>().Sum(c => c.Width) <= ((ListView)Field(embedded, "objectPicker")).ClientSize.Width, "OBIS columns fit without horizontal overflow");
            Check(((Button)Field(embedded, "pickerAdd")).Enabled, "Valid initial selection enables Add to dashboard");

            using var second = new DisposableDevice(); second.Value.Name = "Meter B";
            var regB = new GXDLMSRegister(reg.LogicalName) { Description = "L1 voltage" }; second.Value.Objects.Add(regB); devices.Add(second.Value);
            tree.Nodes[0].Nodes.Add(new TreeNode("Meter B") { Tag = second.Value });
            var bindingB = Binding(regB);
            Check(bindingB.Key != b.Key, "Identical OBIS attributes on two meters have different sample keys");
            visual.Widgets[1].Bindings.Add(bindingB); visual.Widgets[3].Bindings.Add(bindingB);
            Call(embedded, "RefreshMeters"); Call(embedded, "OpenPicker", reg);
            var meterPicker = (ComboBox)Field(embedded, "meterPicker");
            Check(meterPicker.Items.Count == 2, "Picker lists both loaded meters");
            var selectedObjects = (HashSet<GXDLMSObject>)Field(embedded, "selectedObjects");
            meterPicker.SelectedIndex = 1;
            var pickerList = (ListView)Field(embedded, "objectPicker"); pickerList.Items[0].Checked = true;
            Check(selectedObjects.Contains(reg) && selectedObjects.Contains(regB), "Picker keeps selections across meter switches");
            var deniedObject = new GXDLMSRegister("1.0.99.1.0.255") { Description = "Restricted register" };
            for (int ai = 1; ai <= ((IGXDLMSBase)deniedObject).GetNames().Length; ai++) deniedObject.SetAccess(ai, Gurux.DLMS.Enums.AccessMode.NoAccess);
            second.Value.Objects.Add(deniedObject);
            Call(embedded, "FillPicker");
            Check(pickerList.Items.Count == second.Value.Objects.Count, "Picker includes every selected-meter object including unreadable ones");
            var deniedRow = pickerList.Items.Cast<ListViewItem>().Single(x => ReferenceEquals(x.Tag, deniedObject));
            Check(deniedRow.ToolTipText.Contains("No readable attributes"), "Unreadable object explains its access restriction");
            deniedRow.Checked = true;
            Check(!deniedRow.Checked && !selectedObjects.Contains(deniedObject), "Unreadable row cannot be checked");
            Check(meterPicker.AccessibleName == "Meter", "Meter selector has an explicit accessible label");
            ((TextBox)Field(embedded, "search")).Text = "no-match";
            Check(pickerList.Items.Count == 0, "Search filters displayed objects");
            meterPicker.SelectedIndex = 0;
            Check(((TextBox)Field(embedded, "search")).Text == "" && pickerList.Items.Count == device.Value.Objects.Count, "Switching meter clears stale search and shows all its objects");
            meterPicker.SelectedIndex = 1;
            Call(embedded, "UpdateAttributes");
            Check(((ComboBox)Field(embedded, "attribute")).SelectedItem != null, "Cross-meter common attributes available");
            int beforeAdd = visual.Widgets.Count; Call(embedded, "AddPicked");
            Check(visual.Widgets.Last().Bindings.Select(x => x.MeterId).Distinct().Count() == 2, "Widget can contain both meters");
            visual.Widgets.RemoveAt(beforeAdd); Call(embedded, "Render");
            // Simulate independent devices without opening either transport.
            using var started = new System.Threading.ManualResetEventSlim();
            var multi = DashboardReader.ReadMeters(new[] { b, b, bindingB }, (id, batch) => {
                if (id == b.MeterId) { if (!started.Wait(3000)) throw new Exception("Other meter blocked"); throw new IOException("Meter A timeout"); }
                started.Set(); return batch.Select(x => new DashboardReading(x.Key, "241.2", "V", 241.2, null, DateTime.UtcNow)).ToList();
            }, () => false).GetAwaiter().GetResult();
            Check(multi.Count == 2 && multi.Single(x => x.Key == bindingB.Key).Error == null, "Meter timeout isolated and other meter read independently");
            Call(embedded, "ApplyReadings", multi);
            Check((string)Call(embedded, "State", b.Key).GetType().GetField("Text").GetValue(Call(embedded, "State", b.Key)) == "230.4", "Failed meter retains its own last value");
            Check((string)Call(embedded, "State", bindingB.Key).GetType().GetField("Text").GetValue(Call(embedded, "State", bindingB.Key)) == "241.2", "Second meter updates separately");
            var originalId = second.Value.DashboardId; second.Value.Name = "Renamed meter B";
            Check(ReferenceEquals(Call(embedded, "Resolve", bindingB), regB), "Meter rename preserves binding");
            devices.Remove(second.Value);
            Check(((string)Call(embedded, "Quality", bindingB)).Contains("Meter missing"), "Removed meter explicitly marked missing");
            devices.Add(second.Value);
            Check(ReferenceEquals(Call(embedded, "Resolve", bindingB), regB), "Restored meter resolves by stable ID");
            Call(main, "SyncDashboardTree", null, embedded);
            var dashboardRoot = tree.Nodes.Cast<TreeNode>().Single(n => n.Text == "Dashboards");
            Check(dashboardRoot.ImageKey == "dashboards" && dashboardRoot.SelectedImageKey == "dashboards", "Dashboard root uses dedicated icon");
            Check(dashboardRoot.Nodes[0].ImageKey == "dashboard" && dashboardRoot.Nodes[dashboardRoot.Nodes.Count - 1].ImageKey == "dashboard-new", "Saved and new dashboard icons are distinct");
            foreach (string icon in new[] { "dashboards", "dashboard", "dashboard-new" })
                foreach (int size in new[] { 16, 20, 24, 32 })
                    foreach (bool dark in new[] { false, true })
                    { using var glyph = TreeIcons.Render(icon, size, dark); Check(glyph.Width == size, "Dashboard icon renders at " + size); }
            tree.SelectedNode = dashboardRoot.Nodes[0]; Application.DoEvents();
            Check((bool)Field(main, "dashboardScreenActive"), "Dashboard tree selection uses workspace without meter casts");
            Check(Call(main, "GetSelectedDevice") == null, "Dashboard node is not a meter command target");
            var savePath = Path.Combine(directory, "multi.json"); DashboardStore.Save(savePath, embeddedDoc);
            var reloaded = DashboardStore.Load(savePath);
            Check(reloaded.Dashboards[0].Widgets[1].Bindings.Any(x => x.MeterId == originalId), "Multi-meter persistence retains meter IDs");
            Call(embedded, "RefreshViews");
            main.Size = new Size(1600, 1000); Application.DoEvents();
            using (var shot = new Bitmap(main.Width, main.Height)) { main.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size)); shot.Save(Path.Combine(directory, "dashboard-embedded.png")); }
            var cards = ((Panel)Field(embedded, "widgets")).Controls.Cast<Control>().ToArray();
            Check(cards[0].Top == cards[1].Top && cards[1].Top == cards[2].Top, "Three compact tiles share top row");
            Check(cards[4].Top >= cards[3].Bottom, "Trend spans the card region as in the latest reference");
            var originalCards = ((IEnumerable)Call(embedded, "OrderedCards")).Cast<object>().ToArray();
            var originalCount = originalCards.Length;
            var originalBindings = visual.Widgets.SelectMany(w => w.Bindings).Select(x => x.Key).OrderBy(x => x).ToArray();
            var dragType = typeof(DashboardForm).GetNestedType("CardDrag", BindingFlags.NonPublic);
            var dragData = Activator.CreateInstance(dragType, embedded, originalCards[0]);
            var targetCard = ((Panel)Field(embedded, "widgets")).Controls[2];
            var dropPoint = targetCard.PointToScreen(new Point(targetCard.Width - 8, targetCard.Height / 2));
            var dragEvent = new DragEventArgs(new DataObject(dragData), 1, dropPoint.X, dropPoint.Y, DragDropEffects.Move, DragDropEffects.None);
            Call(embedded, "DragCardOver", targetCard, dragEvent);
            Check(dragEvent.Effect == DragDropEffects.Move, "Drag-over accepts local card with insertion feedback");
            Call(embedded, "DropCard", targetCard, dragEvent);
            var movedCards = ((IEnumerable)Call(embedded, "OrderedCards")).Cast<object>().ToArray();
            Check(movedCards.Length == originalCount && movedCards[2].Equals(originalCards[0]), "Drop moves one value tile rather than its group");
            Check(visual.Widgets.SelectMany(w => w.Bindings).Select(x => x.Key).OrderBy(x => x).SequenceEqual(originalBindings), "Drop preserves exact binding multiset without duplication");
            Check(!embedded.MoveCard(0, 0, true), "Dropping onto itself is a no-op");
            for (int n = 0; n < 20; n++) { embedded.MoveCard(0, originalCount - 1, true); embedded.MoveCard(originalCount - 1, 0, false); }
            Check(((Panel)Field(embedded, "widgets")).Controls.Count == originalCount, "Repeated moves never duplicate rendered cards");
            string dragPath = Path.Combine(directory, "drag-order.json"); DashboardStore.Save(dragPath, embeddedDoc);
            var savedOrder = DashboardStore.Load(dragPath).Dashboards[0].Widgets[0].TileOrder;
            Check(savedOrder.SequenceEqual(visual.Widgets[0].TileOrder) && savedOrder[0] == 2, "Individual tile positions persist through save and reload");
            var chartBefore = visual.Widgets.Single(w => w.Kind == "Trend");
            embedded.MoveCard(3, 0, false);
            Check(ReferenceEquals(((IEnumerable)Call(embedded, "OrderedCards")).Cast<object>().First().GetType().GetProperty("Widget").GetValue(((IEnumerable)Call(embedded, "OrderedCards")).Cast<object>().First()), chartBefore), "Chart can be dragged as one card");
            embedded.MoveCard(0, 3, true);
            embedded.ResizeCard(0, 380, 230);
            embedded.ResizeCard(3, 650, 390);
            var resizedBinding = visual.Widgets[0].Bindings.Single(b => b.Size != null);
            Check(resizedBinding.Size.Width == 380 && resizedBinding.Size.Height == 230, "Resizing changes only selected value tile");
            Check(chartBefore.Size.Width == 650 && chartBefore.Size.Height == 390, "Chart dimensions are independent of tiles");
            DashboardStore.Save(dragPath, embeddedDoc);
            var resizedDoc = DashboardStore.Load(dragPath);
            Check(resizedDoc.Dashboards[0].Widgets.SelectMany(w => w.Bindings).Single(b => b.Size != null).Size.Width == 380 && resizedDoc.Dashboards[0].Widgets.Single(w => w.Kind == "Trend").Size.Height == 390, "Tile and chart sizes survive save/reload");
            embedded.MoveCard(0, 2, true);
            Check(resizedBinding.Size.Width == 380, "Reordering preserves size on its binding");
            embedded.MoveCard(2, 0, false);
            foreach (int windowWidth in new[] { 850, 1400 })
            {
                main.Width = windowWidth; Application.DoEvents(); Call(embedded, "LayoutWorkspace");
                var resizedCards = ((Panel)Field(embedded, "widgets")).Controls.Cast<Control>().ToArray();
                Check(!resizedCards.Any(a => resizedCards.Any(b => a != b && a.Bounds.IntersectsWith(b.Bounds))), "Resized cards do not overlap at width " + windowWidth);
                using var shot = new Bitmap(main.Width, main.Height); main.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size)); shot.Save(Path.Combine(directory, "dashboard-resized-" + windowWidth + ".png"));
            }
            embedded.ResizeCard(0, -1, -1);
            Check(resizedBinding.Size.Width == 160 && resizedBinding.Size.Height == 150, "Resize enforces readable minimum size");
            resizedBinding.Size.Width = int.MaxValue;
            bool invalidSize = false; try { DashboardStore.Validate(embeddedDoc); } catch (InvalidDataException) { invalidSize = true; }
            Check(invalidSize, "Import validation rejects unbounded dimensions");
            resizedBinding.Size.Width = 380;
            int beforeDeleteBindings = visual.Widgets[0].Bindings.Count;
            embedded.DeleteCard(0);
            Check(visual.Widgets[0].Bindings.Count == beforeDeleteBindings - 1 && ((Panel)Field(embedded, "widgets")).Controls.Count == originalCount - 1, "Corner delete removes only the selected tile");
            string orphanKey = "deleted-binding";
            Call(embedded, "ApplyReadings", new List<DashboardReading> { new DashboardReading(orphanKey, "1", "", 1, null, DateTime.UtcNow) });
            Call(embedded, "Render");
            Check(!((IDictionary)Field(embedded, "samples")).Contains(orphanKey), "Removing bindings releases their unused sample history");
            DashboardStore.Validate(embeddedDoc);
            Check(!((Panel)Field(embedded, "widgets")).Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).OfType<Button>().Any(bu => new[] { "Up", "Down", "Remove" }.Contains(bu.Text)), "Legacy move/remove buttons eliminated");
            typeof(DashboardForm).GetField("dirty", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(embedded, false);
            Call(main, "ClearDashboardWorkspace");
            Check(embedded.IsDisposed, "Replacing meter workspace disposes dashboard");
            Check(tree.Nodes.Cast<TreeNode>().Any(n => n.Text == "Dashboards"), "Dashboard root remains available after replacing meter file");
            Call(main, "DisplayDashboard", null, null);
            var emptyWorkspace = (DashboardForm)Field(main, "activeDashboard");
            main.Dispose(); Check(emptyWorkspace.IsDisposed, "Embedded dashboard disposed with main application");
            // Migrate a v1 file in a temporary directory, preserving its original bytes.
            var migrationFolder = Path.Combine(directory, "legacy-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(migrationFolder);
            string identity = device.Value.Name + "|" + device.Value.MediaType + "|" + device.Value.MediaSettings + "|" + device.Value.ClientAddress + "|" + device.Value.LogicalAddress + "|" + device.Value.PhysicalAddress;
            string legacyPath = Path.Combine(migrationFolder, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity))) + ".json");
            var legacyBinding = Binding(reg); legacyBinding.MeterId = ""; legacyBinding.MeterName = "";
            DashboardStore.Save(legacyPath, new DashboardDocument { Version = 1, Dashboards = new() { new DashboardDefinition { Widgets = new() { new DashboardWidget { Bindings = new() { legacyBinding } } } } } });
            var legacyBytes = File.ReadAllText(legacyPath); var workspacePath = Path.Combine(migrationFolder, "workspace-v2.json");
            using (var migration = new DashboardForm(device.Value, storagePath: workspacePath))
            {
                migration.InitializeWorkspace(); var migrated = (DashboardDocument)Field(migration, "document");
                Check(migrated.Dashboards.Single().Widgets.Single().Bindings.Single().MeterId == device.Value.DashboardId, "Legacy dashboard receives stable meter identity");
                Call(migration, "Save"); migration.RefreshMeters();
                Check(migrated.Dashboards.Count == 1 && migrated.MigratedSources.Count == 1, "Legacy migration is idempotent");
                Check(File.ReadAllText(legacyPath) == legacyBytes, "Legacy dashboard source remains unchanged");
            }
            using (var reopened = new DashboardForm(device.Value, storagePath: workspacePath))
            { reopened.InitializeWorkspace(); Check(((DashboardDocument)Field(reopened, "document")).Dashboards.Count == 1, "Reload does not duplicate migrated dashboards"); }
            var overrides = new System.Xml.Serialization.XmlAttributeOverrides();
            overrides.Add(typeof(GXDLMSDevice), "ObsoleteObjects", new System.Xml.Serialization.XmlAttributes { XmlIgnore = true });
            overrides.Add(typeof(Gurux.DLMS.ManufacturerSettings.GXDLMSAttributeSettings), new System.Xml.Serialization.XmlAttributes { XmlIgnore = true });
            var types = new List<Type>(GXDLMSClient.GetObjectTypes()) { typeof(Gurux.DLMS.ManufacturerSettings.GXDLMSAttributeSettings), typeof(Gurux.DLMS.ManufacturerSettings.GXDLMSAttribute) };
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(GXDLMSDevice), overrides, types.ToArray(), null, "Gurux1");
            using var xml = new StringWriter(); serializer.Serialize(xml, second.Value);
            using var reader = new StringReader(xml.ToString()); var restored = (GXDLMSDevice)serializer.Deserialize(reader);
            Check(restored.DashboardId == second.Value.DashboardId, "Stable meter ID survives the application's XML serializer");
            restored.KeepAliveStop(); (restored.Media as IDisposable)?.Dispose();

            ReviewDashboardScreens(directory);
            Console.WriteLine("PASS: " + assertions + " assertions; offline reader, persistence, import safety, disconnected polling, and two UI sizes. Artifacts: " + directory);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    sealed class OfflineMainForm : MainForm
    {
        protected override void OnLoad(EventArgs e) { } // Avoid startup listeners and online updates in this offline fixture.
    }
    sealed class DisposableDevice : IDisposable
    {
        public GXDLMSDevice Value = new(new Gurux.Net.GXNet()) { DashboardId = Guid.NewGuid().ToString("N") };
        public void Dispose() { Value.KeepAliveStop(); ((IDisposable)Value.Media).Dispose(); }
    }
}

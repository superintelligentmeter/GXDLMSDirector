// Intelmeter contributors, 2026-10-18. Separate-process dashboard persistence verification. GPL-2.0-only.
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using System.Xml.Serialization;
using GXDLMSDirector;
using Gurux.DLMS;
using Gurux.DLMS.Objects;
internal static partial class Program
{
    private static string[] RestartOrder(DashboardForm form) => ((IEnumerable)Call(form, "OrderedCards")).Cast<object>().Select(item =>
    {
        var binding = (DashboardBinding)item.GetType().GetProperty("Binding").GetValue(item);
        var widget = (DashboardWidget)item.GetType().GetProperty("Widget").GetValue(item);
        return binding?.Key ?? widget.Kind + ":" + widget.Title;
    }).ToArray();
    private static int[][] RestartBounds(DashboardForm form) => ((IEnumerable)Field(form, "layoutCards")).Cast<Control>().Select(card => new[] { card.Left, card.Top, card.Width, card.Height }).ToArray();
    private static void AddRestartSelection(DashboardForm form, GXDLMSDevice[] meters, string obis, int displayKind, string title)
    {
        Call(form, "OpenPicker", (object)null);
        var picker = (ComboBox)Field(form, "meterPicker");
        foreach (var meter in meters)
        {
            picker.SelectedItem = picker.Items.Cast<object>().Single(choice => ReferenceEquals(choice.GetType().GetProperty("Meter").GetValue(choice), meter));
            var list = (ListView)Field(form, "objectPicker");
            list.Items.Cast<ListViewItem>().Single(item => ((GXDLMSObject)item.Tag).LogicalName == obis).Checked = true;
        }
        var attribute = (ComboBox)Field(form, "attribute");
        attribute.SelectedItem = attribute.Items.Cast<object>().Single(choice => (int)choice.GetType().GetProperty("Index").GetValue(choice) == 2);
        ((ComboBox)Field(form, "display")).SelectedIndex = displayKind; ((TextBox)Field(form, "widgetTitle")).Text = title;
        ((Button)Field(form, "pickerAdd")).PerformClick();
    }
    private static void ReviewDashboardRestart(string directory, bool write)
    {
        string workspacePath = Path.Combine(directory, "workspace.json"), meterPath = Path.Combine(directory, "meters.gxc"), expectedPath = Path.Combine(directory, "expected.json");
        using var main = new OfflineMainForm(); main.DashboardStoragePath = workspacePath;
        main.StartPosition = FormStartPosition.Manual; main.Bounds = new Rectangle(Screen.PrimaryScreen.WorkingArea.Left + 30, Screen.PrimaryScreen.WorkingArea.Top + 30, 1500, 940);
        main.Show(); SettleDisplay();
        var devices = (IList)Field(main, "Devices");
        if (write)
        {
            for (int index = 0; index < 2; index++)
            {
                var meter = new GXDLMSDevice(new Gurux.Net.GXNet()) { DashboardId = Guid.NewGuid().ToString("N"), Name = "Restart fixture meter " + (index + 1) };
                meter.Objects.Add(new GXDLMSRegister("1.0.32.7.0.255") { Description = "Voltage" });
                meter.Objects.Add(new GXDLMSRegister("1.0.52.7.0.255") { Description = "Current" });
                meter.Objects.Add(new GXDLMSClock("0.0.1.0.0.255") { Description = "Meter clock" }); devices.Add(meter);
            }
        }
        else
        {
            var types = new List<Type>(GXDLMSClient.GetObjectTypes()) { typeof(Gurux.DLMS.ManufacturerSettings.GXDLMSAttributeSettings), typeof(Gurux.DLMS.ManufacturerSettings.GXDLMSAttribute) };
            var serializer = new XmlSerializer(typeof(GXDLMSDeviceCollection), null, types.ToArray(), null, "Gurux1");
            using var reader = File.OpenText(meterPath);
            foreach (GXDLMSDevice meter in (GXDLMSDeviceCollection)serializer.Deserialize(reader)) devices.Add(meter);
        }
        var meters = devices.Cast<GXDLMSDevice>().ToArray();
        Call(main, "ShowDashboardScreen", true); Call(main, "DisplayDashboard", meters[0], null); SettleDisplay();
        var form = (DashboardForm)Field(main, "activeDashboard"); var document = (DashboardDocument)Field(form, "document");
        if (write)
        {
            var primary = document.Dashboards[0]; primary.Name = "Restart supply overview";
            AddRestartSelection(form, meters, "1.0.32.7.0.255", 0, "Both meter voltages");
            AddRestartSelection(form, new[] { meters[0] }, "1.0.52.7.0.255", 1, "Current trend");
            AddRestartSelection(form, meters, "0.0.1.0.0.255", 2, "Meter clocks");
            Check(primary.Widgets.Count == 3 && primary.Widgets[0].Bindings.Count == 2, "Writer adds real picker selections from two meters");
            form.ResizeCard(0, 280, 190); form.ResizeCard(1, 320, 210); form.ResizeCard(2, 620, 280); form.ResizeCard(3, 480, 240);
            Check(form.MoveCard(3, 0, false), "Writer reorders table ahead of values and chart");
            var secondary = new DashboardDefinition { Name = "Restart secondary view" }; document.Dashboards.Add(secondary); Call(form, "BindDashboards", secondary);
            AddRestartSelection(form, new[] { meters[1] }, "1.0.52.7.0.255", 0, "Second meter current");
            form.SelectDashboard(primary); ((Panel)Field(form, "picker")).Visible = false; SettleDisplay();
            // Store meters through the application's serializer, without its MRU or preferences writes.
            typeof(MainForm).GetMethod("Save", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string), devices.GetType() }, null).Invoke(null, new object[] { meterPath, devices });
            ((NumericUpDown)Field(form, "interval")).Value = 17;
            var expected = new { ProcessId = Environment.ProcessId, Dpi = form.DeviceDpi, Document = JsonSerializer.Serialize(document), Order = RestartOrder(form), Bounds = RestartBounds(form) };
            File.WriteAllText(expectedPath, JsonSerializer.Serialize(expected));
            Capture(main, Path.Combine(directory, "before-close.png"));
            Check((bool)Call(main, "PrepareDashboardsForClose"), "Application dashboard close hook flushes pending autosave");
            Check(DashboardStore.Load(workspacePath).Dashboards[0].RefreshSeconds == 17, "Final edit persists even when closing before debounce");
        }
        else
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(expectedPath)); var expected = manifest.RootElement;
            Check(expected.GetProperty("ProcessId").GetInt32() != Environment.ProcessId, "Reader is a fresh OS process with no retained dashboard objects");
            Check(JsonSerializer.Serialize(document) == expected.GetProperty("Document").GetString(), "Every dashboard name, widget type, OBIS binding, attribute, order, dimension and refresh setting survives restart");
            Check(RestartOrder(form).SequenceEqual(expected.GetProperty("Order").EnumerateArray().Select(value => value.GetString())), "Rendered card order is restored");
            Check(document.Dashboards.Count == 2 && document.Dashboards[0].Widgets.Count == 3, "Reopen does not duplicate dashboards or widgets");
            foreach (var binding in document.Dashboards.SelectMany(definition => definition.Widgets).SelectMany(widget => widget.Bindings))
            {
                var meter = (GXDLMSDevice)Call(form, "ResolveMeter", binding); var obj = (GXDLMSObject)Call(form, "Resolve", binding);
                Check(meter != null && meter.DashboardId == binding.MeterId && obj != null && obj.LogicalName == binding.Obis, "Restored OBIS resolves to its saved meter: " + binding.MeterName + " / " + binding.Obis);
                Check(binding.Attribute == 2 && !((string)Call(form, "Quality", binding)).Contains("missing", StringComparison.OrdinalIgnoreCase), "Restored attribute and meter/object association are valid");
            }
            Check(document.Dashboards[0].Widgets[0].Bindings.Select(binding => binding.MeterId).Distinct().Count() == 2, "Identical OBIS selections remain distinct across meters");
            if (form.DeviceDpi == expected.GetProperty("Dpi").GetInt32())
                Check(JsonSerializer.Serialize(RestartBounds(form)) == expected.GetProperty("Bounds").GetRawText(), "Rendered tile/chart/table positions and sizes match before close at the same DPI");
            var secondary = document.Dashboards[1]; form.SelectDashboard(secondary); SettleDisplay();
            Check(((IEnumerable)Field(form, "layoutCards")).Cast<Control>().Count() == 1 && ((Label)Field(form, "heading")).Text == secondary.Name, "Second saved dashboard opens with its own restored selection");
            form.SelectDashboard(document.Dashboards[0]); SettleDisplay(); Capture(main, Path.Combine(directory, "after-reopen.png"));
            Check(!(bool)Field(form, "dirty"), "Reopening saved dashboards does not create unsaved edits");
            Check((bool)Call(main, "PrepareDashboardsForClose"), "Reopened application dashboard closes cleanly");
        }
        foreach (var meter in meters) { meter.KeepAliveStop(); (meter.Media as IDisposable)?.Dispose(); }
    }
}

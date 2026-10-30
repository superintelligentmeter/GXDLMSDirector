// Superintelligent Meter contributors, 2026-10-29. GPL-2.0-only.
// Offline Director-dialog review. No connections, settings saves, or user documents are used.
using System.Collections;
using System.Configuration;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using GXDLMSDirector;
using GXDLMSDirector.Macro;
using Gurux.DLMS;
using Gurux.DLMS.Enums;
using Gurux.DLMS.ManufacturerSettings;
using Gurux.DLMS.Objects;
using Gurux.DLMS.UI;
using Gurux.Net;

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
internal static partial class Program
{
    static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Assembly App = typeof(GXByteEditor).Assembly;
    static string output;
    static readonly List<object> Results = new();
    static int checks;
    static string filter;
    static readonly List<string> UnexpectedDialogs = new();
    static object Field(object obj, string name) => obj.GetType().GetField(name, All).GetValue(obj);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, All).Invoke(obj, args);
    static Form Create(string name, params object[] args) => (Form)Activator.CreateInstance(App.GetType("GXDLMSDirector." + name), All, null, args, null);
    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (Control nested in Descendants(c)) yield return nested; }
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        ++checks;
        Console.WriteLine("PASS " + message);
    }

    // Reset only the current process' settings values; never call Settings.Reset/Save.
    static void UseFixtureSettings()
    {
        var type = App.GetType("GXDLMSDirector.Properties.Settings");
        var settings = (ApplicationSettingsBase)type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        foreach (SettingsProperty p in settings.Properties)
        {
            var value = new SettingsPropertyValue(p) { SerializedValue = p.DefaultValue };
            settings[p.Name] = value.PropertyValue;
        }
        settings["ExternalMedias"] = "";
        settings["MacroFiles"] = "";
        settings["HdlcAddressMedia"] = "GXNet";
        settings["PlcMedia"] = "GXNet";
    }

    static GXManufacturerCollection Manufacturers()
    {
        var result = new GXManufacturerCollection();
        var m = new GXManufacturer { Name = "Review fixture manufacturer with a long display name", Identification = "TST", UseLogicalNameReferencing = true };
        m.Settings.Add(new GXAuthentication(Authentication.None, 16) { Selected = true });
        m.ServerSettings.Add(new GXServerAddress(HDLCAddressType.Default, 1, true));
        for (int i = 1; i <= 30; i++) m.ObisCodes.Add(new GXObisCode { ObjectType = ObjectType.Data, LogicalName = "0.0.96.1." + i + ".255", Description = "Review identification data " + i });
        result.Add(m);
        return result;
    }

    static Form Device(string folder)
    {
        var m = Manufacturers();
        return Create("DevicePropertiesForm", m, new GXDLMSDevice(new GXNet()) { Name = "Offline meter with a long readable name", Manufacturer = "TST", UseLogicalNameReferencing = true, MediaType = typeof(GXNet).FullName }, folder);
    }

    static Form Values(string folder)
    {
        string path = Path.Combine(folder, "values.xml");
        var data = new GXDLMSData("0.0.96.1.0.255") { Value = "Long offline snapshot value for readability review" };
        data.SetDataType(2, DataType.String);
        new GXDLMSObjectCollection { data }.Save(path, new GXXmlWriterSettings());
        var templates = new Dictionary<Type, List<IGXDLMSView>> { { typeof(GXDLMSData), new List<IGXDLMSView> { new Gurux.DLMS.UI.GXDLMSDataView() } } };
        return new GXValuesDlg(templates, path);
    }

    static void Review(string name, Func<Form> factory)
    {
        if (!string.IsNullOrEmpty(filter) && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
        Console.WriteLine("REVIEW " + name);
        using Form form = factory();
        if (comboReview && !modalComboReview) ApplicationTypography.Apply(form);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(12, 12);
        form.ShowInTaskbar = false;
        form.AutoValidate = AutoValidate.Disable;
        if (comboReview && modalComboReview) { ReviewModalCombos(form, name); return; }
        form.Show(); Application.DoEvents();
        if (comboReview) { ReviewComboStates(form, name); return; }
        Capture(form, name, "default");
        foreach (TabControl tabs in Descendants(form).OfType<TabControl>().ToArray())
        {
            for (int i = 0; i < tabs.TabCount; ++i)
            {
                // Activate ancestors before nested pages.
                for (Control parent = tabs.Parent; parent != null; parent = parent.Parent)
                    if (parent is TabPage p && p.Parent is TabControl tc) tc.SelectedTab = p;
                tabs.SelectedIndex = i;
                Capture(form, name, tabs.Name + "-" + i);
            }
        }
        if (form.FormBorderStyle == FormBorderStyle.Sizable || form.FormBorderStyle == FormBorderStyle.SizableToolWindow)
        {
            var normal = form.Size;
            form.Size = new Size(Math.Max(form.MinimumSize.Width, Math.Min(normal.Width, 700)), Math.Max(form.MinimumSize.Height, Math.Min(normal.Height, 500)));
            Capture(form, name, "compact");
            form.WindowState = FormWindowState.Minimized; Application.DoEvents();
            form.WindowState = FormWindowState.Normal; form.Size = normal;
            Capture(form, name, "restored");
        }
        if (name == "Manufacturers")
        {
            form.Size = new Size(700, 500); Application.DoEvents();
            Check(((Control)Field(form, "SupportedInterfacesTb")).Bottom < ((Control)Field(form, "NewBtn")).Top, "Manufacturer interface list stays above its actions at compact height");
        }
        if (name == "Settings")
        {
            ((TabControl)Field(form, "Tabs")).SelectedIndex = 0; Application.DoEvents();
            var view = (Panel)Field(form, "NotificationsView");
            var protocol = Descendants(view).Single(c => c.Name == "ProtocolCB");
            view.ScrollControlIntoView(protocol); Capture(form, name, "notification-bottom");
            Rectangle bounds = form.RectangleToClient(protocol.RectangleToScreen(protocol.ClientRectangle));
            Check(form.ClientRectangle.Contains(bounds) && view.PointToClient(protocol.PointToScreen(Point.Empty)).Y >= 0, "Notification protocol field can be reached without overlapping the footer");
        }
        if (name == "HdlcResolverSettings")
        {
            form.Size = new Size(700, 500);
            ((TabControl)Field(form, "tabControl1")).SelectedIndex = 1; Application.DoEvents();
            var page = (TabPage)Field(form, "tabPage2");
            var target = (Control)Field(form, "ClientAddressesTb");
            page.ScrollControlIntoView(target); Capture(form, name, "compact-client-addresses");
            Check(page.AutoScroll && page.VerticalScroll.Visible, "Compact HDLC search settings expose scrolling to client addresses");
        }
        // Dispose directly: several legacy closing handlers persist preferences.
    }

    static void Capture(Form form, string name, string state)
    {
        Application.DoEvents(); form.Refresh(); Application.DoEvents();
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
        string file = name + "-" + state + ".png"; bmp.Save(Path.Combine(output, file));
        var controls = Descendants(form).Where(c => c.Visible).Select(c => new { type = c.GetType().Name, c.Name, text = c is TextBoxBase || c is ComboBox ? "[fixture editor]" : c.Text, bounds = c.Bounds.ToString(), parent = c.Parent.Name, clipped = !c.Parent.ClientRectangle.Contains(c.Bounds), font = c.Font.SizeInPoints, c.TabIndex, c.TabStop, c.Enabled }).ToArray();
        Results.Add(new { screen = name, state, file, size = form.Size.ToString(), dpi = form.DeviceDpi, accept = (form.AcceptButton as Control)?.Name, cancel = (form.CancelButton as Control)?.Name, controls });
        File.WriteAllText(Path.Combine(output, "rendered-review.json"), JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
    }

    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, EnumWindow callback, IntPtr data);
    delegate bool EnumWindow(IntPtr window, IntPtr data);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    static void InvokeInvalid(Form form, string handler)
    {
        form.DialogResult = DialogResult.OK;
        using var close = new System.Windows.Forms.Timer { Interval = 70 };
        close.Tick += (_, _) => EnumThreadWindows(GetCurrentThreadId(), (window, _) => {
            var name = new StringBuilder(64); GetClassName(window, name, 64);
            if (name.ToString() == "#32770") SendMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
            return true;
        }, IntPtr.Zero);
        close.Start(); Call(form, handler, null, EventArgs.Empty); close.Stop();
        Check(form.DialogResult == DialogResult.None, form.GetType().Name + " keeps invalid edits open");
    }

    static void Validate()
    {
        var macro = new GXMacro { Name = "Delay review", Value = "1000" };
        using (var form = new GXMacroDelayDlg(macro))
        {
            ((DateTimePicker)Field(form, "DelayTb")).Value = new DateTime(2000, 1, 1);
            InvokeInvalid(form, "OKBtn_Click"); Check(macro.Value == "1000", "Invalid delay preserves the macro");
        }
        using (var form = new GXMacroCountDlg())
        {
            ((TextBox)Field(form, "CountTb")).Text = "0"; InvokeInvalid(form, "OKBtn_Click");
        }
        var original = new GXObisCode { ObjectType = ObjectType.Data, LogicalName = "0.0.96.1.0.255", Description = "Original" };
        var duplicate = new GXObisCode { ObjectType = ObjectType.Data, LogicalName = "0.0.96.1.1.255", Description = "Existing" };
        original.Attributes.Add(new GXDLMSAttributeSettings { Index = 2, Type = DataType.String });
        using (var form = new OBISCodeForm(null, null, new GXObisCodeCollection { original, duplicate }, original))
        {
            Check((ObjectType)((ComboBox)Field(form, "InterfaceCB")).SelectedItem == ObjectType.Data, "OBIS interface selection remains visible");
            var draft = (GXObisCode)Field(form, "Target"); draft.LogicalName = duplicate.LogicalName; draft.Description = "Rejected edit";
            draft.Attributes[0].Type = DataType.UInt32;
            Check(original.Attributes[0].Type == DataType.String, "OBIS attribute draft is isolated from the original");
            InvokeInvalid(form, "OKBtn_Click");
            Check(original.LogicalName == "0.0.96.1.0.255" && original.Description == "Original", "Duplicate OBIS rejection preserves the original");
        }
        using (var form = new OBISCodeForm(null, null, new GXObisCodeCollection { original, duplicate }, original))
        {
            var draft = (GXObisCode)Field(form, "Target");
            draft.Description = "Cancelled edit"; draft.Attributes[0].Type = DataType.UInt16;
            form.DialogResult = DialogResult.Cancel;
        }
        Check(original.Description == "Original" && original.Attributes[0].Type == DataType.String, "Cancelled OBIS edits preserve original values and attributes");
        using (var form = new OBISCodeForm(null, null, new GXObisCodeCollection { original, duplicate }, original))
        {
            var draft = (GXObisCode)Field(form, "Target");
            draft.Description = "Accepted edit"; draft.Attributes[0].Type = DataType.UInt16;
            Call(form, "OKBtn_Click", null, EventArgs.Empty);
        }
        Check(original.Description == "Accepted edit" && original.Attributes[0].Type == DataType.UInt16, "Accepted OBIS edit commits its draft and attributes");
    }

    static void ValidateSupportingDialogs()
    {
        var objects = new GXDLMSObjectCollection();
        var data = new GXDLMSData("0.0.96.1.0.255"); objects.Add(data);
        var script = new GXDLMSScript { Id = 1 };
        using (var form = new GXDLMSScriptDlg(script, objects, true))
        {
            Check(new[] { "AddBtn", "EditBtn", "RemoveBtn" }.All(n => !((Button)Field(form, n)).Enabled) && ((TextBox)Field(form, "IdTb")).ReadOnly, "Read-only script disables editing controls and identifier");
            Call(form, "AddScript", null, EventArgs.Empty); Call(form, "RemoveScript", null, EventArgs.Empty);
            Check(script.Actions.Count == 0 && script.Id == 1, "Read-only script event handlers preserve its model");
        }
        using (var form = new GXDLMSScriptDlg(script, objects, false))
        {
            ((TextBox)Field(form, "IdTb")).Text = "invalid"; InvokeInvalid(form, "OkBtn_Click");
            Check(script.Id == 1, "Invalid script identifier preserves the model");
        }
        var action = new GXDLMSScriptAction { Type = Gurux.DLMS.Objects.Enums.ScriptActionType.Write, Target = data, Index = 2, Parameter = new byte[] { 1, 2 }, ParameterDataType = DataType.OctetString };
        script.Actions.Add(action);
        using (var form = new GXDLMSScriptDlg(script, objects, false))
        {
            form.Show(); Application.DoEvents();
            var draft = (GXDLMSScript)Field(form, "target");
            ((byte[])draft.Actions[0].Parameter)[0] = 9;
            Check(((byte[])script.Actions[0].Parameter)[0] == 1 && ReferenceEquals(draft.Actions[0].Target, data), "Script draft copies parameter arrays and retains the shared object reference");
            ((ListView)Field(form, "Actions")).Items[0].Selected = true;
            Call(form, "RemoveScript", null, EventArgs.Empty);
            draft.Actions.Add(new GXDLMSScriptAction { Type = Gurux.DLMS.Objects.Enums.ScriptActionType.Write, Target = data, Index = 2, Parameter = "Added draft" });
            form.DialogResult = DialogResult.Cancel;
        }
        Check(script.Actions.Count == 1 && ReferenceEquals(script.Actions[0], action), "Cancelled script additions and removals preserve the original actions");
        using (var form = new GXDLMSScriptDlg(script, objects, false))
        {
            ((GXDLMSScript)Field(form, "target")).Actions.Clear();
            ((TextBox)Field(form, "IdTb")).Text = "invalid"; InvokeInvalid(form, "OkBtn_Click");
            Check(script.Actions.Count == 1 && script.Id == 1, "Invalid script identifier does not commit draft action changes");
        }
        using (var form = new GXDLMSScriptDlg(script, objects, false))
        {
            var draft = (GXDLMSScript)Field(form, "target"); draft.Actions[0].Index = 3;
            ((TextBox)Field(form, "IdTb")).Text = "2"; Call(form, "OkBtn_Click", null, EventArgs.Empty);
            Check(script.Id == 2 && script.Actions[0].Index == 3 && ReferenceEquals(script.Actions[0].Target, data), "Valid script acceptance commits the edited actions and identifier together");
        }
        var function = new GXDLMSFunctionControl(); objects.Add(function);
        using (var form = new GXFunctionControlFunctionDlg(function, "Review function", 2))
        {
            form.Show(); Application.DoEvents();
            var combo = (ComboBox)Field(form, "TargetTb"); combo.SelectedItem = data;
            Call(form, "AddBtn_Click", null, EventArgs.Empty);
            Check(form.TargetObjects.Count == 1 && ReferenceEquals(form.TargetObjects[0], data), "Function Add retains the selected shared object");
            var list = (ListView)Field(form, "FunctionListView"); list.Items[0].Selected = true;
            Call(form, "RemoveBtn_Click", null, EventArgs.Empty);
            Check(form.TargetObjects.Count == 0 && combo.Items.Contains(data), "Function Remove returns the object to the picker");
        }
    }

    [STAThread] static int Main(string[] args)
    {
        output = Path.GetFullPath(args.Length == 0 ? Path.Combine(Path.GetTempPath(), "gurux-host-ui-review") : args[0]);
        Directory.CreateDirectory(output);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetDefaultFont(new Font("Microsoft Sans Serif", 8.25f));
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); ApplicationTypography.Install();
        string fixture = Path.Combine(output, "fixture-data"); Directory.CreateDirectory(fixture);
        UseFixtureSettings();
        comboReview = args.Contains("--combos");
        modalComboReview = args.Contains("--modal");
        reverseComboReview = args.Contains("--reverse");
        filter = args.Skip(1).FirstOrDefault(a => a != "--combos" && a != "--modal" && a != "--reverse");
        var all = App.GetTypes().Where(t => !t.IsAbstract && (typeof(Form).IsAssignableFrom(t) || typeof(UserControl).IsAssignableFrom(t))).Select(t => new { t.FullName, constructors = t.GetConstructors(All).Select(c => string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))).ToArray() });
        File.WriteAllText(Path.Combine(output, "compiled-screen-inventory.json"), JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
        if (!comboReview) { Validate(); ValidateSupportingDialogs(); }
        using var dismissUnexpected = new System.Windows.Forms.Timer { Interval = 100 };
        dismissUnexpected.Tick += (_, _) => EnumThreadWindows(GetCurrentThreadId(), (window, _) => {
            var name = new StringBuilder(64); GetClassName(window, name, 64);
            if (name.ToString() == "#32770") { UnexpectedDialogs.Add("Unexpected native dialog during capture"); Console.WriteLine("UNEXPECTED NATIVE DIALOG"); SendMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero); }
            return true;
        }, IntPtr.Zero);
        dismissUnexpected.Start();
        Review("ByteEditor", () => new GXByteEditor(Encoding.ASCII.GetBytes("A long offline byte value for readability and editing review.")));
        Review("MacroDelay", () => new GXMacroDelayDlg(new GXMacro { Name = "Wait for meter to become ready", Value = "1500", Description = "Offline sample action with a longer description." }));
        Review("MacroCount", () => new GXMacroCountDlg());
        Review("MacroEdit", () => new GXMacroEditDlg(new GXMacro { Name = "Read meter identification", Description = "Offline sample macro action" }));
        Review("ExternalMedia", () => new GXExternalMediaForm());
        Review("PlcRegister", () => new GXPlcRegisterDlg(new byte[8], new byte[8]));
        Review("Conformance", () => Create("GXConformanceDlg", new GXConformanceSettings()));
        Review("Manufacturer", () => { var m = Manufacturers(); return new ManufacturerForm(m, m[0]); });
        Review("Manufacturers", () => new ManufacturersForm(Manufacturers(), "TST"));
        Review("ObisCode", () => { var m = Manufacturers(); return new OBISCodeForm(null, null, m[0].ObisCodes, m[0].ObisCodes[0]); });
        Review("ObisCodes", () => new OBISCodesForm(Manufacturers(), "TST", ObjectType.Data, "0.0.96.1.1.255"));
        Review("DeviceProperties", () => Device(fixture));
        Review("Translator", () => Create("DLMSTranslatorForm", fixture));
        Review("Settings", () => new GXSettingsDlg(new GXNet(), new GXDLMSTranslator()));
        Review("HdlcResolver", () => Create("GXHdlcAddressResolver", Manufacturers()));
        Review("HdlcResolverSettings", () => new GXHdlcAddressesResolverDlg(new GXNet()));
        Review("SerialMonitor", () => new GXSerialMonitor());
        Review("PlcDiscover", () => new GXPlcDiscover(null));
        Review("MacroEditor", () =>
        {
            var editor = new GXMacroView(new GXDLMSMeterCollection());
            for (int i = 1; i <= 25; i++) editor.AddAction(new GXMacro { Type = UserActionType.Delay, Name = "Wait before offline reading " + i, Value = "1000", Description = "Review-only delay. Never executed." });
            return editor;
        });
        Review("SnapshotValues", () => Values(fixture));
        string durations = Path.Combine(fixture, "durations.txt"); File.WriteAllLines(durations, new[] { "25", "35", "29", "42" });
        Review("Durations", () => new GXDurations(new List<KeyValuePair<string, string>> { new("Offline fixture", durations) }));
        using (var chartForm = new GXDurations(new List<KeyValuePair<string, string>> { new("Numeric", durations) }))
        {
            var chart = (System.Windows.Forms.DataVisualization.Charting.Chart)Field(chartForm, "chart1");
            Check(chart.Series[0].Points.Select(p => p.XValue).SequenceEqual(new double[] { 1, 2, 3, 4 }), "Untimed durations plot each sample at a distinct position");
            Check(chart.ChartAreas[0].AxisX.LabelStyle.Format == "", "Untimed durations use numeric axis labels");
        }
        string timedDurations = Path.Combine(fixture, "timed-durations.txt");
        File.WriteAllLines(timedDurations, new[] { "2026-10-29T10:00:00;25", "2026-10-29T10:00:01;35" });
        using (var chartForm = new GXDurations(new List<KeyValuePair<string, string>> { new("Timed", timedDurations) }))
        {
            var chart = (System.Windows.Forms.DataVisualization.Charting.Chart)Field(chartForm, "chart1");
            Check(chart.Series[0].XValueType == System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime && chart.Series[0].Points[0].XValue < chart.Series[0].Points[1].XValue && chart.ChartAreas[0].AxisX.LabelStyle.Format == "HH:mm:ss", "Timed durations retain their timestamp positions and clock labels");
        }
        File.WriteAllText(Path.Combine(output, "rendered-review.json"), JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Completed {Results.Count} rendered states and {checks} regression assertions.");
        Check(UnexpectedDialogs.Count == 0, "No unexpected native error dialogs during capture");
        return 0;
    }
}

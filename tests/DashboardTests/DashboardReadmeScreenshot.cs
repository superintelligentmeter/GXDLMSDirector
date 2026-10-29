// superintelligentmeter contributors, 2026-10-24. Real application screenshot with simulated readings. GPL-2.0-only.
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using GXDLMSDirector;
using Gurux.DLMS.Objects;
internal static partial class Program
{
    private static void ShowReadmeScreenshot(string directory)
    {
        using var supply = new DisposableDevice(); using var workshop = new DisposableDevice();
        supply.Value.Name = "Main supply"; workshop.Value.Name = "Workshop";
        supply.Value.Objects.Add(new GXDLMSRegister("1.0.32.7.0.255") { Description = "Supply voltage" });
        supply.Value.Objects.Add(new GXDLMSRegister("1.0.31.7.0.255") { Description = "Supply current" });
        workshop.Value.Objects.Add(new GXDLMSRegister("1.0.32.7.0.255") { Description = "Workshop voltage" });
        using var main = new OfflineMainForm(); main.DashboardStoragePath = Path.Combine(directory, "sample-workspace.json");
        main.Text = "Gurux GXDLMSDirector — Sample workspace";
        main.FormBorderStyle = FormBorderStyle.None;
        main.StartPosition = FormStartPosition.Manual; main.Bounds = new Rectangle(40, 40, 1600, 900); main.Show(); SettleDisplay();
        var devices = (IList)Field(main, "Devices"); devices.Add(supply.Value); devices.Add(workshop.Value);
        var tree = (TreeView)Field(main, "ObjectTree");
        foreach (var meter in new[] { supply.Value, workshop.Value }) tree.Nodes[0].Nodes.Add(new TreeNode(meter.Name) { Tag = meter, ImageIndex = 1, SelectedImageIndex = 1 }); tree.Nodes[0].Expand();
        Call(main, "ShowDashboardScreen", true); Call(main, "DisplayDashboard", supply.Value, null);
        var form = (DashboardForm)Field(main, "activeDashboard"); var document = (DashboardDocument)Field(form, "document"); var dashboard = document.Dashboards[0]; dashboard.Name = "Electrical overview";
        var voltage = Binding(supply.Value.Objects[0]); var current = Binding(supply.Value.Objects[1]); var other = Binding(workshop.Value.Objects[0]);
        dashboard.Widgets.Add(new DashboardWidget { Title = "Live measurements", Bindings = new() { voltage, current, other } });
        dashboard.Widgets.Add(new DashboardWidget { Kind = "Trend", Title = "Voltage trend · main supply and workshop", Bindings = new() { voltage, other } });
        Call(form, "BindDashboards", dashboard); Call(main, "SyncDashboardTree", supply.Value, form);
        DateTime start = DateTime.UtcNow.AddMinutes(-6);
        for (int index = 0; index <= 36; index++)
        {
            double v = 230 + Math.Sin(index * .38) * 1.1 + Math.Cos(index * .13) * .35, w = 228.8 + Math.Sin(index * .38 + .7) * .8;
            Call(form, "ApplyReadings", new List<DashboardReading> { new(voltage.Key, v.ToString("F1"), "V", v, null, start.AddSeconds(index * 10)), new(other.Key, w.ToString("F1"), "V", w, null, start.AddSeconds(index * 10)), new(current.Key, "12.4", "A", 12.4, null, start.AddSeconds(index * 10)) });
        }
        Call(form, "RefreshViews"); ((Label)Field(form, "status")).Text = "Sample workspace · Simulated readings · No physical meters connected";
        Call(form, "MarkChanged"); PumpAutosave();
        // Select the normal tree entry and show the existing layout without editing.
        tree.SelectedNode = tree.Nodes.Cast<TreeNode>().First(node => node.Text == "Dashboards").Nodes.Cast<TreeNode>().First(node => node.Text == "Electrical overview");
        main.ActiveControl = tree; SettleDisplay();
        Capture(main, Path.Combine(directory, "render-reference.png"));
        using var stop = new System.Windows.Forms.Timer { Interval = 500 }; var started = DateTime.UtcNow;
        stop.Tick += (_, _) => { if (File.Exists(Path.Combine(directory, "stop")) || DateTime.UtcNow - started > TimeSpan.FromMinutes(6)) { main.Dispose(); Application.ExitThread(); } }; stop.Start();
        Application.Run();
    }
}

// Intelmeter contributors, 2026-10-17. Feature-free application smoke test. GPL-2.0-only.
using System.Reflection;
using System.Windows.Forms;
using GXDLMSDirector;
internal static class Program
{
    private sealed class OfflineMainForm : MainForm { protected override void OnLoad(EventArgs e) { } }
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            var assembly = typeof(MainForm).Assembly;
            if (assembly.GetType("GXDLMSDirector.DashboardForm") != null) throw new Exception("Dashboard UI was included.");
            if (assembly.GetType("GXDLMSDirector.DashboardSettingsPage") != null) throw new Exception("Dashboard Settings page was included.");
            if (assembly.GetType("GXDLMSDirector.DashboardReader") != null) throw new Exception("Dashboard reader was included.");
            using var form = new OfflineMainForm(); form.Show(); Application.DoEvents();
            var tree = (TreeView)typeof(MainForm).GetField("ObjectTree", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
            if (tree.Nodes.Cast<TreeNode>().Any(n => n.Text == "Dashboards")) throw new Exception("Dashboard navigation is visible.");
            if (tree.Nodes.Count == 0 || tree.Nodes[0].Text != "Devices") throw new Exception("Original device navigation is missing.");
            typeof(MainForm).GetMethod("RestoreDashboardNavigation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null);
            if (!(bool)typeof(MainForm).GetMethod("PrepareDashboardsForClose", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null)) throw new Exception("Disabled lifecycle blocked close.");
            var meter = new GXDLMSDevice { DashboardId = "preserved-id" };
            if (meter.DashboardId != "preserved-id") throw new Exception("Identity metadata lost.");
            Console.WriteLine("PASS: feature-free assembly, original Devices screen and safe lifecycle."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

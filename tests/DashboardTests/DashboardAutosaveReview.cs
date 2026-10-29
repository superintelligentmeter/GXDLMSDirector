// Intelmeter contributors, 2026-10-17. Offline automatic persistence checks. GPL-2.0-only.
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GXDLMSDirector;
using Gurux.DLMS.Objects;
internal static partial class Program
{
    private static void PumpAutosave(int milliseconds = 650)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
    }
    private static void ReviewDashboardAutosave(string folder)
    {
        string path = Path.Combine(folder, "autosave-workspace.json");
        using var meter = new DisposableDevice(); meter.Value.Objects.Add(new GXDLMSRegister("1.0.32.7.0.255")); meter.Value.Objects.Add(new GXDLMSRegister("1.0.52.7.0.255"));
        using var form = new DashboardForm(meter.Value, storagePath: path); form.Size = new Size(1200, 800); form.Show(); Application.DoEvents();
        var doc = (DashboardDocument)Field(form, "document");
        doc.Dashboards[0].Widgets.Add(new DashboardWidget { Kind = "Values", Title = "Supply", Bindings = new() { Binding(meter.Value.Objects[0]), Binding(meter.Value.Objects[1]) } }); Call(form, "Render");
        Check(!Descendants(form).OfType<Button>().Any(button => button.Text == "Save"), "Manual Save button is removed");
        var interval = (NumericUpDown)Field(form, "interval"); interval.Value = 7;
        PumpAutosave();
        Check(DashboardStore.Load(path).Dashboards[0].RefreshSeconds == 7 && !(bool)Field(form, "dirty"), "Refresh setting is persisted automatically");
        Check(((LinkLabel)Field(form, "saveIndicator")).Text == "Saved", "Successful autosave shows Saved");
        Call(form, "RefreshViews"); Check(((LinkLabel)Field(form, "saveIndicator")).Text == "Saved", "Reading updates preserve the independent save status");
        form.MoveCard(0, 1, true); PumpAutosave();
        Check(DashboardStore.Load(path).Dashboards[0].Widgets[0].TileOrder.SequenceEqual(doc.Dashboards[0].Widgets[0].TileOrder), "Drag reorder persists without a Save action");
        string beforeResize = File.ReadAllText(path);
        var card = ((System.Collections.IEnumerable)Field(form, "layoutCards")).Cast<Control>().First();
        var grip = Descendants(card).Single(control => control.AccessibleName == "Resize card");
        typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grip, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 2, 2, 0) });
        form.ResizeCard(0, 420, 220);
        typeof(Control).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grip, new object[] { new MouseEventArgs(MouseButtons.None, 0, 80, 80, 0) });
        Check(doc.Dashboards[0].Widgets[0].Bindings.Single(binding => binding.Size != null).Size.Width == 420, "Mouse movement without the left button does not resize a captured card");
        PumpAutosave();
        Check(File.ReadAllText(path) == beforeResize && (bool)Field(form, "dirty"), "Resize movement does not write intermediate layouts");
        typeof(Control).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grip, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 2, 2, 0) });
        var savedSize = DashboardStore.Load(path).Dashboards[0].Widgets[0].Bindings.Single(binding => binding.Size != null).Size;
        Check(savedSize.Width == 420 && savedSize.Height == 220, "Releasing resize grip immediately saves the completed size");
        form.DeleteCard(0); PumpAutosave();
        Check(DashboardStore.Load(path).Dashboards[0].Widgets[0].Bindings.Count == 1, "Removing a tile persists automatically");
        interval.Value = 8; form.Hide(); form.SetWorkspaceActive(false); PumpAutosave();
        Check(DashboardStore.Load(path).Dashboards[0].RefreshSeconds == 8, "Autosave continues when dashboard monitoring is inactive and hidden");
        form.Show(); Application.DoEvents(); interval.Value = 9;
        Check(form.PrepareToClose() && DashboardStore.Load(path).Dashboards[0].RefreshSeconds == 9, "Closing flushes the latest pending edit without a normal save prompt");
        string savedText = File.ReadAllText(path);
        using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            interval.Value = 10; Check(!(bool)Call(form, "FlushAutoSave"), "Locked destination reports autosave failure");
            var indicator = (LinkLabel)Field(form, "saveIndicator");
            Check((bool)Field(form, "dirty") && indicator.Text.Contains("Not saved") && indicator.TabStop, "Autosave failure keeps dirty changes and a keyboard-accessible retry");
            Call(form, "RefreshViews"); Check(indicator.Text.Contains("Not saved"), "Meter updates cannot hide a save failure");
            Check(File.ReadAllText(path) == savedText, "Failed save preserves the previous file");
            Capture(form, Path.Combine(folder, "16-autosave-failure.png"));
        }
        var retry = (LinkLabel)Field(form, "saveIndicator");
        typeof(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(retry, new object[] { new LinkLabelLinkClickedEventArgs(retry.Links[0]) });
        Check(DashboardStore.Load(path).Dashboards[0].RefreshSeconds == 10 && retry.Text == "Saved" && !(bool)Field(form, "dirty"), "Retry saves retained changes when the destination becomes writable");
        Capture(form, Path.Combine(folder, "17-autosave-saved.png"));
        string protectedText = File.ReadAllText(path);
        typeof(DashboardForm).GetField("loadFailed", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, true); interval.Value = 11;
        Check(!(bool)Call(form, "FlushAutoSave") && File.ReadAllText(path) == protectedText, "Autosave never overwrites a workspace whose original load failed");
        typeof(DashboardForm).GetField("dirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, false);
    }
}

// Superintelligent Meter contributors, 2026-10-30. GPL-2.0-only.
// Focused evidence for approved rounded hosts around native combo controls.
using System.Collections;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using GXDLMSDirector;
using Gurux.DLMS.Objects;

internal static partial class Program
{
    private static void ReviewDashboardCombos(string folder)
    {
        var observations = new List<object>();
        void Inspect(Form window, string state)
        {
            Gurux.DLMS.UI.ApplicationTypography.Apply(window); SettleDisplay(); Capture(window, Path.Combine(folder, state + ".png"));
            var combos = Descendants(window).OfType<ComboBox>().Where(c => c.Visible).ToArray();
            foreach (var combo in combos)
            {
                string region = "none";
                if (combo.Region != null) { using var graphics = combo.CreateGraphics(); region = combo.Region.GetBounds(graphics).ToString(); }
                observations.Add(new { state, combo.AccessibleName, combo.Name, dpi = combo.DeviceDpi, nativeHeight = combo.Height, preferredHeight = combo.PreferredHeight, bounds = combo.Bounds.ToString(), parentType = combo.Parent.GetType().Name, parentBounds = combo.Parent.ClientRectangle.ToString(), font = combo.Font.SizeInPoints, style = combo.FlatStyle.ToString(), region });
                if (combo.Parent is DashboardFieldHost host)
                {
                    Check(host.ClientRectangle.Contains(combo.Bounds), state + ": rounded combo contains its native editor " + combo.AccessibleName);
                    Check(Math.Abs(combo.Top * 2 + combo.Height - host.ClientSize.Height) <= 1, state + ": rounded combo editor is vertically centered " + combo.AccessibleName);
                    Check(combo.ClientSize.Height - 2 >= combo.Font.Height, state + ": native combo item fits its font " + combo.AccessibleName);
                    Check(combo.Region != null && combo.FlatStyle == FlatStyle.Flat, state + ": inner rectangular frame stays suppressed " + combo.AccessibleName);
                    combo.Focus(); combo.DroppedDown = true; Check(combo.DroppedDown, state + ": native dropdown still opens " + combo.AccessibleName); combo.DroppedDown = false;
                }
            }
            File.WriteAllText(Path.Combine(folder, "combo-review.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        }
        using var first = new DisposableDevice(); using var second = new DisposableDevice();
        first.Value.Name = "Main building meter — long display name"; second.Value.Name = "Workshop meter";
        first.Value.Objects.Add(new GXDLMSRegister("1.0.32.7.0.255") { Description = "Voltage L1" });
        second.Value.Objects.Add(new GXDLMSRegister("1.0.52.7.0.255") { Description = "Voltage L2" });
        using var main = new OfflineMainForm { DashboardStoragePath = Path.Combine(folder, "workspace.json") };
        main.Show(); ((IList)Field(main, "Devices")).Add(first.Value); ((IList)Field(main, "Devices")).Add(second.Value);
        Call(main, "ShowDashboardScreen", true); Call(main, "DisplayDashboard", first.Value, null);
        var form = (DashboardForm)Field(main, "activeDashboard");
        int cycle = 0;
        foreach (var screen in Screen.AllScreens.Concat(Screen.AllScreens.Take(1)))
        {
            main.WindowState = FormWindowState.Normal;
            main.Location = new Point(screen.WorkingArea.Left + 20, screen.WorkingArea.Top + 20); SettleDisplay();
            foreach (var size in new[] { new Size(Math.Min(1500, screen.WorkingArea.Width - 40), Math.Min(940, screen.WorkingArea.Height - 40)), new Size(900, 650) })
            {
                main.Size = size; Call(form, "OpenPicker", first.Value.Objects[0]); SettleDisplay();
                string state = "picker-" + cycle + "-" + main.DeviceDpi + "-" + size.Width;
                Inspect(main, state);
                Call(form, "OpenPicker", (object)null); Inspect(main, state + "-no-selection");
                Call(form, "OpenPicker", first.Value.Objects[0]);
                ((ComboBox)Field(form, "display")).SelectedIndex = 1; Inspect(main, state + "-trend");
                ((ComboBox)Field(form, "display")).SelectedIndex = 0;
                main.WindowState = FormWindowState.Minimized; SettleDisplay(); main.WindowState = FormWindowState.Maximized; SettleDisplay(); Inspect(main, state + "-restored");
                main.WindowState = FormWindowState.Normal;
            }
            using var relink = new DashboardRelinkDialog(form.Font, new[] { first.Value.Name, second.Value.Name }, new object[] { first.Value.Name, second.Value.Name });
            relink.StartPosition = FormStartPosition.Manual; relink.Location = main.Location; relink.Show(main); Inspect(relink, "relink-" + cycle + "-" + relink.DeviceDpi);
            cycle++;
        }
        typeof(DashboardForm).GetField("dirty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(form, false);
    }
}

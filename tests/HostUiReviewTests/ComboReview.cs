// Superintelligent Meter contributors, 2026-10-30. GPL-2.0-only.
// Offline native-combo inventory and actual monitor geometry evidence.
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using Gurux.DLMS.Enums;
using Gurux.DLMS.ManufacturerSettings;

internal static partial class Program
{
    static bool comboReview, modalComboReview, reverseComboReview;
    static void ReviewModalCombos(Form form, string name)
    {
        using var owner = new Form { Font = Gurux.DLMS.UI.ApplicationTypography.BodyFont, ShowInTaskbar = false, Size = new Size(900, 650) };
        var origin = (reverseComboReview ? Screen.AllScreens.Last() : Screen.AllScreens.First()).WorkingArea;
        owner.StartPosition = FormStartPosition.Manual; owner.Location = new Point(origin.Left + 20, origin.Top + 20);
        form.Location = owner.Location; owner.Show();
        Exception failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 350 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                Check(form.Font.Name == "Segoe UI", name + ": modal production Idle applied typography before the monitor review");
                ReviewComboStates(form, name);
            }
            catch (Exception error) { failure = error; }
            finally { form.Dispose(); }
        };
        timer.Start(); form.ShowDialog(owner);
        if (failure != null) throw failure;
    }
    static readonly List<object> ComboResults = new();
    static IEnumerable<ComboBox> Combos(Control root) => Descendants(root).OfType<ComboBox>().Concat(
        Descendants(root).OfType<ToolStrip>().SelectMany(t => t.Items.OfType<ToolStripComboBox>().Select(c => c.ComboBox))).Distinct();

    static void SettleCombos()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 100) { Application.DoEvents(); Thread.Sleep(5); }
    }

    static string ComboRegion(ComboBox combo)
    {
        if (combo.Region == null) return "none";
        using var graphics = combo.CreateGraphics(); return combo.Region.GetBounds(graphics).ToString();
    }

    static bool ComboFullyVisible(ComboBox combo)
    {
        if (!combo.Visible) return false;
        Rectangle bounds = combo.RectangleToScreen(combo.ClientRectangle);
        for (Control parent = combo.Parent; parent != null; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds)) return false;
        return true;
    }
    static void AssertSettingsLayout(Form form, string name, string state)
    {
        if (name != "DeviceProperties" && name != "Manufacturer") return;
        string context = name + " " + state;
        var tabs = (TabControl)Field(form, name == "DeviceProperties" ? "DeviceTab" : "tabControl1");
        var footer = Descendants(form).Single(c => c.Name == "SettingsActions");
        Check(tabs.Parent.ClientRectangle.Contains(tabs.Bounds) && footer.Parent.ClientRectangle.Contains(footer.Bounds)
            && tabs.Bottom <= footer.Top, context + ": tab content and footer remain separate and contained");
        bool HorizontalVisible(Control control)
        {
            var rectangle = control.RectangleToScreen(control.ClientRectangle);
            for (Control parent = control.Parent; parent != null; parent = parent.Parent)
            {
                var parentRectangle = parent.RectangleToScreen(parent.ClientRectangle);
                if (rectangle.Left < parentRectangle.Left || rectangle.Right > parentRectangle.Right) return false;
            }
            return true;
        }
        var combos = Combos(form).Where(c => c.Visible).ToArray();
        Check(combos.All(HorizontalVisible), context + ": every visible combo remains horizontally reachable");
        var numbers = Descendants(form).OfType<NumericUpDown>().Where(c => c.Visible).ToArray();
        Check(numbers.All(c => c.Parent.ClientRectangle.Contains(c.Bounds) && HorizontalVisible(c)), context + ": numeric editors remain inside their page");
        var actions = Descendants(footer).OfType<Button>().ToArray();
        Check(actions.All(c => c.Parent.ClientRectangle.Contains(c.Bounds) && HorizontalVisible(c)), context + ": footer actions remain fully available");
        Check(ReferenceEquals(form.AcceptButton, Field(form, "OKBtn")) && ReferenceEquals(form.CancelButton, Field(form, "CancelBtn")), context + ": original Enter and Escape actions are preserved");
        if (name == "DeviceProperties")
        {
            var pairs = new[] { ("ClientAddLbl", "ClientAddTB"), ("ResendLbl", "ResendTb"), ("PhysicalServerAddressLbl", "PhysicalServerAddressTB") };
            Check(pairs.All(pair => ((Control)Field(form, pair.Item1)).Right + 2 <= ((Control)Field(form, pair.Item2)).Left), context + ": right-column labels cannot cover numeric values");
        }        if (name == "Manufacturer")
        {
            var size = (Control)Field(form, "SizeCb");
            var physical = (Control)Field(form, "PhysicalServerAddTB");
            var logical = (Control)Field(form, "LogicalServerAddTB");
            Check(!size.Bounds.IntersectsWith(physical.Bounds) && !size.Bounds.IntersectsWith(logical.Bounds)
                && !physical.Bounds.IntersectsWith(logical.Bounds), context + ": address inputs do not overlap the size selector");
        }
    }
    static void CaptureCombos(Form form, string name, string state)
    {
        SettleCombos();
        Capture(form, name, state);
        var combos = Combos(form).Select(c => new
        {
            c.Name, c.AccessibleName, c.Visible, fullyVisible = ComboFullyVisible(c), c.Enabled, c.DeviceDpi,
            screenshotBounds = new Rectangle(c.PointToScreen(Point.Empty).X - form.Left, c.PointToScreen(Point.Empty).Y - form.Top, c.Width, c.Height).ToString(),
            bounds = c.Bounds.ToString(), parent = c.Parent?.Name, parentType = c.Parent?.GetType().Name,
            parentBounds = c.Parent?.ClientRectangle.ToString(), nativeHeight = c.Height, preferredHeight = c.PreferredHeight,
            parentHeight = c.Parent?.Height, font = c.Font.SizeInPoints, textHeight = c.Font.Height, textFits = c.ClientSize.Height >= c.Font.Height, style = c.FlatStyle.ToString(),
            dropdownStyle = c.DropDownStyle.ToString(), items = c.Items.Count,
            outsideParent = c.Parent != null && !c.Parent.ClientRectangle.Contains(c.Bounds),
            region = ComboRegion(c)
        }).ToArray();
        ComboResults.Add(new { screen = name, state, dpi = form.DeviceDpi, combos });
        File.WriteAllText(Path.Combine(output, "combo-review.json"), JsonSerializer.Serialize(ComboResults, new JsonSerializerOptions { WriteIndented = true }));
        AssertSettingsLayout(form, name, state);
    }

    static void CaptureComboTabs(Form form, string name, string prefix)
    {
        CaptureCombos(form, name, prefix);
        foreach (TabControl tabs in Descendants(form).OfType<TabControl>().ToArray())
        {
            for (int i = 0; i < tabs.TabCount; i++)
            {
                for (Control parent = tabs.Parent; parent != null; parent = parent.Parent)
                    if (parent is TabPage p && p.Parent is TabControl tc) tc.SelectedTab = p;
                tabs.SelectedIndex = i;
                CaptureCombos(form, name, prefix + "-" + tabs.Name + "-" + i);
            }
        }
        if (name == "DeviceProperties")
        {
            var media = (ComboBox)Field(form, "MediasCB");
            for (Control parent = media.Parent; parent != null; parent = parent.Parent)
                if (parent is TabPage p && p.Parent is TabControl tc) tc.SelectedTab = p;
            var original = media.SelectedItem;
            foreach (var item in media.Items.Cast<object>().Where(x => new[] { "GXNet", "GXSerial", "GXTerminal" }.Contains(x.GetType().Name)).ToArray())
            {
                media.SelectedItem = item;
                CaptureCombos(form, name, prefix + "-media-" + item.GetType().Name);
            }
            media.SelectedItem = original;
            var authentication = (ComboBox)Field(form, "AuthenticationCB");
            var selected = authentication.SelectedItem;
            var ecdsa = new GXAuthentication(Authentication.HighECDSA, 16);
            authentication.Items.Add(ecdsa); authentication.SelectedItem = ecdsa;
            CaptureCombos(form, name, prefix + "-ecdsa");
            authentication.SelectedItem = selected; authentication.Items.Remove(ecdsa);
            var signing = Descendants(form).OfType<ComboBox>().FirstOrDefault(c => c.Name == "SigningCb");
            if (signing != null)
            {
                for (Control parent = signing.Parent; parent != null; parent = parent.Parent)
                    if (parent is TabPage p && p.Parent is TabControl tc) tc.SelectedTab = p;
                var initial = signing.SelectedItem;
                signing.SelectedItem = signing.Items.Cast<object>().FirstOrDefault(x => x.ToString() == "GeneralSigning");
                foreach (var key in Descendants(form).OfType<ComboBox>().Where(c => c.Name.EndsWith("KeysCb")))
                {
                    for (Control parent = key.Parent; parent != null; parent = parent.Parent)
                        if (parent is ScrollableControl scroll && scroll.AutoScroll) scroll.ScrollControlIntoView(key);
                    CaptureCombos(form, name, prefix + "-" + key.Name);
                    Check(ComboFullyVisible(key), name + " " + prefix + ": signing selector is reachable within its scrolling viewport: " + key.Name);
                }
                signing.SelectedItem = initial;
            }
        }
    }

    static void ReviewComboStates(Form form, string name)
    {
        if (!Combos(form).Any()) { CaptureCombos(form, name, "no-combo"); return; }
        foreach (var item in Descendants(form).OfType<ToolStrip>().SelectMany(t => t.Items.OfType<ToolStripComboBox>())) item.ComboBox.Name = item.Name;
        Size original = form.Size;
        bool resizable = form.FormBorderStyle == FormBorderStyle.Sizable || form.FormBorderStyle == FormBorderStyle.SizableToolWindow;
        int trip = 0;
        var displays = (reverseComboReview ? Screen.AllScreens.Reverse() : Screen.AllScreens).ToArray();
        foreach (Screen screen in displays.Concat(displays.Take(1)))
        {
            form.WindowState = FormWindowState.Normal;
            form.Location = new Point(screen.WorkingArea.Left + 20, screen.WorkingArea.Top + 20);
            SettleCombos();
            if (resizable) form.Size = new Size(Math.Min(Math.Max(original.Width, 900), screen.WorkingArea.Width - 40), Math.Min(Math.Max(original.Height, 700), screen.WorkingArea.Height - 40));
            string prefix = screen.DeviceName.Replace("\\", "").Replace(".", "") + "-" + form.DeviceDpi + "-trip" + trip++;
            CaptureComboTabs(form, name, prefix + (resizable ? "-wide" : "-fixed"));
            if (form.Owner != null)
            {
                form.Owner.WindowState = FormWindowState.Minimized; SettleCombos();
                form.Owner.WindowState = FormWindowState.Normal; SettleCombos();
                CaptureCombos(form, name, prefix + "-owner-restored");
            }
            if (!resizable) continue;
            form.Size = new Size(Math.Max(form.MinimumSize.Width, Math.Min(form.Width, 700)), Math.Max(form.MinimumSize.Height, Math.Min(form.Height, 500)));
            CaptureComboTabs(form, name, prefix + "-narrow");            if (name == "Manufacturer")
            {
                var size = (ComboBox)Field(form, "SizeCb");
                var page = (TabPage)Field(form, "tabPage1");
                ((TabControl)page.Parent).SelectedTab = page; page.ScrollControlIntoView(size);
                CaptureCombos(form, name, prefix + "-narrow-size-visible");
                Check(ComboFullyVisible(size), prefix + ": Manufacturer size selector remains reachable at compact height");
            }
            form.WindowState = FormWindowState.Minimized; SettleCombos(); form.WindowState = FormWindowState.Normal;
            CaptureCombos(form, name, prefix + "-restored");
        }
    }
}

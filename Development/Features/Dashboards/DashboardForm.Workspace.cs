// superintelligentmeter contributors, 2026-10-17. Native dashboard mockup layout. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Gurux.DLMS.Objects;

namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm
    {
        private static readonly Color ink = Color.FromArgb(24, 48, 78), muted = Color.FromArgb(85, 105, 130), border = Color.FromArgb(212, 223, 237);
        private Font headingFont, strongFont;
        private Panel header, picker, monitoringFrame, noticeFrame, noticeBlock;
        private Label refreshLabel;
        private Button readNow;
        private DashboardFieldHost intervalField;
        private Label heading, connection, selectedCount;
        private Panel headingActions;
        private TextBox search, widgetTitle;
        private ComboBox attribute, display, meterPicker;
        private sealed record MeterChoice(GXDLMSDevice Meter) { public override string ToString() => Meter.Name + " [" + Meter.DashboardId.Substring(0, Math.Min(6, Meter.DashboardId.Length)) + "]"; }
        private ListView objectPicker;
        private Label pickerSummary, pickerReason;
        private Button pickerAdd, editLayoutButton, connectButton, addObisButton;
        private bool userPaused, dragInProgress;
        private DateTime autoRetryAfter;
        private Panel workspaceBody, workspaceCenter, pickerViewport;
        private readonly ToolTip dashboardTips = new() { AutoPopDelay = 15000 };
        private readonly HashSet<GXDLMSObject> selectedObjects = new();
        private readonly List<Control> layoutCards = new();
        private bool fillingPicker, editLayout;
        private int chartMinutes = 15;
        internal event Action DefinitionsChanged;
        internal IEnumerable<DashboardDefinition> Definitions => document.Dashboards;
        internal void SelectDashboard(DashboardDefinition d) { if (!busy && d != null) dashboards.SelectedItem = d; }
        internal void NewDashboard()
        {
            if (busy) return;
            var name = Ask("Dashboard name", "New dashboard"); if (name == null) return;
            if (document.Dashboards.Count >= 50) throw new InvalidOperationException("Maximum 50 dashboards.");
            var d = new DashboardDefinition { Name = name }; document.Dashboards.Add(d); BindDashboards(d); MarkChanged();
            DefinitionsChanged?.Invoke(); OpenPicker(null);
        }
        internal void RenameDashboard(DashboardDefinition d)
        {
            if (busy || d == null) return;
            var name = Ask("Dashboard name", d.Name); if (name == null) return;
            d.Name = name; MarkChanged(); BindDashboards(Current); DefinitionsChanged?.Invoke();
        }
        internal void DeleteDashboard(DashboardDefinition d)
        {
            if (busy || d == null) return;
            if (MessageBox.Show(DialogOwner, "Delete dashboard \"" + d.Name + "\"? This cannot be undone.", "Dashboards",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            bool wasCurrent = ReferenceEquals(d, Current);
            document.Dashboards.Remove(d); MarkChanged();
            BindDashboards(wasCurrent ? document.Dashboards.FirstOrDefault() : Current); DefinitionsChanged?.Invoke();
        }
        private int Scale(int n) => (int)Math.Round(n * DeviceDpi / 96f);
        private Button Command(string text, Action action)
        {
            var b = new DashboardButton { Text = text, AutoSize = false, Height = Scale(32), MinimumSize = new Size(Scale(74), Scale(32)), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = ink, Padding = new Padding(5, 0, 5, 0) };
            b.Width = Math.Max(Scale(74), TextRenderer.MeasureText(text, Font).Width + Scale(24));
            b.FlatAppearance.BorderColor = border;
            b.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(DialogOwner, ex.Message, "Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }; return b;
        }
        // Custom borders must repaint their old bounds as well as the newly exposed strip.
        private class DashboardSurface : Panel
        {
            internal DashboardSurface()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            }
            protected override bool IsInputKey(Keys keyData)
            {
                var code = keyData & Keys.KeyCode;
                return TabStop && (keyData & Keys.Control) != 0 && code is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);
            }
        }
        private class RoundedSurface : DashboardSurface
        {
            internal static GraphicsPath Outline(RectangleF bounds, float radius)
            {
                var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
                if (d <= 0) return path;
                path.AddArc(bounds.X, bounds.Y, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
                path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
            }
            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent?.BackColor ?? Color.White);
                using var path = Outline(new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 10 * DeviceDpi / 96f);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var fill = new SolidBrush(BackColor); e.Graphics.FillPath(fill, path);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e); using var path = Outline(new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 10 * DeviceDpi / 96f);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var pen = new Pen(border); e.Graphics.DrawPath(pen, path);
            }
        }
        private Panel Card() => new RoundedSurface { BackColor = Color.White, Padding = new Padding(1) };
        private void BuildWorkspace()
        {
            ForeColor = ink; headingFont = new Font(Font.FontFamily, 15, FontStyle.Bold); strongFont = new Font(Font, FontStyle.Bold);
            header = new Panel { Dock = DockStyle.Top, Height = Scale(74), Padding = new Padding(12) };
            heading = new Label { Text = "Electrical overview", Font = headingFont, AutoEllipsis = true };
            headingActions = new Panel();
            connectButton = Command("Connect", ConnectBoundMeters); connectButton.Visible = false; headingActions.Controls.Add(connectButton);
            addObisButton = Command("Add OBIS", () => OpenPicker(null)); headingActions.Controls.Add(addObisButton);
            editLayoutButton = Command("Edit layout", () => SetEditLayout(!editLayout)); headingActions.Controls.Add(editLayoutButton);
            var more = new ContextMenuStrip();
            more.Items.Add("New dashboard", null, (_, _) => NewDashboard());
            more.Items.Add("Rename dashboard", null, (_, _) => RenameDashboard(Current));
            more.Items.Add("Delete dashboard", null, (_, _) => DeleteDashboard(Current));
            more.Items.Add("Relink meter...", null, (_, _) => RelinkMeter());
            var options = Command("More", () => { });
            options.Click += (_, _) => more.Show(options, new Point(options.Width, options.Height), ToolStripDropDownDirection.BelowLeft);
            more.Opened += (_, _) =>
            {
                // Use the owning button's monitor, including negative desktop coordinates.
                // Opened uses the menu's actual size after WinForms has applied its DPI.
                var area = Screen.FromControl(options).WorkingArea;
                more.Location = KeepMenuOnScreen(more.Bounds, area);
            };
            headingActions.Controls.Add(options); options.Disposed += (_, _) => more.Dispose();
            header.Controls.AddRange(new Control[] { heading, headingActions });
            header.Layout += (_, _) => LayoutHeader();
            headingActions.ControlAdded += (_, _) => LayoutHeader();
            Padding = new Padding(Scale(10));
            commands.BackColor = Color.FromArgb(246, 248, 251);
            connection = new Label { AutoSize = true, Text = "0 of 0 meters connected", ForeColor = muted };
            refreshLabel = new Label { Text = "Refresh every (s)", AutoSize = true, ForeColor = muted };
            monitor.MinimumSize = new Size(Scale(132), Scale(34)); monitor.AutoSize = false; monitor.Size = new Size(Scale(132), Scale(34)); monitor.Appearance = Appearance.Button; monitor.Text = "Start monitoring";
            readNow = Command("Read now", () => _ = Poll());
            intervalField = new DashboardFieldHost(interval) { Size = new Size(Scale(70), Scale(32)) };
            commands.Controls.AddRange(new Control[] { connection, refreshLabel, intervalField, readNow, monitor });
            for (int i = 0; i < commands.Controls.Count; i++) commands.Controls[i].TabIndex = i;
            UpdateMonitorText();
            commands.Layout += (_, _) => AlignMonitoring();
            monitoringFrame = new RoundedSurface { Dock = DockStyle.Top, Height = Scale(60), BackColor = commands.BackColor, Padding = new Padding(Scale(12), Scale(4), Scale(12), Scale(4)) };
            monitoringFrame.Controls.Add(commands);
            workspaceCenter = new Panel { Dock = DockStyle.Fill }; workspaceCenter.Controls.Add(widgets);
            picker = new RoundedSurface { Dock = DockStyle.Right, Width = Scale(320), BackColor = Color.White, Visible = false, Padding = new Padding(Scale(14)), Margin = Padding.Empty };
            workspaceBody = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, Scale(10), 0, 0) }; workspaceBody.Controls.Add(workspaceCenter); workspaceBody.Controls.Add(picker);
            workspaceBody.SizeChanged += (_, _) => LayoutWorkspacePanels();
            picker.VisibleChanged += (_, _) =>
            {
                LayoutWorkspacePanels();
                if (picker.Visible) dashboardTypographyDirty = true;
                QueueWorkspaceLayout();
            };
            status.AutoSize = false; status.Dock = DockStyle.Fill; status.ForeColor = muted; status.Padding = Padding.Empty; status.TextAlign = ContentAlignment.MiddleLeft; status.AutoEllipsis = true;
            noticeFrame = new RoundedSurface { Dock = DockStyle.Top, Height = Scale(36), BackColor = commands.BackColor, Padding = new Padding(Scale(14), Scale(5), Scale(14), Scale(5)) };
            saveIndicator = new LinkLabel { Text = "Autosave on", Dock = DockStyle.Right, Width = Scale(126), TextAlign = ContentAlignment.MiddleRight, LinkArea = new LinkArea(0, 0), TabStop = false, LinkColor = muted, ForeColor = muted, AutoEllipsis = true, AccessibleName = "Dashboard save status" };
            saveIndicator.LinkClicked += (_, _) => FlushAutoSave();
            noticeFrame.Controls.Add(status); noticeFrame.Controls.Add(saveIndicator);
            noticeBlock = new Panel { Dock = DockStyle.Top, Height = Scale(46), Visible = false, Padding = new Padding(0, Scale(6), 0, Scale(6)) }; noticeFrame.Dock = DockStyle.Fill; noticeBlock.Controls.Add(noticeFrame);
            status.Text = "Live trends show samples collected while monitoring. No historical data loaded.";
            Controls.Add(workspaceBody); Controls.Add(noticeBlock); Controls.Add(monitoringFrame); Controls.Add(header);
            header.TabIndex = 0; workspaceBody.TabIndex = 1; monitoringFrame.TabIndex = 1; commands.TabIndex = 0; widgets.TabIndex = 1; picker.TabIndex = 2;
            interval.AccessibleName = "Refresh interval in seconds";
            foreach (Control command in headingActions.Controls) command.TabIndex = headingActions.Controls.IndexOf(command);
            BuildPicker();
            InitializeDragLayout();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            // Child size/visibility events can be suppressed or nested during a
            // minimize/restore docking pass. Reconcile from the final parent size.
            LayoutWorkspacePanels();
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) QueueWorkspaceLayout();
        }
        private bool workspaceLayoutQueued;
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e); QueueWorkspaceLayout();
        }
        private void QueueWorkspaceLayout()
        {
            if (!IsHandleCreated || IsDisposed || workspaceLayoutQueued) return;
            workspaceLayoutQueued = true;
            BeginInvoke(new Action(() =>
            {
                workspaceLayoutQueued = false;
                if (IsDisposed || !Visible || workspaceBody == null || DialogOwner.WindowState == FormWindowState.Minimized) return;
                // Wait until the host's restore/docking transaction has finished.
                if (dashboardTypographyDirty || !DashboardTypographyMatchesOwner()) RestoreDashboardTypography();
                LayoutWorkspacePanels(); PerformLayout();
                LayoutHeader(); AlignMonitoring(); PerformLayout();
                monitoringFrame.PerformLayout(); commands.PerformLayout(); workspaceBody.PerformLayout();
                ResizeWidgets(); Invalidate(true);
            }));
        }
        private bool arrangingWorkspace;
        private void LayoutWorkspacePanels()
        {
            if (arrangingWorkspace || workspaceBody == null || picker == null || header == null || monitoringFrame == null || noticeBlock == null || !Visible || DialogOwner.WindowState == FormWindowState.Minimized) return;
            arrangingWorkspace = true;
            try
            {
                bool fullPicker = picker.Visible && ClientSize.Width - Padding.Horizontal < Scale(820);
                workspaceCenter.Visible = !fullPicker;
                header.Visible = !fullPicker; monitoringFrame.Visible = !fullPicker; // The second status bar appears only when saving fails; everything else is shown in the monitoring bar.
                noticeBlock.Visible = autoSaveFailed;
                picker.Dock = fullPicker ? DockStyle.Fill : DockStyle.Right;
                if (!fullPicker) picker.Width = Math.Min(Scale(320), workspaceBody.ClientSize.Width);
                // Dock the right-hand picker before the fill workspace; changing z-order
                // here can let the fill panel obscure the picker at desktop widths.
                workspaceBody.Controls.SetChildIndex(workspaceCenter, 0);
                workspaceBody.Controls.SetChildIndex(picker, 1);
                workspaceCenter.Padding = picker.Visible && !fullPicker ? new Padding(0, 0, Scale(18), 0) : Padding.Empty;
            }
            finally { arrangingWorkspace = false; }
        }
        private void SetEditLayout(bool enabled)
        {
            editLayout = enabled; editLayoutButton.Text = enabled ? "Done" : "Edit layout";
            if (enabled) monitor.Checked = false;
            status.Text = enabled ? "Drag to move; use the lower-right corner to resize. Changes save automatically." : "Layout ready. Changes save automatically.";
            dashboardTips.SetToolTip(editLayoutButton, enabled ? "Drag cards to move them; use the lower-right corner to resize." : "Rearrange and resize cards");
            Render();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && picker.Visible) { picker.Visible = false; addObisButton.Focus(); return true; }
            if (keyData == Keys.Escape && editLayout) { SetEditLayout(false); editLayoutButton.Focus(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        internal static Point KeepMenuOnScreen(Rectangle menu, Rectangle area) => new Point(
            Math.Clamp(menu.Left, area.Left, Math.Max(area.Left, area.Right - menu.Width)),
            Math.Clamp(menu.Top, area.Top, Math.Max(area.Top, area.Bottom - menu.Height)));
        private int DashboardCommandWidth(ButtonBase button)
        {
            int textWidth = ReferenceEquals(button, editLayoutButton)
                ? Math.Max(TextRenderer.MeasureText("Edit layout", button.Font).Width, TextRenderer.MeasureText("Done", button.Font).Width)
                : TextRenderer.MeasureText(button.Text, button.Font).Width;
            return Math.Max(Scale(74), textWidth + Scale(24));
        }
        private bool arrangingChrome;
        private void LayoutHeader()
        {
            if (arrangingChrome || headingActions == null) return;
            arrangingChrome = true;
            try
            {
                int gap = Scale(4), height = Scale(32), spacing = Scale(6), width = 0;
                foreach (Control c in headingActions.Controls)
                {
                    if (!c.Visible) continue;
                    c.MinimumSize = new Size(Scale(74), height);
                    int buttonWidth = DashboardCommandWidth((ButtonBase)c);
                    c.Margin = Padding.Empty; c.SetBounds(width, 0, buttonWidth, height); width += buttonWidth + spacing;
                }
                width = Math.Max(0, width - spacing);
                bool narrow = header.ClientSize.Width < width + Scale(280);
                headingActions.SetBounds(narrow ? gap : Math.Max(gap, header.ClientSize.Width - width - Scale(12)), narrow ? gap + height + gap : gap, width, height);
                heading.SetBounds(gap, gap, Math.Max(60, narrow ? header.ClientSize.Width - gap * 2 : headingActions.Left - gap * 2), height);
                header.Height = gap * 2 + height + (narrow ? height + gap : 0);
            }
            finally { arrangingChrome = false; }
        }
        private void AlignMonitoring()
        {
            if (arrangingChrome || readNow == null || monitoringFrame == null) return;
            {
                // The bar only needs arranging again when its wrapping, size or scale changes. A new time or a
                // changed message is handled by the table itself; rebuilding would repaint every button.
                int g = Scale(10);
                Control[] r = { refreshLabel, intervalField, readNow, monitor };
                int pref = connection.PreferredSize.Width + r.Sum(c => c is Label ? c.PreferredSize.Width : c.Width) + g * 6;
                bool stack = commands.ClientSize.Width < r.Sum(c => c is Label ? c.PreferredSize.Width : c.Width) + g * 3;
                string key = stack + "|" + (commands.ClientSize.Width < pref) + "|" + DeviceDpi + "|" + commands.ClientSize.Width + "|" + readNow.Text + "|" + monitor.Width;
                if (key == lastAlignKey) return;
                lastAlignKey = key;
            }
            arrangingChrome = true;
            commands.SuspendLayout();
            try
            {
                int gap = Scale(10), row = Scale(38);
                Control[] right = { refreshLabel, intervalField, readNow, monitor };
                int preferred = connection.PreferredSize.Width + right.Sum(c => c is Label ? c.PreferredSize.Width : c.Width) + gap * 6;
                bool wrap = commands.ClientSize.Width < preferred;
                bool stack = wrap && commands.ClientSize.Width < right.Sum(c => c is Label ? c.PreferredSize.Width : c.Width) + gap * 3;
                // Release old row/column assignments before shrinking the table;
                // otherwise AutoGrow retains the former second row on restore.
                foreach (Control c in commands.Controls) { commands.SetColumnSpan(c, 1); commands.SetCellPosition(c, new TableLayoutPanelCellPosition(0, 0)); }
                commands.ColumnStyles.Clear(); commands.RowStyles.Clear(); commands.ColumnCount = stack ? 2 : wrap ? 4 : 6; commands.RowCount = stack ? 3 : wrap ? 2 : 1;
                for (int i = 0; i < commands.ColumnCount; i++) commands.ColumnStyles.Add(new ColumnStyle(!wrap && i == 1 ? SizeType.Percent : SizeType.AutoSize, !wrap && i == 1 ? 100 : 0));
                for (int i = 0; i < commands.RowCount; i++) commands.RowStyles.Add(new RowStyle(SizeType.Absolute, row));
                foreach (Control c in commands.Controls) { commands.SetColumnSpan(c, 1); c.Anchor = AnchorStyles.Left; c.Margin = new Padding(0, 0, gap, 0); }
                commands.SetCellPosition(connection, new TableLayoutPanelCellPosition(0, 0));
                if (wrap) commands.SetColumnSpan(connection, stack ? 2 : 4);
                for (int i = 0; i < right.Length; i++) commands.SetCellPosition(right[i], new TableLayoutPanelCellPosition(stack ? i % 2 : wrap ? i : i + 2, stack ? 1 + i / 2 : wrap ? 1 : 0));
                monitor.Margin = Padding.Empty;
                readNow.MinimumSize = new Size(Scale(74), Scale(32)); readNow.Height = Scale(32);
                readNow.Width = Math.Max(Scale(74), TextRenderer.MeasureText(readNow.Text, readNow.Font).Width + Scale(24));
                monitor.MinimumSize = new Size(Scale(132), Scale(32)); monitor.Size = new Size(Scale(132), Scale(32));
                intervalField.Size = new Size(Scale(70), Scale(32));
                monitoringFrame.Height = row * commands.RowCount + monitoringFrame.Padding.Vertical;
            }
            finally { commands.ResumeLayout(true); arrangingChrome = false; }
        }
        private static bool IsConnected(GXDLMSDevice m) => m.Media?.IsOpen == true && (m.Status & DeviceState.Connected) != 0;
        private GXDLMSDevice[] BoundMeters()
        {
            var ids = Current?.Widgets.SelectMany(w => w.Bindings).Select(b => b.MeterId).ToHashSet() ?? new HashSet<string>();
            return getMeters().Where(m => ids.Contains(m.DashboardId)).ToArray();
        }
        // One button for the meters this dashboard uses: Connect while any is down, Disconnect once all are up.
        private void ConnectBoundMeters()
        {
            if (busy || connectMeters == null || disconnectMeters == null) return;
            var bound = BoundMeters();
            var down = bound.Where(m => !IsConnected(m)).ToArray();
            if (down.Length > 0)
            {
                status.Text = connectMeters(down) ? "Connecting " + down.Length + " meter(s)..." : "Waiting for the current meter operation to finish...";
                return;
            }
            if (bound.Length == 0) return;
            monitor.Checked = false;
            status.Text = disconnectMeters(bound) ? "Disconnecting " + bound.Length + " meter(s)..." : "Waiting for the current meter operation to finish...";
        }
        private void UpdateMonitorText() => monitor.Text = monitor.Checked ? "Pause monitoring" : userPaused ? "Resume monitoring" : "Start monitoring";
        // Start by itself whenever a dashboard with connected meters is shown, unless the user paused it.
        private void AutoStartMonitoring()
        {
            if (monitor.Checked || userPaused || busy || DateTime.UtcNow < autoRetryAfter) return;
            if (Current == null || picker.Visible || editLayout || cardResizeInProgress || dragInProgress) return;
            if (!Current.Widgets.Any(w => w.Bindings.Count > 0)) return;
            if (!BoundMeters().Any(IsConnected)) return;
            monitor.Checked = true;
        }
        private string lastStatusTip, lastAlignKey;
        private void UpdateMonitoring()
        {
            if (connection == null) return;
            var ids = Current?.Widgets.SelectMany(w => w.Bindings).Select(b => b.MeterId).Distinct().ToArray() ?? Array.Empty<string>();
            int connected = getMeters().Count(m => ids.Contains(m.DashboardId) && IsConnected(m));
            string state = monitor.Checked ? "Live" : userPaused ? "Paused" : picker.Visible || editLayout ? "Paused while editing" : connected == 0 ? "Waiting for a connection" : "Starting";
            bool changed = false;
            // Touch a control only when its content changed, so a refresh never repaints or re-lays out the bar.
            void Set(Label label, string text, Color color)
            {
                if (label.Text != text) { label.Text = text; changed = true; }
                if (label.ForeColor != color) label.ForeColor = color;
            }
            var shown = Current?.Widgets.SelectMany(w => w.Bindings).Select(b => b.Key).ToHashSet() ?? new HashSet<string>();
            int failed = samples.Count(p => shown.Contains(p.Key) && p.Value.Error != null);
            string problem = readFailed ? " · read failed" : failed > 0 ? " · " + failed + " failed" : "";
            Set(connection, connected + " of " + ids.Length + " meters connected · " + state + problem,
                problem.Length != 0 ? Color.Firebrick : connected > 0 ? Color.FromArgb(20, 120, 50) : muted);
            var bound = BoundMeters();
            // The button stays in place during a read (clicks are ignored then); hiding it would move the other buttons.
            bool showConnection = connectMeters != null && disconnectMeters != null && bound.Length > 0;
            string connectionText = bound.All(IsConnected) ? "Disconnect" : "Connect";
            if (connectButton != null && (connectButton.Visible != showConnection || connectButton.Text != connectionText))
            { connectButton.Text = connectionText; connectButton.Visible = showConnection; LayoutHeader(); }
            // The latest status message stays available on hover instead of in a second bar.
            if (lastStatusTip != status.Text) { lastStatusTip = status.Text; dashboardTips.SetToolTip(connection, status.Text); }
            if (changed) AlignMonitoring();
        }
        private void RefreshMeterPicker(GXDLMSDevice preferred = null)
        {
            if (meterPicker == null) return;
            var meters = getMeters(); var selected = preferred ?? device;
            meterPicker.Items.Clear(); meterPicker.Items.AddRange(meters.Select(m => new MeterChoice(m)).Cast<object>().ToArray());
            meterPicker.SelectedItem = meterPicker.Items.Cast<MeterChoice>().FirstOrDefault(x => x.Meter == selected) ?? meterPicker.Items.Cast<MeterChoice>().FirstOrDefault();
            selectedObjects.RemoveWhere(o => !meters.Contains(o.Parent?.Tag as GXDLMSDevice)); UpdateAttributes();
        }
        private void RelinkMeter()
        {
            if (busy || Current == null) return;
            var sources = Current.Widgets.SelectMany(w => w.Bindings).GroupBy(b => b.MeterId).Select(g => g.First()).ToArray();
            using var dialog = new DashboardRelinkDialog(Font, sources.Select(b => MeterLabel(b) + " [" + b.MeterId + "]").ToArray(), getMeters().Select(m => new MeterChoice(m)).Cast<object>().ToArray());
            if (dialog.ShowDialog(DialogOwner) != DialogResult.OK) return;
            var from = dialog.From; var to = dialog.To;
            var sourceId = sources[from.SelectedIndex].MeterId; var target = ((MeterChoice)to.SelectedItem).Meter;
            foreach (var w in Current.Widgets)
                if (w.Bindings.Select(b => (b.MeterId == sourceId ? target.DashboardId : b.MeterId) + ":" + b.ClassId + ":" + b.Obis + ":" + b.Attribute).Distinct().Count() != w.Bindings.Count)
                { MessageBox.Show(DialogOwner, "Relinking would create duplicate measurements in a widget. Remove the duplicate first."); return; }
            foreach (var b in Current.Widgets.SelectMany(w => w.Bindings).Where(b => b.MeterId == sourceId)) { b.MeterId = target.DashboardId; b.MeterName = target.Name; }
            samples.Clear(); MarkChanged(); Render();
        }
        private void OpenPicker(GXDLMSObject initial)
        {
            if (busy) return;
            if (Current == null) { NewDashboard(); return; }
            monitor.Checked = false; picker.Visible = true; selectedObjects.Clear(); if (initial != null && UnavailableReason(initial) == null) selectedObjects.Add(initial);
            RefreshMeterPicker(initial?.Parent?.Tag as GXDLMSDevice);
            search.Text = initial?.LogicalName ?? ""; FillPicker(); UpdateAttributes(); ResizeWidgets(); search.Focus();
        }
        private string UnavailableReason(GXDLMSObject o)
        {
            if (o is not IGXDLMSBase supported) return "Object type is not supported by the installed DLMS package.";
            var meter = o.Parent?.Tag as GXDLMSDevice;
            if (meter == null) return "Object is not associated with a loaded meter.";
            var reasons = Enumerable.Range(1, supported.GetNames().Length).Select(i => DashboardAccessPolicy.AttributeError(o, i, meter.Comm.client.CanRead)).ToArray();
            if (reasons.Any(reason => reason == null)) return null;
            return reasons.All(reason => reason == "Access denied") ? "No readable attributes under this meter's current access rights." : string.Join(" ", reasons.Distinct());
        }
        private void FillPicker()
        {
            if (objectPicker == null || search == null) return;
            fillingPicker = true; objectPicker.BeginUpdate();
            try
            {
                objectPicker.Items.Clear();
                var objects = device?.Objects.Cast<GXDLMSObject>().ToArray() ?? Array.Empty<GXDLMSObject>();
                int unavailable = 0;
                foreach (var o in objects.Where(o => ((o.Description ?? "") + " " + o.ObjectType + " " + o.LogicalName).Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
                {
                    string reason = UnavailableReason(o);
                    if (reason != null) { unavailable++; selectedObjects.Remove(o); }
                    string availability = reason ?? (o is GXDLMSProfileGeneric ? "Available; profile buffer excluded." : "Available");
                    var item = new ListViewItem(ObjectLabel(o))
                    { Tag = o, Checked = selectedObjects.Contains(o), ToolTipText = availability, ForeColor = reason == null ? ink : SystemColors.GrayText };
                    item.SubItems.Add(o.LogicalName); item.SubItems.Add(reason == null ? "Ready" : "Unavailable"); objectPicker.Items.Add(item);
                }
                pickerSummary.Text = objectPicker.Items.Count + " of " + objects.Length + " objects · " + unavailable + " unavailable";
                pickerReason.Text = device == null ? "Load a meter under Devices to select its objects." : objects.Length == 0 ? "This meter has no loaded objects. Refresh its object list under Devices." : objectPicker.Items.Count == 0 ? "No objects match your search. Clear the search to show all objects." : "Select an object to see its availability.";
            }
            finally { objectPicker.EndUpdate(); fillingPicker = false; }
            UpdateAttributes();
        }
        private sealed record AttributeChoice(int Index, string Name) { public override string ToString() => Name; }
        private void UpdateAttributes()
        {
            int previous = (attribute.SelectedItem as AttributeChoice)?.Index ?? 2;
            attribute.Items.Clear();
            if (selectedObjects.Count > 0)
            {
                var names = ((IGXDLMSBase)selectedObjects.First()).GetNames();
                for (int i = 1; i <= names.Length; i++) if (selectedObjects.All(o => i <= ((IGXDLMSBase)o).GetNames().Length && string.Equals(((IGXDLMSBase)o).GetNames()[i - 1], names[i - 1], StringComparison.OrdinalIgnoreCase) && (o.Parent?.Tag as GXDLMSDevice) is GXDLMSDevice owner && DashboardAccessPolicy.AttributeError(o, i, owner.Comm.client.CanRead) == null)) attribute.Items.Add(new AttributeChoice(i, names[i - 1]));
                attribute.SelectedItem = attribute.Items.Cast<AttributeChoice>().FirstOrDefault(x => x.Index == previous) ?? attribute.Items.Cast<AttributeChoice>().FirstOrDefault();
            }
            selectedCount.Text = selectedObjects.Count + " selected";
            if (selectedObjects.Count > 0 && attribute.Items.Count == 0)
                pickerReason.Text = "These objects have no common readable dashboard attribute. Add them separately.";
            UpdatePickerAction();
        }
        private void AddPicked()
        {
            if (busy || Current == null) return;
            if (selectedObjects.Count == 0 || selectedObjects.Count > 32 || attribute.SelectedItem is not AttributeChoice a || string.IsNullOrWhiteSpace(widgetTitle.Text)) throw new InvalidOperationException("Select 1–32 objects, a common readable attribute and a title.");
            if (selectedObjects.Any(o => UnavailableReason(o) != null)) throw new InvalidOperationException("A selected object is no longer available. Reopen Add OBIS to review its access rights.");
            if (Current.Widgets.Count >= 30) throw new InvalidOperationException("Maximum 30 widgets.");
            Current.Widgets.Add(new DashboardWidget { Title = widgetTitle.Text.Trim(), Kind = new[] { "Values", "Trend", "Table" }[display.SelectedIndex], Bindings = selectedObjects.Select(o => new DashboardBinding { MeterId = ((GXDLMSDevice)o.Parent.Tag).DashboardId, MeterName = ((GXDLMSDevice)o.Parent.Tag).Name, ClassId = (int)o.ObjectType, Obis = o.LogicalName, Attribute = a.Index, Label = ObjectLabel(o) }).ToList() });
            MarkChanged(); Render();
        }
        private void DiscardCards()
        {
            refreshViews.Clear(); layoutCards.Clear(); cardItems.Clear();
            var old = widgets.Controls.Cast<Control>().ToArray(); widgets.Controls.Clear(); foreach (var c in old) c.Dispose();
        }
        private void RenderWorkspace()
        {
            if (!DashboardTypographyMatchesOwner()) RestoreDashboardTypography();
            if (heading == null) return;
            PruneUnusedSamples();
            heading.Text = Current?.Name ?? "Dashboards"; dashboardTips.SetToolTip(heading, heading.Text);
            DiscardCards();
            if (Current == null || Current.Widgets.Count == 0)
            {
                widgets.Controls.Add(new Label { Text = Current == null
                    ? "No dashboards yet.\nRight-click Dashboards in the tree and choose New dashboard."
                    : "Create your dashboard with Add OBIS.\nChoose value tiles, trend charts or tables for your meter's readable attributes.", ForeColor = muted, AutoSize = true, Location = new Point(12, 20) }); return;
            }
            foreach (var item in OrderedCards())
            {
                var w = item.Widget;
                if (w.Kind == "Values")
                {
                    var tile = BuildTile(w, item.Binding); tile.Tag = "Value"; widgets.Controls.Add(tile); layoutCards.Add(tile); AttachCard(tile, item);
                    continue;
                }
                var box = Card(); box.Padding = new Padding(Scale(12)); box.Size = new Size(Scale(600), Scale(300)); box.Tag = w.Kind;
                if (w.Kind == "Trend") BuildChart(box, w); else BuildTable(box, w);
                var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, Scale(38)), Padding = new Padding(0, 0, 28, 4), WrapContents = true };
                var chartTitle = new Label { Text = w.Title, Font = strongFont, AutoSize = true, MaximumSize = new Size(Scale(300), 0), Padding = new Padding(3, 7, 10, 0) }; dashboardTips.SetToolTip(chartTitle, w.Title); bar.Controls.Add(chartTitle);
                if (w.Kind == "Trend") foreach (var choice in new[] { ("15 min", 15), ("1 h", 60), ("24 h", 1440) }) bar.Controls.Add(Command(choice.Item1, () => { chartMinutes = choice.Item2; Render(); }));
                box.Controls.Add(bar); widgets.Controls.Add(box); layoutCards.Add(box); AttachCard(box, item);
            }
            // Controls created on another-DPI monitor are scaled when parented.
            // Match application roles only after the complete card tree is attached.
            RestoreDashboardTypography(); ResizeWidgets(); RefreshViews();
            foreach (Control card in widgets.Controls) HookDpiFreeze(card);
        }
        // A value tile is one control that draws its own text. The six labels it used to be built from were six
        // windows per tile, and every window costs time when the application moves to a monitor with another DPI.
        private sealed class TileSurface : RoundedSurface
        {
            private readonly DashboardForm owner;
            private string title = "", meter = "", value = "", obis = "", last = "", badge = "";
            private Color badgeColor;
            internal TileSurface(DashboardForm owner) { this.owner = owner; BackColor = Color.White; Padding = new Padding(1); }
            internal void SetContent(string newTitle, string newMeter, string newValue, string newObis, string newLast, string newBadge, Color newBadgeColor)
            {
                if (title == newTitle && meter == newMeter && value == newValue && obis == newObis && last == newLast && badge == newBadge && badgeColor == newBadgeColor) return;
                title = newTitle; meter = newMeter; value = newValue; obis = newObis; last = newLast; badge = newBadge; badgeColor = newBadgeColor;
                Invalidate();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
                int pad = owner.Scale(14), inner = Math.Max(0, Width - pad * 2), badgeWidth = owner.Scale(62);
                var g = e.Graphics;
                TextRenderer.DrawText(g, title, owner.strongFont, new Rectangle(pad, pad, Math.Max(10, inner - badgeWidth - owner.Scale(28)), owner.Scale(22)), ink, flags);
                var pill = new Rectangle(Width - pad - badgeWidth - owner.Scale(22), pad, badgeWidth, owner.Scale(20));
                using (var path = Outline(new RectangleF(pill.X, pill.Y, Math.Max(1, pill.Width - 1), Math.Max(1, pill.Height - 1)), pill.Height / 2f))
                using (var fill = new SolidBrush(Color.FromArgb(246, 248, 251)))
                {
                    var smoothing = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; g.FillPath(fill, path); g.SmoothingMode = smoothing;
                }
                TextRenderer.DrawText(g, badge, owner.Font, pill, badgeColor, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(g, meter, owner.Font, new Rectangle(pad, pad + owner.Scale(25), inner, owner.Scale(20)), muted, flags);
                TextRenderer.DrawText(g, value, owner.valueFont, new Rectangle(pad, pad + owner.Scale(48), inner, Math.Max(owner.Scale(30), Height - owner.Scale(124))), ink, flags);
                TextRenderer.DrawText(g, obis, owner.Font, new Rectangle(pad, Height - owner.Scale(61), inner, owner.Scale(21)), muted, flags);
                using (var pen = new Pen(border)) g.DrawLine(pen, pad, Height - owner.Scale(38), Width - pad, Height - owner.Scale(38));
                TextRenderer.DrawText(g, last, owner.Font, new Rectangle(pad, Height - owner.Scale(30), Math.Max(0, inner - owner.Scale(12)), owner.Scale(20)), muted, flags);
            }
        }
        private Panel BuildTile(DashboardWidget w, DashboardBinding b)
        {
            var tile = new TileSurface(this);
            string tip = null;
            refreshViews.Add(() =>
            {
                var sample = State(b.Key); string quality = Quality(b);
                string meter = MeterLabel(b), valueText = sample.Text + (string.IsNullOrEmpty(sample.Unit) ? "" : " " + sample.Unit);
                string badge = quality == "Disconnected" ? "Offline" : quality.StartsWith("Meter missing") || quality.StartsWith("Object missing") ? "Missing" : quality == "Fresh" ? "Live" : quality == "Not read" ? "Not read" : quality == "Stale" ? "Stale" : "Error";
                tile.SetContent(b.Label, meter, valueText, b.Obis, "Last success: " + (sample.Success?.ToLocalTime().ToString("HH:mm:ss") ?? "never"), badge,
                    quality == "Fresh" ? Color.FromArgb(20, 120, 50) : sample.Error != null ? Color.Firebrick : muted);
                // One tip for the whole tile, changed only when its text changes.
                string newTip = b.Label + "\n" + meter + " | " + b.Obis + "\n" + valueText + "\n" + quality + (sample.Error == null ? "" : "\n" + sample.Error);
                if (tip != newTip) { tip = newTip; dashboardTips.SetToolTip(tile, newTip); }
            });
            return tile;
        }
        private void LayoutWorkspace()
        {
            if (sizing || widgets.IsDisposed) return; sizing = true;

            try
            {
                int scrollY = -widgets.AutoScrollPosition.Y; widgets.AutoScroll = false; widgets.AutoScrollMinSize = Size.Empty;
                int gap = Scale(14), width = Math.Max(120, widgets.ClientSize.Width - SystemInformation.VerticalScrollBarWidth), y = 0;
                int columns = width >= Scale(540) ? 3 : width >= Scale(390) ? 2 : 1;
                int tileHeight = Scale(176), tileWidth = (width - gap * (columns - 1)) / columns;
                if (cardItems.Values.Any(item => CardSize(item) != null))
                {
                    int x = 0, rowHeight = 0;
                    foreach (var card in layoutCards)
                    {
                        var size = CardSize(cardItems[card]);
                        bool value = (string)card.Tag == "Value";
                        int w = Math.Min(width, size == null ? (value ? tileWidth : width) : Scale(size.Width));
                        int h = size == null ? (value ? tileHeight : Scale((string)card.Tag == "Trend" ? 300 : 210)) : Scale(size.Height);
                        if (x > 0 && x + w > width) { x = 0; y += rowHeight + gap; rowHeight = 0; }
                        card.SetBounds(x, y, w, h); x += w + gap; rowHeight = Math.Max(rowHeight, h);
                    }
                    y += rowHeight + gap;
                }
                else for (int i = 0; i < layoutCards.Count;)
                {
                    var c = layoutCards[i]; string kind = (string)c.Tag;
                    if (kind == "Value") { int count = 0; while (i < layoutCards.Count && (string)layoutCards[i].Tag == "Value" && count < columns) { layoutCards[i++].SetBounds(count * (tileWidth + gap), y, tileWidth, tileHeight); count++; } y += tileHeight + gap; }
                    else if (kind == "Trend")
                    {
                        c.SetBounds(0, y, width, Scale(255)); y += c.Height + gap; i++;
                    }
                    else { c.SetBounds(0, y, width, Scale(210)); y += c.Height + gap; i++; }
                }
                widgets.AutoScrollMinSize = new Size(0, y); widgets.AutoScroll = true; widgets.AutoScrollPosition = new Point(0, scrollY);
            }
            finally
            {

                // Repaint moved children and the exposed background in one queued paint pass.
                widgets.Invalidate(true);
                sizing = false;
            }
        }
    }
}

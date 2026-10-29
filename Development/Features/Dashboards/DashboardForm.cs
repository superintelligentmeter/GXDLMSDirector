// Intelmeter contributors, 2026-10-17. Meter dashboard workspace. GPL-2.0-only.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Gurux.DLMS.Objects;

namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm : Form
    {
        private GXDLMSDevice device;
        private readonly Func<GXDLMSDevice[]> getMeters;
        private readonly string path;
        private DashboardDocument document = new();
        private readonly ComboBox dashboards = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        private readonly NumericUpDown interval = new() { Minimum = 2, Maximum = 3600, Value = 5, Width = 65 };
        private readonly CheckBox monitor = new DashboardMonitor() { Text = "Monitor", AutoSize = true };
        private readonly Label status = new() { AutoSize = true, Text = "Paused - select attributes, then Read now or Monitor." };
        private readonly Panel widgets = new DashboardSurface() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        private readonly TableLayoutPanel commands = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
        private readonly Dictionary<string, Sample> samples = new();
        private readonly List<Action> refreshViews = new();
        private DateTime nextRead;
        private bool busy, dirty, loadFailed, sizing, initialized;
        private bool workspaceActive = true;
        private readonly Func<bool> beginRead;
        private readonly Action endRead;
        internal bool IsReading => busy;
        internal GXDLMSDevice Meter => device;
        // Native dialogs must belong to the real top-level window, not the embedded Form.
        internal Form DialogOwner => TopLevelControl as Form ?? this;
        private Font valueFont;
        private volatile bool closing, stopRead;
        private DashboardDefinition Current => dashboards.SelectedItem as DashboardDefinition;
        private sealed class Sample
        {
            public string Text = "-", Unit = "", Error;
            public DateTime? Success; public bool Numeric;
            public readonly List<(DateTime Time, double Value)> Points = new();
        }
        public DashboardForm(GXDLMSDevice device, GXDLMSObject initial = null, Func<bool> beginRead = null, Action endRead = null, Func<GXDLMSDevice[]> getMeters = null, string storagePath = null)
        {
            this.device = device; this.getMeters = getMeters ?? (() => device == null ? Array.Empty<GXDLMSDevice>() : new[] { device }); this.beginRead = beginRead; this.endRead = endRead;
            Text = "Dashboards";
            Font = Gurux.DLMS.UI.ApplicationTypography.BodyFont;
            valueFont = new Font(Font.FontFamily, 19f, FontStyle.Bold);
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1160, 800); MinimumSize = new Size(760, 500);
            StartPosition = FormStartPosition.CenterParent; BackColor = Color.White;
            path = storagePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gurux", "Dashboards", "workspace-v2.json");
            BuildWorkspace();
            autoSaveTimer.Tick += (_, _) => { if (!cardResizeInProgress) FlushAutoSave(); };
            dashboards.SelectedIndexChanged += (_, _) => { monitor.Checked = false; if (Current != null) interval.Value = Current.RefreshSeconds; Render(); };
            interval.ValueChanged += (_, _) => { if (Current != null && Current.RefreshSeconds != (int)interval.Value) { Current.RefreshSeconds = (int)interval.Value; MarkChanged(); } };
            monitor.CheckedChanged += (_, _) => { nextRead = DateTime.MinValue; if (!monitor.Checked && busy) stopRead = true; };
            widgets.SizeChanged += (_, _) => ResizeWidgets();
            timer.Tick += async (_, _) => { if (!workspaceActive || !Visible || busy) return; RefreshViews(); if (monitor.Checked && DateTime.UtcNow >= nextRead) await Poll(); };
            FormClosing += (_, e) => { if (!PrepareToClose()) e.Cancel = true; };
            Shown += (_, _) => InitializeWorkspace(initial);
        }
        internal void InitializeWorkspace(GXDLMSObject initial = null)
        {
            if (!initialized)
            {
                initialized = true;
                try { if (File.Exists(path)) document = DashboardStore.Load(path); }
                catch (Exception ex) { loadFailed = true; MessageBox.Show(DialogOwner, "Saved dashboards could not be loaded. The file has not been changed.\n" + ex.Message); }
                MigrateLegacyDashboards();
                if (document.Dashboards.Count == 0) document.Dashboards.Add(new DashboardDefinition());
                BindDashboards(document.Dashboards[0]); if (workspaceActive) timer.Start();
            }
            if (initial != null) AddObjects(initial);
        }
        internal void SetWorkspaceActive(bool active)
        {
            workspaceActive = active;
            if (initialized) { if (active) timer.Start(); else timer.Stop(); }
            if (!active)
            {
                monitor.Checked = false; stopRead = true;
                status.Text = "Paused while viewing another workspace. Enable Monitor to resume.";
            }
        }
        internal bool PrepareToClose()
        {
            monitor.Checked = false; stopRead = true;
            if (busy) { status.Text = "Finishing the current meter read. Please retry closing when it completes."; return false; }
            if (!dirty || FlushAutoSave()) return true;
            using var prompt = new DashboardSavePrompt();
            var result = prompt.ShowDialog(DialogOwner);
            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.Yes)
            {
                return FlushAutoSave();
            }
            return true;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { closing = true; stopRead = true; timer.Dispose(); autoSaveTimer.Dispose(); dashboardTips.Dispose(); valueFont.Dispose(); headingFont?.Dispose(); strongFont?.Dispose(); }
            base.Dispose(disposing);
        }

        private string Ask(string title, string value)
        {
            using var dialog = new DashboardNameDialog(Font, title, value);
            return dialog.ShowDialog(DialogOwner) == DialogResult.OK ? dialog.Value : null;
        }
        private void BindDashboards(DashboardDefinition selected)
        {
            dashboards.Items.Clear(); dashboards.Items.AddRange(document.Dashboards.Cast<object>().ToArray());
            dashboards.SelectedItem = selected; Render();
        }
        private void Save()
        {
            if (loadFailed) throw new InvalidOperationException("The existing dashboard file could not be loaded. Export your new dashboards to another file before repairing it; it will not be overwritten.");
            DashboardStore.Save(path, document); dirty = false; autoSaveTimer.Stop(); autoSaveFailed = false;
            UpdateSaveIndicator("Saved", false);
        }

        private void Import()
        {
            using var dlg = new OpenFileDialog { Filter = "Dashboard JSON|*.json" };
            if (dlg.ShowDialog(DialogOwner) != DialogResult.OK) return;
            var imported = DashboardStore.Load(dlg.FileName);
            if (imported.Version == 1)
            {
                if (device == null) throw new InvalidDataException("Select a meter in Add OBIS before importing a legacy single-meter dashboard.");
                foreach (var b in imported.Dashboards.SelectMany(d => d.Widgets).SelectMany(w => w.Bindings)) { b.MeterId = device.DashboardId; b.MeterName = device.Name; }
            }
            if (document.Dashboards.Count + imported.Dashboards.Count > 50) throw new InvalidDataException("Maximum 50 dashboards.");
            document.Dashboards.AddRange(imported.Dashboards); BindDashboards(imported.Dashboards.FirstOrDefault()); MarkChanged();
        }
        private void Export()
        {
            if (Current == null) return;
            using var dlg = new SaveFileDialog { Filter = "Dashboard JSON|*.json", FileName = "dashboard.json" };
            if (dlg.ShowDialog(DialogOwner) == DialogResult.OK) DashboardStore.Save(dlg.FileName, new DashboardDocument { Dashboards = new() { Current } });
        }
        private DashboardMeterCatalog viewCatalog;
        private GXDLMSDevice ResolveMeter(DashboardBinding b) => (viewCatalog ?? new DashboardMeterCatalog(getMeters())).Meter(b.MeterId);
        private string MeterLabel(DashboardBinding b) => ResolveMeter(b)?.Name ?? (string.IsNullOrEmpty(b.MeterName) ? "Missing meter" : b.MeterName);
        private GXDLMSObject Resolve(DashboardBinding b) => (viewCatalog ?? new DashboardMeterCatalog(getMeters())).Resolve(b);
        internal void RefreshMeters()
        {
            if (busy) return;
            RefreshMeterPicker(); MigrateLegacyDashboards(); Render();
        }
        private void MigrateLegacyDashboards()
        {
            if (loadFailed) return;
            foreach (var meter in getMeters())
            {
                string identity = meter.Name + "|" + meter.MediaType + "|" + meter.MediaSettings + "|" + meter.ClientAddress + "|" + meter.LogicalAddress + "|" + meter.PhysicalAddress;
                string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                string legacy = Path.Combine(Path.GetDirectoryName(path), key + ".json");
                if (document.MigratedSources.Contains(key) || !File.Exists(legacy)) continue;
                try
                {
                    var old = DashboardStore.Load(legacy);
                    if (document.Dashboards.Count + old.Dashboards.Count > 50) { status.Text = "Legacy dashboards exceed the 50-dashboard limit. Export or remove some before migrating."; continue; }
                    foreach (var d in old.Dashboards)
                    {
                        foreach (var b in d.Widgets.SelectMany(w => w.Bindings)) { b.MeterId = meter.DashboardId; b.MeterName = meter.Name; }
                        d.Name = (meter.Name + " / " + d.Name); if (d.Name.Length > 100) d.Name = d.Name.Substring(0, 100);
                        document.Dashboards.Add(d);
                    }
                    document.Version = 2; document.MigratedSources.Add(key); MarkChanged();
                    BindDashboards(Current ?? document.Dashboards.FirstOrDefault()); DefinitionsChanged?.Invoke();
                }
                catch (Exception ex) { status.Text = "Legacy dashboard preserved; migration failed: " + ex.Message; }
            }
        }
        private void AddObjects(GXDLMSObject initial) => OpenPicker(initial);
        private Sample State(string key) { if (!samples.TryGetValue(key, out var s)) samples[key] = s = new Sample(); return s; }
        private string Quality(DashboardBinding b)
        {
            var s = State(b.Key);
            var owner = ResolveMeter(b);
            if (owner == null) return "Meter missing - use More > Relink meter";
            if (Resolve(b) == null) return "Object missing on this meter";
            if (owner.Media?.IsOpen != true || (owner.Status & DeviceState.Connected) == 0) return "Disconnected";
            if (s.Error != null) return s.Error;
            if (!s.Success.HasValue) return "Not read";
            return (DateTime.UtcNow - s.Success.Value).TotalSeconds > Math.Max(10, (Current?.RefreshSeconds ?? 5) * 2) ? "Stale" : "Fresh";
        }
        private void Render() => RenderWorkspace();
        private void ResizeWidgets() => LayoutWorkspace();
        private void RefreshViews()
        {
            // A short-lived index keeps object identity current between refreshes, while
            // sharing lookups across every tile, chart and table in this refresh.
            viewCatalog = new DashboardMeterCatalog(getMeters());
            try { UpdateMonitoring(); foreach (var update in refreshViews) update(); }
            finally { viewCatalog = null; }
        }
        private void PruneUnusedSamples()
        {
            var used = document.Dashboards.SelectMany(d => d.Widgets).SelectMany(w => w.Bindings).Select(b => b.Key).ToHashSet();
            foreach (var key in samples.Keys.Where(key => !used.Contains(key)).ToArray()) samples.Remove(key);
        }
    }
}

// Intelmeter contributors, 2026-10-17. Dashboard module. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Gurux.DLMS.Objects;
namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm
    {
        private async Task Poll()
        {
            if (busy || closing || !workspaceActive || IsDisposed || Current == null) return;
            nextRead = DateTime.UtcNow.AddSeconds(Current.RefreshSeconds);
            var requests = Current.Widgets.SelectMany(w => w.Bindings).GroupBy(b => b.Key).Select(g => g.First()).ToArray();
            if (requests.Length == 0) return;
            if (beginRead != null && !beginRead()) { status.Text = "Waiting for the current meter operation to finish..."; return; }
            stopRead = false; busy = true; foreach (Control c in commands.Controls) if (c != monitor) c.Enabled = false; widgets.Enabled = false; picker.Enabled = false; header.Enabled = false; status.Text = "Reading " + requests.Length + " unique attributes...";
            try
            {
                var meters = getMeters().ToDictionary(m => m.DashboardId);
                var results = await DashboardReader.ReadMeters(requests, (id, batch) =>
                {
                    if (!meters.TryGetValue(id, out var meter)) throw new InvalidOperationException("Meter missing");
                    return ReadMeter(meter, batch);
                }, () => closing || stopRead);
                if (!meters.Values.Any(m => m.Media?.IsOpen == true)) monitor.Checked = false;
                if (IsDisposed) return;
                ApplyReadings(results);
                status.Text = (stopRead ? "Read paused " : "Read completed ") + DateTime.Now.ToString("HH:mm:ss") + " | " + results.Count(r => r.Error != null) + " failed | trends are session-only" + (results.Any(r => r.Error == "Disconnected") ? " | Disconnected meters skipped" : "");
            }
            catch (Exception ex) { monitor.Checked = false; status.Text = "Read failed: " + ex.Message; }
            finally
            {
                busy = false; endRead?.Invoke();
                if (!IsDisposed)
                {
                    foreach (Control c in commands.Controls) c.Enabled = true;
                    widgets.Enabled = true; picker.Enabled = true; header.Enabled = true; nextRead = DateTime.UtcNow.AddSeconds(Current?.RefreshSeconds ?? 5); RefreshViews();
                }
            }
        }
        private void ApplyReadings(IEnumerable<DashboardReading> results)
        {
                foreach (var r in results)
                {
                    var s = State(r.Key); s.Error = r.Error;
                    if (r.Error == null) { if (s.Unit != r.Unit) s.Points.Clear(); s.Text = r.Text; s.Unit = r.Unit; s.Numeric = r.Number.HasValue; s.Success = r.Time; }
                    s.Points.Add((r.Time, r.Error == null && r.Number.HasValue ? r.Number.Value : double.NaN));
                    if (s.Points.Count > 1000) s.Points.RemoveRange(0, s.Points.Count - 1000);
                }
        }
        private List<DashboardReading> ReadMeter(GXDLMSDevice device, DashboardBinding[] bindings)
        {
            if (device.Media?.IsOpen != true || (device.Status & DeviceState.Connected) == 0) throw new InvalidOperationException("Disconnected");
            bool resumeKeepAlive = device.IsKeepAliveRunning;
            device.KeepAliveStop();
            try
            {
                // Also wait for a keep-alive that was already in flight when its timer stopped.
                lock (device.Media.Synchronous)
                {
                    var catalog = new DashboardMeterCatalog(new[] { device });
                    return DashboardReader.Read(bindings, catalog.Resolve, device.Comm.client.CanRead,
                        (o, index) => device.Comm.ReadValue(o, index), () => closing || stopRead);
                }
            }
            finally { if (resumeKeepAlive && device.Media?.IsOpen == true) device.KeepAliveStart(); }
        }
    }
}

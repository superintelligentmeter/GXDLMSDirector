// Modified for the Intelmeter fork, 2026-10-16: .NET 10 migration, UI modernization and compatibility fixes. GPL-2.0-only; original notices retained.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

namespace GXDLMSDirector
{
    /// <summary>Original 24-unit vector glyphs. SVG exports and toolbar bitmaps share the same geometry.</summary>
    public static class ModernIcons
    {
        private sealed record Shape(string Kind, bool Accent, float[] Values);
        private static Shape P(bool accent, params float[] points) => new("polyline", accent, points);
        private static Shape R(bool accent, float x, float y, float width, float height) => new("rect", accent, new[] { x, y, width, height });
        private static Shape C(bool accent, float x, float y, float radius) => new("circle", accent, new[] { x, y, radius });
        private static Shape D(bool accent, float x, float y, float radius) => new("dot", accent, new[] { x, y, radius });
        private static readonly Dictionary<string, Shape[]> Glyphs = new()
        {
            ["new"] = new[] { P(false, 13,21,4,21,4,3,14,3,19,8,19,11), P(false,14,3,14,8,19,8), P(true,18,14,18,22), P(true,14,18,22,18) },
            ["open"] = new[] { P(false,3,10,3,5,9,5,12,8,20,8), P(true,3,10,22,10,19,20,3,20,3,10) },
            ["save"] = new[] { P(false,4,3,17,3,21,7,21,21,3,21,3,3,4,3), R(true,7,3,9,6), R(false,7,14,10,7) },
            ["connect"] = new[] { P(false,9,3,9,7), P(false,15,3,15,7), P(true,6,7,18,7,18,10,15,14,9,14,6,10,6,7), P(false,12,14,12,18,8,22) },
            ["disconnect"] = new[] { P(false,9,3,9,7), P(false,15,3,15,7), P(false,6,7,18,7,18,10,15,14,9,14,6,10,6,7), P(false,12,14,12,21), P(true,3,21,21,3) },
            ["read"] = new[] { R(false,3,3,10,18), P(false,6,7,10,7), P(true,11,14,22,14), P(true,18,10,22,14,18,18) },
            ["write"] = new[] { R(false,3,3,10,18), P(false,6,7,10,7), P(true,22,14,11,14), P(true,15,10,11,14,15,18) },
            ["notifications"] = new[] { P(false,5,17,7,14,7,9,9,5,15,5,17,9,17,14,19,17,5,17), P(true,10,21,14,21), P(false,12,3,12,5), D(true,19,5,2) },
            ["properties"] = new[] { P(false,4,6,20,6), P(false,4,12,20,12), P(false,4,18,20,18), R(true,7,4,3,4), R(true,15,10,3,4), R(true,8,16,3,4) },
            ["force-read"] = new[] { C(false,12,12,9), P(true,13,5,8,13,12,13,11,19,17,10,13,10,13,5) },
            ["time-zone"] = new[] { C(false,12,12,9), P(true,12,6,12,12,16,14), P(false,3,12,5,12), P(false,19,12,21,12) },
            ["delete"] = new[] { P(false,4,6,20,6), P(false,9,6,9,3,15,3,15,6), P(false,6,6,7,21,17,21,18,6), P(true,10,10,10,17), P(true,14,10,14,17) },
            ["help"] = new[] { C(true,12,12,9), P(false,9,8,10,6,14,6,16,8,16,10,12,13,12,14), D(false,12,18,0.8f) },
            ["add-device"] = new[] { R(false,3,3,12,18), R(false,6,6,6,4), P(true,18,13,18,21), P(true,14,17,22,17) },
            ["cancel"] = new[] { C(false,12,12,9), P(true,8,8,16,16), P(true,16,8,8,16) },
            ["refresh"] = new[] { P(false,4,9,6,5,10,3,15,3,19,6,21,10), P(true,21,4,21,10,15,10), P(false,20,15,18,19,14,21,9,21,5,18,3,14), P(true,3,20,3,14,9,14) }
        };

        private static readonly Dictionary<string, string> Commands = new()
        {
            ["NewBtn"] = "new", ["NewMnu"] = "new", ["OpenBtn"] = "open", ["OpenMnu"] = "open",
            ["SaveBtn"] = "save", ["SaveMnu"] = "save", ["ConnectBtn"] = "connect", ["ConnectMnu"] = "connect",
            ["DisconnectMnu"] = "disconnect", ["ReadBtn"] = "read", ["ReadMnu"] = "read",
            ["WriteBtn"] = "write", ["WriteMnu"] = "write", ["NotificationsBtn"] = "notifications",
            ["OptionsBtn"] = "properties", ["PropertiesMnu"] = "properties", ["ForceRefreshBtn"] = "force-read",
            ["UseMeterTimeZoneBtn"] = "time-zone", ["DeleteBtn"] = "delete", ["DeleteMnu"] = "delete",
            ["toolStripButton1"] = "help", ["ContentsMnu"] = "help", ["AddDeviceMnu"] = "add-device",
            ["CancelBtn"] = "cancel", ["RefreshMnu"] = "refresh"
        };

        public static Bitmap Render(string name, int pixels, bool highContrast = false)
        {
            var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(pixels / 24f, pixels / 24f);
            foreach (var shape in Glyphs[name])
            {
                var color = highContrast ? SystemColors.ControlText : shape.Accent ? Color.FromArgb(48, 86, 211) : Color.FromArgb(51, 65, 85);
                using var pen = new Pen(color, 1.65f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                var v = shape.Values;
                switch (shape.Kind)
                {
                    case "polyline":
                        var points = Enumerable.Range(0, v.Length / 2).Select(i => new PointF(v[i * 2], v[i * 2 + 1])).ToArray();
                        g.DrawLines(pen, points);
                        break;
                    case "rect":
                        using (var path = new GraphicsPath())
                        {
                            float d = Math.Min(2, Math.Min(v[2], v[3]));
                            path.AddArc(v[0], v[1], d, d, 180, 90);
                            path.AddArc(v[0] + v[2] - d, v[1], d, d, 270, 90);
                            path.AddArc(v[0] + v[2] - d, v[1] + v[3] - d, d, d, 0, 90);
                            path.AddArc(v[0], v[1] + v[3] - d, d, d, 90, 90);
                            path.CloseFigure();
                            g.DrawPath(pen, path);
                        }
                        break;
                    case "circle": g.DrawEllipse(pen, v[0] - v[2], v[1] - v[2], 2 * v[2], 2 * v[2]); break;
                    case "dot":
                        using (var brush = new SolidBrush(color)) g.FillEllipse(brush, v[0] - v[2], v[1] - v[2], 2 * v[2], 2 * v[2]);
                        break;
                }
            }
            return bitmap;
        }

        private static readonly ConditionalWeakTable<ToolStrip, Action> Refreshers = new();

        public static void Apply(ToolStrip strip)
        {
            if (strip.IsDisposed || strip.Disposing) return;
            if (Refreshers.TryGetValue(strip, out var refresh))
            {
                refresh();
                return;
            }
            var owned = new Dictionary<ToolStripItem, Bitmap>();
            IEnumerable<ToolStripItem> Items(ToolStripItemCollection items)
            {
                foreach (ToolStripItem item in items)
                {
                    yield return item;
                    if (item is ToolStripDropDownItem drop)
                        foreach (var child in Items(drop.DropDownItems)) yield return child;
                }
            }
            void RefreshImages()
            {
                if (strip.IsDisposed || strip.Disposing) return;
                int logicalSize = strip is MenuStrip || strip is ToolStripDropDown ? 16 : 18;
                int size = (int)Math.Round(logicalSize * strip.DeviceDpi / 96f);
                strip.ImageScalingSize = new Size(size, size);
                foreach (var item in Items(strip.Items))
                {
                    if (!Commands.TryGetValue(item.Name, out string glyph)) continue;
                    if (item.Name == "ConnectBtn" && item is ToolStripButton connection)
                    {
                        glyph = connection.Checked ? "disconnect" : "connect";
                        connection.ToolTipText = connection.Checked ? "Disconnect from the meter" : "Connect to the meter";
                        connection.AccessibleName = connection.Checked ? "Disconnect" : "Connect";
                    }
                    // Studio Outline also renders with system ink in high contrast.
                    var image = StudioIcons.Render(glyph, size);
                    item.Image = image;
                    item.ImageScaling = ToolStripItemImageScaling.None;
                    item.ImageTransparentColor = Color.Empty;
                    if (owned.TryGetValue(item, out var old)) old.Dispose();
                    owned[item] = image;
                    if (item is ToolStripButton)
                    {
                        item.AutoSize = true;
                        item.Padding = new Padding((int)(5 * strip.DeviceDpi / 96f));
                        item.Margin = new Padding(1, 2, 1, 2);
                    }
                    if (string.IsNullOrEmpty(item.AccessibleName))
                        item.AccessibleName = string.IsNullOrEmpty(item.ToolTipText) ? item.Text : item.ToolTipText;
                }
            }
            void RefreshEvent(object sender, EventArgs e) => RefreshImages();
            void ConnectionChanged(object sender, EventArgs e)
            {
                if (sender is not ToolStripButton connection || strip.IsDisposed || strip.Disposing) return;
                // A state change must not reset the DPI-scaled metrics of the whole toolbar.
                // Keep the current image canvas and only replace this button's glyph.
                int size = connection.Image?.Width ?? strip.ImageScalingSize.Width;
                var image = StudioIcons.Render(connection.Checked ? "disconnect" : "connect", size);
                connection.Image = image;
                connection.ToolTipText = connection.Checked ? "Disconnect from the meter" : "Connect to the meter";
                connection.AccessibleName = connection.Checked ? "Disconnect" : "Connect";
                if (owned.TryGetValue(connection, out var old)) old.Dispose();
                owned[connection] = image;
            }
            void DisposeImages(object sender, EventArgs e)
            {
                strip.DpiChangedAfterParent -= RefreshEvent;
                strip.SystemColorsChanged -= RefreshEvent;
                strip.Disposed -= DisposeImages;
                foreach (var item in strip.Items.OfType<ToolStripButton>().Where(item => item.Name == "ConnectBtn"))
                    item.CheckedChanged -= ConnectionChanged;
                foreach (var image in owned.Values) image.Dispose();
                owned.Clear();
                Refreshers.Remove(strip);
            }
            Refreshers.Add(strip, RefreshImages);
            strip.DpiChangedAfterParent += RefreshEvent;
            strip.SystemColorsChanged += RefreshEvent;
            strip.Disposed += DisposeImages;
            foreach (var item in strip.Items.OfType<ToolStripButton>().Where(item => item.Name == "ConnectBtn"))
                item.CheckedChanged += ConnectionChanged;
            RefreshImages();
        }

        public static void ExportAssets(string directory)
        {
            Directory.CreateDirectory(directory);
            string F(float value) => value.ToString(CultureInfo.InvariantCulture);
            foreach (var pair in Glyphs)
            {
                var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke-width=\"1.65\" stroke-linecap=\"round\" stroke-linejoin=\"round\">\n");
                foreach (var shape in pair.Value)
                {
                    var v = shape.Values;
                    string color = shape.Accent ? "#3056d3" : "#334155";
                    if (shape.Kind == "polyline") svg.Append($"<polyline stroke=\"{color}\" points=\"{string.Join(" ", Enumerable.Range(0, v.Length / 2).Select(i => F(v[2*i]) + "," + F(v[2*i+1])))}\"/>\n");
                    else if (shape.Kind == "rect") svg.Append($"<rect stroke=\"{color}\" x=\"{F(v[0])}\" y=\"{F(v[1])}\" width=\"{F(v[2])}\" height=\"{F(v[3])}\" rx=\"1\"/>\n");
                    else svg.Append($"<circle {(shape.Kind == "dot" ? "fill" : "stroke")}=\"{color}\" cx=\"{F(v[0])}\" cy=\"{F(v[1])}\" r=\"{F(v[2])}\"/>\n");
                }
                File.WriteAllText(Path.Combine(directory, pair.Key + ".svg"), svg.Append("</svg>").ToString());
                foreach (int size in new[] { 16, 20, 24, 32, 48 })
                {
                    using var bitmap = Render(pair.Key, size);
                    bitmap.Save(Path.Combine(directory, pair.Key + "-" + size + ".png"));
                }
            }
            using var sheet = new Bitmap(960, 660);
            using var g = Graphics.FromImage(sheet);
            g.Clear(Color.FromArgb(248, 250, 253));
            using var title = new Font("Segoe UI", 22, FontStyle.Bold);
            using var label = new Font("Segoe UI", 10);
            using var ink = new SolidBrush(Color.FromArgb(27, 39, 59));
            using var muted = new SolidBrush(Color.FromArgb(101, 115, 137));
            g.DrawString("GURUX  /  Command icons", title, ink, 32, 24);
            g.DrawString("16 original vector glyphs · 24-unit grid · slate + blue", label, muted, 34, 69);
            int index = 0;
            foreach (string name in Glyphs.Keys)
            {
                int x = 32 + index % 4 * 232, y = 112 + index / 4 * 128;
                g.FillRectangle(Brushes.White, x, y, 216, 112);
                using var big = Render(name, 36);
                using var small = Render(name, 20);
                g.DrawImageUnscaled(big, x + 20, y + 20);
                g.DrawImageUnscaled(small, x + 172, y + 29);
                g.DrawString(name.Replace('-', ' '), label, ink, x + 18, y + 76);
                ++index;
            }
            sheet.Save(Path.Combine(directory, "icon-family.png"));
        }
    }
}

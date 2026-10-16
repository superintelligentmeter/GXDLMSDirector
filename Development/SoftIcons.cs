// Modified for the Intelmeter fork, 2026-10-16: .NET 10 migration, UI modernization and compatibility fixes. GPL-2.0-only; original notices retained.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GXDLMSDirector
{
    /// <summary>Original 24-unit vector glyphs. SVG exports and toolbar bitmaps share the same geometry.</summary>
    public static class SoftIcons
    {
        private sealed record Shape(string Kind, bool Accent, float[] Values);
        private static Shape P(bool accent, params float[] points) => new("polyline", accent, points);
        private static Shape Q(bool accent, params float[] points) => new("polygon", accent, points);
        private static Shape W(params float[] points) => new("white", false, points);
        private static Shape R(bool accent, float x, float y, float width, float height) => new("rect", accent, new[] { x, y, width, height });
        private static Shape C(bool accent, float x, float y, float radius) => new("circle", accent, new[] { x, y, radius });
        private static Shape D(bool accent, float x, float y, float radius) => new("dot", accent, new[] { x, y, radius });
        private static readonly Dictionary<string, Shape[]> Glyphs = new()
        {
            ["new"] = new[] { R(false,3,2,15,20), P(false,7,7,13,7), P(false,7,11,11,11), D(true,18,17,5), W(18,14.5f,18,19.5f), W(15.5f,17,20.5f,17) },
            ["open"] = new[] { Q(false,2,5,9,5,12,8,21,8,21,20,2,20), Q(true,2,10,23,10,19,20,2,20), P(false,5,7,9,7) },
            ["save"] = new[] { R(false,3,2,18,20), R(true,7,2,10,7), R(true,7,14,10,8), P(false,10,17,14,17), P(false,14,4,14,7) },
            ["connect"] = new[] { P(false,8,3,8,8), P(false,16,3,16,8), R(false,4,7,16,9), R(true,8,13,8,5), P(false,12,18,12,21,8,21) },
            ["disconnect"] = new[] { R(false,3,8,7,10), R(false,14,6,7,10), P(false,1,13,3,13), P(false,21,11,23,11), P(true,12,3,10,6), P(true,14,18,12,21) },
            ["read"] = new[] { R(false,2,2,13,20), R(true,5,5,7,4), P(false,7,18,10,18), Q(true,13,12,18,12,18,9,23,14,18,19,18,16,13,16) },
            ["write"] = new[] { R(false,2,2,13,20), R(true,5,5,7,4), P(false,7,18,10,18), Q(true,23,12,18,12,18,9,13,14,18,19,18,16,23,16) },
            ["notifications"] = new[] { Q(false,3,18,6,14,6,8,8,4,14,4,17,8,17,14,20,18), R(true,8,18,7,3), D(true,18,5,4) },
            ["properties"] = new[] { R(false,2,2,20,20), P(false,6,7,18,7), P(false,6,12,18,12), P(false,6,17,18,17), D(true,9,7,2.4f), D(true,15,12,2.4f), D(true,10,17,2.4f) },
            ["force-read"] = new[] { R(false,2,2,20,20), Q(true,13,3,6,14,11,14,10,22,19,10,14,10,15,3) },
            ["time-zone"] = new[] { C(false,12,12,10), P(false,12,5,12,12,17,15), D(true,19,5,3), D(true,12,12,1.6f) },
            ["delete"] = new[] { R(false,5,6,14,16), R(true,3,4,18,4), P(false,9,4,9,2,15,2,15,4), P(false,10,11,10,18), P(false,14,11,14,18) },
            ["help"] = new[] { C(false,12,12,10), P(true,8,8,9,6,14,6,16,8,16,10,12,13,12,14), D(true,12,18,1.2f) },
            ["add-device"] = new[] { R(false,2,2,14,20), R(true,5,5,8,5), P(false,6,17,10,17), D(true,18,17,5), W(18,14.5f,18,19.5f), W(15.5f,17,20.5f,17) },
            ["cancel"] = new[] { R(false,2,2,20,20), P(true,8,8,16,16), P(true,16,8,8,16) },
            ["refresh"] = new[] { C(false,12,12,10), P(false,6,9,8,6,13,5,17,7,19,10), Q(true,19,4,19,11,12,11), P(false,18,15,16,18,11,19,7,17,5,14), Q(true,5,20,5,13,12,13) }
        };

        public static Bitmap Render(string name, int pixels)
        {
            var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(pixels / 24f, pixels / 24f);
            foreach (var shape in Glyphs[name])
            {
                var color = shape.Accent ? Color.FromArgb(15, 148, 136) : Color.FromArgb(30, 73, 78);
                if (shape.Kind == "white") color = Color.White;
                var fill = shape.Accent ? Color.FromArgb(15, 148, 136) : Color.FromArgb(197, 226, 219);
                using var fillBrush = new SolidBrush(fill);
                using var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                var v = shape.Values;
                switch (shape.Kind)
                {
                    case "white":
                    case "polyline":
                        var points = Enumerable.Range(0, v.Length / 2).Select(i => new PointF(v[i * 2], v[i * 2 + 1])).ToArray();
                        g.DrawLines(pen, points);
                        break;
                    case "polygon":
                        g.FillPolygon(fillBrush, Enumerable.Range(0, v.Length / 2).Select(i => new PointF(v[i * 2], v[i * 2 + 1])).ToArray());
                        break;
                    case "rect":
                        using (var path = new GraphicsPath())
                        {
                            float d = Math.Min(5, Math.Min(v[2], v[3]));
                            path.AddArc(v[0], v[1], d, d, 180, 90);
                            path.AddArc(v[0] + v[2] - d, v[1], d, d, 270, 90);
                            path.AddArc(v[0] + v[2] - d, v[1] + v[3] - d, d, d, 0, 90);
                            path.AddArc(v[0], v[1] + v[3] - d, d, d, 90, 90);
                            path.CloseFigure();
                            g.FillPath(fillBrush, path);
                        }
                        break;
                    case "circle": g.FillEllipse(fillBrush, v[0] - v[2], v[1] - v[2], 2 * v[2], 2 * v[2]); break;
                    case "dot":
                        using (var brush = new SolidBrush(color)) g.FillEllipse(brush, v[0] - v[2], v[1] - v[2], 2 * v[2], 2 * v[2]);
                        break;
                }
            }
            return bitmap;
        }

        public static void ExportAssets(string directory)
        {
            Directory.CreateDirectory(directory);
            string F(float value) => value.ToString(CultureInfo.InvariantCulture);
            foreach (var pair in Glyphs)
            {
                var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\">\n");
                foreach (var shape in pair.Value)
                {
                    var v = shape.Values;
                    string color = shape.Accent ? "#0f9488" : "#1e494e";
                    string fill = shape.Accent ? "#0f9488" : "#c5e2db";
                    if (shape.Kind == "white") color = "#ffffff";
                    if (shape.Kind == "polyline" || shape.Kind == "white") svg.Append($"<polyline stroke=\"{color}\" points=\"{string.Join(" ", Enumerable.Range(0, v.Length / 2).Select(i => F(v[2*i]) + "," + F(v[2*i+1])))}\"/>\n");
                    else if (shape.Kind == "polygon") svg.Append($"<polygon fill=\"{fill}\" points=\"{string.Join(" ", Enumerable.Range(0, v.Length / 2).Select(i => F(v[2*i]) + "," + F(v[2*i+1])))}\"/>\n");
                    else if (shape.Kind == "rect") svg.Append($"<rect fill=\"{fill}\" x=\"{F(v[0])}\" y=\"{F(v[1])}\" width=\"{F(v[2])}\" height=\"{F(v[3])}\" rx=\"{F(Math.Min(2.5f, Math.Min(v[2], v[3])/2))}\"/>\n");
                    else svg.Append($"<circle fill=\"{(shape.Kind == "dot" ? color : fill)}\" cx=\"{F(v[0])}\" cy=\"{F(v[1])}\" r=\"{F(v[2])}\"/>\n");
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
            g.DrawString("GURUX  /  Soft Duo", title, ink, 32, 24);
            g.DrawString("SET 02     Rounded silhouettes · jade accents · soft mint fills", label, muted, 34, 69);
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

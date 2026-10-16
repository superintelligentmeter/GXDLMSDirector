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
    public static class StudioIcons
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
            ["connect"] = new[] { P(true,10,7,13,4,16,3,19,4,21,7,20,10,17,13), P(false,14,17,11,20,8,21,5,20,3,17,4,14,7,11), P(true,8,16,16,8) },
            ["disconnect"] = new[] { P(false,12,5,14,3,17,3,20,6,20,9,18,11), P(false,12,19,10,21,7,21,4,18,4,15,6,13), P(true,8,6,10,8), P(true,14,16,16,18) },
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

        public static Bitmap Render(string name, int pixels, bool dark = false)
        {
            var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = pixels / 24f;
            float stroke = Math.Max(1, (float)Math.Round(1.4f * scale));
            // GDI+ integer coordinates are pixel centres. Odd-width strokes sit
            // on those centres; even-width strokes sit between them.
            float offset = (int)stroke % 2 == 0 ? .5f : 0f;
            float Snap(float value) => (float)Math.Round(value * scale - offset) + offset;
            foreach (var shape in Glyphs[name])
            {
                var color = SystemInformation.HighContrast ? SystemColors.ControlText : ColorTranslator.FromHtml(Ink(name, shape.Accent, dark));
                using var pen = new Pen(color, stroke) { StartCap = LineCap.Square, EndCap = LineCap.Square, LineJoin = LineJoin.Miter };
                var v = shape.Values;
                switch (shape.Kind)
                {
                    case "polyline":
                        var points = Enumerable.Range(0, v.Length / 2).Select(i => new PointF(Snap(v[i * 2]), Snap(v[i * 2 + 1]))).ToArray();
                        g.DrawLines(pen, points);
                        break;
                    case "rect":
                        using (var path = new GraphicsPath())
                        {
                            float left = Snap(v[0]), top = Snap(v[1]);
                            float right = Snap(v[0] + v[2]), bottom = Snap(v[1] + v[3]);
                            float d = Math.Min(2 * scale, Math.Min(right - left, bottom - top));
                            path.AddArc(left, top, d, d, 180, 90);
                            path.AddArc(right - d, top, d, d, 270, 90);
                            path.AddArc(right - d, bottom - d, d, d, 0, 90);
                            path.AddArc(left, bottom - d, d, d, 90, 90);
                            path.CloseFigure();
                            g.DrawPath(pen, path);
                        }
                        break;
                    case "circle":
                        float x = Snap(v[0] - v[2]), y = Snap(v[1] - v[2]);
                        g.DrawEllipse(pen, x, y, Snap(v[0] + v[2]) - x, Snap(v[1] + v[2]) - y);
                        break;
                    case "dot":
                        using (var brush = new SolidBrush(color)) g.FillEllipse(brush, (v[0] - v[2]) * scale, (v[1] - v[2]) * scale, 2 * v[2] * scale, 2 * v[2] * scale);
                        break;
                }
            }
            return bitmap;
        }

        public static void ExportAssets(string directory, bool dark)
        {
            Directory.CreateDirectory(directory);
            string F(float value) => value.ToString(CultureInfo.InvariantCulture);
            foreach (var pair in Glyphs)
            {
                var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke-width=\"1.4\" stroke-linecap=\"square\" stroke-linejoin=\"miter\">\n");
                foreach (var shape in pair.Value)
                {
                    var v = shape.Values;
                    string color = Ink(pair.Key, shape.Accent, dark);
                    if (shape.Kind == "polyline") svg.Append($"<polyline stroke=\"{color}\" points=\"{string.Join(" ", Enumerable.Range(0, v.Length / 2).Select(i => F(v[2*i]) + "," + F(v[2*i+1])))}\"/>\n");
                    else if (shape.Kind == "rect") svg.Append($"<rect stroke=\"{color}\" x=\"{F(v[0])}\" y=\"{F(v[1])}\" width=\"{F(v[2])}\" height=\"{F(v[3])}\" rx=\"1\"/>\n");
                    else svg.Append($"<circle {(shape.Kind == "dot" ? "fill" : "stroke")}=\"{color}\" cx=\"{F(v[0])}\" cy=\"{F(v[1])}\" r=\"{F(v[2])}\"/>\n");
                }
                File.WriteAllText(Path.Combine(directory, pair.Key + ".svg"), svg.Append("</svg>").ToString());
                foreach (int size in new[] { 16, 20, 24, 32, 48 })
                {
                    using var bitmap = Render(pair.Key, size, dark);
                    bitmap.Save(Path.Combine(directory, pair.Key + "-" + size + ".png"));
                }
            }
        }
        private static string Ink(string name, bool accent, bool dark)
        {
            if (name == "disconnect") return dark ? "#a6a6a6" : "#666666";
            if (name == "save" || name == "help" || name == "refresh") return dark ? "#75beff" : "#007acc";
            if (!accent) return dark ? "#d4d4d4" : "#424242";
            if (name == "new" || name == "connect" || name == "add-device") return dark ? "#89d185" : "#388a34";
            if (name == "delete" || name == "cancel") return dark ? "#f48771" : "#c74e39";
            return dark ? "#75beff" : "#007acc";
        }
        public static void Preview(string directory)
        {
            using var sheet = new Bitmap(1040, 780);
            using var g = Graphics.FromImage(sheet);
            g.Clear(Color.FromArgb(30,30,30));
            using var title = new Font("Segoe UI", 22);
            using var label = new Font("Segoe UI", 10);
            using var smallFont = new Font("Segoe UI", 9);
            g.DrawString("GURUX  /  Studio Outline", title, Brushes.WhiteSmoke, 30, 22);
            g.DrawString("Fine strokes. Familiar commands. Blue actions and green connection accents.", label, Brushes.Silver, 32, 68);
            int index = 0;
            foreach (string name in Glyphs.Keys)
            {
                int x = 32 + index % 4 * 248, y = 110 + index / 4 * 132;
                using var tile = new SolidBrush(Color.FromArgb(38,38,38));
                g.FillRectangle(tile, x, y, 232, 118);
                using var big = Render(name,32,true);
                using var small = Render(name,16,true);
                g.DrawImageUnscaled(big,x+18,y+18);
                g.DrawImageUnscaled(small,x+78,y+27);
                g.FillRectangle(Brushes.WhiteSmoke,x+151,y+10,68,55);
                using var light = Render(name,20,false);
                g.DrawImageUnscaled(light,x+175,y+27);
                g.DrawString(name.Replace('-',' '),label,Brushes.Gainsboro,x+18,y+83);
                ++index;
            }
            g.DrawString("TOOLBAR PREVIEW  /  16 px",smallFont,Brushes.Silver,32,655);
            using var bar = new SolidBrush(Color.FromArgb(45,45,45));
            g.FillRectangle(bar,32,685,976,48);
            int bx=48;
            foreach (string name in Glyphs.Keys)
            {
                using var icon=Render(name,16,true);
                g.DrawImageUnscaled(icon,bx,701);
                bx+=38;
            }
            sheet.Save(Path.Combine(directory,"icon-family.png"));
        }
    }
}

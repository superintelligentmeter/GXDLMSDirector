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
    public static class TreeIcons
    {
        private sealed record Shape(string Kind, bool Accent, float[] Values);
        private static Shape P(bool accent, params float[] points) => new("polyline", accent, points);
        private static Shape R(bool accent, float x, float y, float width, float height) => new("rect", accent, new[] { x, y, width, height });
        private static Shape C(bool accent, float x, float y, float radius) => new("circle", accent, new[] { x, y, radius });
        private static Shape D(bool accent, float x, float y, float radius) => new("dot", accent, new[] { x, y, radius });
        private static readonly Dictionary<string, Shape[]> Glyphs = new()
        {
            ["devices"] = new[] { R(true,8,2,9,8), P(false,12,10,12,14,4,14,4,17), P(false,12,14,20,14,20,17), R(false,1,17,6,5), R(false,17,17,6,5) },
            ["meter"] = new[] { R(false,5,2,14,20), R(true,8,5,8,5), P(false,9,16,15,16), P(false,10,19,14,19) },
            ["connected"] = new[] { R(false,3,2,14,20), R(false,6,5,8,5), P(true,13,17,16,20,22,12) },
            ["disconnected"] = new[] { R(false,3,2,14,20), R(false,6,5,8,5), P(true,14,15,21,22), P(true,21,15,14,22) },
            ["association"] = new[] { R(false,2,4,8,12), R(true,14,8,8,12), P(true,7,12,17,12) },
            ["data-group"] = new[] { R(true,2,3,8,8), R(false,14,3,8,8), R(false,2,15,8,7), R(true,14,15,8,7) },
            ["data-value"] = new[] { P(false,15,2,4,2,4,22,20,22,20,7,15,2,15,7,20,7), P(true,8,12,16,12), P(true,8,17,14,17) },
            ["clock"] = new[] { C(true,12,12,9), P(false,12,6,12,12,17,15) },
            ["register"] = new[] { R(false,2,4,20,16), P(true,7,9,7,15), P(true,11,9,15,9,15,12,11,15,15,15) },
            ["profile"] = new[] { R(false,2,3,20,18), P(true,2,9,22,9), P(false,9,9,9,21), P(false,2,15,22,15) },
            ["modified"] = new[] { R(false,4,2,14,20), P(false,7,7,14,7), P(true,11,20,12,16,19,9,22,12,15,19,11,20) },
            ["success"] = new[] { C(false,12,12,9), P(true,7,12,11,16,17,8) },
            ["warning"] = new[] { P(false,12,2,23,21,1,21,12,2), P(true,12,8,12,14), D(true,12,18,0.8f) },
            ["error"] = new[] { P(false,8,2,16,2,22,8,22,16,16,22,8,22,2,16,2,8,8,2), P(true,8,8,16,16), P(true,16,8,8,16) },
            ["settings"] = new[] { P(false,3,6,21,6), P(false,3,12,21,12), P(false,3,18,21,18), R(true,7,4,3,4), R(true,14,10,3,4), R(true,8,16,3,4) }
        };

        public static int ObjectIndex(Gurux.DLMS.Enums.ObjectType type, bool group = false) => type switch
        {
            Gurux.DLMS.Enums.ObjectType.Clock => 13,
            Gurux.DLMS.Enums.ObjectType.AssociationLogicalName or Gurux.DLMS.Enums.ObjectType.AssociationShortName => 14,
            Gurux.DLMS.Enums.ObjectType.Register or Gurux.DLMS.Enums.ObjectType.ExtendedRegister or Gurux.DLMS.Enums.ObjectType.DemandRegister => 15,
            Gurux.DLMS.Enums.ObjectType.ProfileGeneric => 8,
            _ => group ? 11 : 7
        };
        // Preserve the legacy numeric slots; selected nodes use the same semantic glyph.
        public const int ModifiedIndex = 10;
        public static int DeviceIndex(bool connected) => connected ? 3 : 4;
        public static int ValueIndex(Gurux.DLMS.Objects.GXDLMSObject value) =>
            value.GetDirtyAttributeIndexes().Length != 0 ? ModifiedIndex : ObjectIndex(value.ObjectType);

        private static readonly ConditionalWeakTable<ImageList, ImageBinding> Bindings = new();
        public static void Apply(ImageList list, Control owner) => Bind(list, owner, new[]
            {"devices","meter","meter","connected","disconnected","connected","disconnected","data-value","profile","modified","modified","data-group","settings","clock","association","register"});

        // GXConformanceTest.ErrorLevel: 0 success, 1 warning, 2 error.
        public static void ApplyStatus(ImageList list, Control owner) =>
            Bind(list, owner, new[] { "success", "warning", "error" });

        private static void Bind(ImageList list, Control owner, string[] names)
        {
            if (!Bindings.TryGetValue(list, out var binding))
            {
                binding = new ImageBinding(list, owner, names);
                Bindings.Add(list, binding);
            }
            binding.Refresh();
        }

        private sealed class ImageBinding
        {
            private readonly ImageList list;
            private readonly Control owner;
            private readonly string[] names;
            private readonly List<Bitmap> sources = new();
            private bool disposed;

            public ImageBinding(ImageList list, Control owner, string[] names)
            {
                this.list = list;
                this.owner = owner;
                this.names = names;
                owner.DpiChangedAfterParent += RefreshEvent;
                owner.SystemColorsChanged += RefreshEvent;
                owner.Disposed += OwnerDisposed;
                list.Disposed += ListDisposed;
            }

            public void Refresh()
            {
                if (disposed || owner.IsDisposed || owner.Disposing) return;
                // ImageList can defer native handle creation. Keep source images alive
                // until they have been removed, including across handle recreation.
                list.Images.Clear();
                ReleaseSources();
                list.ColorDepth = ColorDepth.Depth32Bit;
                int size = Math.Clamp((int)Math.Round(16 * owner.DeviceDpi / 96f), 1, 256);
                list.ImageSize = new Size(size, size);
                list.TransparentColor = Color.Transparent;
                foreach (var name in names)
                {
                    var bitmap = Render(name, size);
                    sources.Add(bitmap);
                    list.Images.Add(name, bitmap);
                }
            }

            private void RefreshEvent(object sender, EventArgs e) => Refresh();
            private void OwnerDisposed(object sender, EventArgs e)
            {
                list.Images.Clear();
                Detach();
            }
            private void ListDisposed(object sender, EventArgs e) => Detach();
            private void ReleaseSources()
            {
                foreach (var image in sources) image.Dispose();
                sources.Clear();
            }
            private void Detach()
            {
                if (disposed) return;
                disposed = true;
                owner.DpiChangedAfterParent -= RefreshEvent;
                owner.SystemColorsChanged -= RefreshEvent;
                owner.Disposed -= OwnerDisposed;
                list.Disposed -= ListDisposed;
                ReleaseSources();
                Bindings.Remove(list);
            }
        }
        public static Bitmap Render(string name, int pixels, bool dark = false)
        {
            var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(pixels / 24f, pixels / 24f);
            foreach (var shape in Glyphs[name])
            {
                var color = SystemInformation.HighContrast ? SystemColors.ControlText : ColorTranslator.FromHtml(Ink(name, shape.Accent, dark));
                using var pen = new Pen(color, 1.4f) { StartCap = LineCap.Square, EndCap = LineCap.Square, LineJoin = LineJoin.Miter };
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
            if (name == "save" || name == "help" || name == "refresh") return dark ? "#75beff" : "#007acc";
            if (!accent) return dark ? "#d4d4d4" : "#424242";
            if (name == "connected" || name == "success") return dark ? "#89d185" : "#388a34";
            if (name == "disconnected" || name == "error") return dark ? "#f48771" : "#c74e39";
            if (name == "warning") return dark ? "#cca700" : "#9a6700";
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
            g.DrawString("GURUX  /  Object tree", title, Brushes.WhiteSmoke, 30, 22);
            g.DrawString("Distinct symbols for meters, groups, values, and connection states.", label, Brushes.Silver, 32, 68);
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

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GXDLMSDirector
{
    // Shell-only styling. Do not change fonts, autoscaling or object-editor layouts.
    internal static class ShellPresentation
    {
        internal static Color Surface => SystemInformation.HighContrast ? SystemColors.Window : Color.White;
        internal static Color Chrome => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(243, 243, 243);
        internal static Color Line => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(214, 214, 214);
        internal static Color Accent => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(0, 122, 204);

        internal static void Apply(Control root)
        {
            if (root is TabPage page)
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = Surface;
                // Keep the navigation scrollbars clear of the bottom tab strip.
                page.Padding = page.Name == "TreeView" || page.Name == "ListView"
                    ? new Padding(0, 0, 0, (int)Math.Ceiling(4 * page.DeviceDpi / 96f)) : Padding.Empty;
            }
            if (root is ListView list) list.BorderStyle = BorderStyle.None;
            if (root is TreeView tree) tree.BorderStyle = BorderStyle.None;
            if (root is TextBoxBase output && (root.Name == "TraceView" || root.Name == "EventsView"))
            {
                output.BorderStyle = BorderStyle.None;
                output.BackColor = Surface;
            }
            if (root is DataGridView grid)
            {
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            }
            if (root is Splitter splitter) splitter.BackColor = Surface;
            if (root is RoundedShellFrame frame)
                frame.BackColor = Chrome;
            else if (root is Panel panel)
                panel.BackColor = panel.Name == "IntegratedTitleBar" ? Chrome : Surface;
            if (root is ToolStrip strip)
            {
                strip.Renderer = new FlatStripRenderer();
                strip.BackColor = Chrome;
            }
            foreach (Control child in root.Controls) Apply(child);
        }

        private sealed class FlatStripRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                if (SystemInformation.HighContrast) { base.OnRenderToolStripBackground(e); return; }
                e.Graphics.Clear(Chrome);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                if (e.ToolStrip is ToolStripDropDown || SystemInformation.HighContrast)
                    base.OnRenderToolStripBorder(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                if (SystemInformation.HighContrast) { base.OnRenderSeparator(e); return; }
                using var pen = new Pen(Line);
                int inset = Math.Max(2, (int)Math.Round(5 * e.ToolStrip.DeviceDpi / 96f));
                if (e.Vertical)
                    e.Graphics.DrawLine(pen, e.Item.Width / 2, inset, e.Item.Width / 2, e.Item.Height - inset);
                else
                    e.Graphics.DrawLine(pen, inset, e.Item.Height / 2, e.Item.Width - inset, e.Item.Height / 2);
            }
        }
    }

    internal sealed class RoundedShellFrame : Panel
    {
        internal RoundedShellFrame()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = ShellPresentation.Chrome;
            UpdateInset();
        }
        private void UpdateInset() => Padding = new Padding((int)Math.Ceiling(10 * DeviceDpi / 96f));
        protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateInset(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float inset = DeviceDpi / 96f;
            var rect = new RectangleF(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1);
            if (rect.Width <= 0 || rect.Height <= 0) return;
            float diameter = Math.Min(20 * DeviceDpi / 96f, Math.Min(rect.Width, rect.Height));
            using var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(ShellPresentation.Surface);
            using var line = new Pen(SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(160, 173, 190));
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(line, path);
        }
    }

    // Retain native splitter dragging; outer section frames provide the borders.
    public class FlatShellSplitter : Splitter
    {
        public FlatShellSplitter()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BorderStyle = BorderStyle.None;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(ShellPresentation.Surface);
        }
    }

    // Keep the native TabControl selection, accessibility, overflow and keyboard
    // behavior; replace only its painted chrome. Native rendering in high contrast.
    public class FlatShellTabControl : TabControl
    {
        private int hovered = -1;

        public FlatShellTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, !SystemInformation.HighContrast);
            UpdateSpacing();
        }

        private void UpdateSpacing()
        {
            int horizontal = (int)Math.Round(12 * DeviceDpi / 96f);
            int vertical = (int)Math.Round(3 * DeviceDpi / 96f);
            if (!IsHandleCreated) Padding = new Point(horizontal, vertical);
            else
            {
                // The managed Padding setter recreates this HWND and all children.
                // Update the native tab metrics in place during monitor transitions.
                SendMessage(Handle, 0x132B, IntPtr.Zero, (IntPtr)((vertical << 16) | horizontal)); // TCM_SETPADDING
                // Native tabs recalculate their item sizes when the font is applied.
                // Reuse their current HFONT; do not create or change the font.
                var fontHandle = SendMessage(Handle, 0x31, IntPtr.Zero, IntPtr.Zero); // WM_GETFONT
                SendMessage(Handle, 0x30, fontHandle, IntPtr.Zero); // WM_SETFONT
                base.OnResize(EventArgs.Empty); // Invalidate TabControl's cached page rectangle.
            }
            Invalidate();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateSpacing();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            UpdateSpacing();
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, !SystemInformation.HighContrast);
            UpdateStyles();
            Invalidate();
        }

        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int next = -1;
            for (int i = 0; i < TabCount; ++i) if (GetTabRect(i).Contains(e.Location)) { next = i; break; }
            if (next != hovered) { hovered = next; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = -1; Invalidate(); }

        private GraphicsPath TabOutline(Rectangle rect, int join, bool bottom)
        {
            float left = rect.Left, right = rect.Right - 1;
            float top = bottom ? 2 * join - (rect.Bottom - 1) : rect.Top;
            float radius = Math.Min(4 * DeviceDpi / 96f, Math.Min((right - left) / 2, (join - top) / 2));
            var path = new GraphicsPath();
            path.AddLine(left, join, left, top + radius);
            path.AddArc(left, top, radius * 2, radius * 2, 180, 90);
            path.AddLine(left + radius, top, right - radius, top);
            path.AddArc(right - radius * 2, top, radius * 2, radius * 2, 270, 90);
            path.AddLine(right, top + radius, right, join);
            if (bottom)
            {
                using var mirror = new Matrix(1, 0, 0, -1, 0, 2 * join);
                path.Transform(mirror);
            }
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (SystemInformation.HighContrast) { base.OnPaint(e); return; }
            e.Graphics.Clear(ShellPresentation.Surface);
            if (TabCount == 0) return;
            var content = DisplayRectangle;
            bool bottom = Alignment == TabAlignment.Bottom;
            var header = bottom
                ? Rectangle.FromLTRB(0, content.Bottom, Width, Height)
                : Rectangle.FromLTRB(0, 0, Width, content.Top);
            using (var brush = new SolidBrush(ShellPresentation.Surface)) e.Graphics.FillRectangle(brush, header);
            using (var pen = new Pen(ShellPresentation.Line))
            {
                // Splitters own pane boundaries; another frame here doubles them.
                e.Graphics.DrawLine(pen, 0, bottom ? header.Top : header.Bottom - 1, Width, bottom ? header.Top : header.Bottom - 1);
            }
            for (int i = 0; i < TabCount; ++i)
            {
                var rect = GetTabRect(i);
                // Use one shared content edge for both selected and inactive tabs;
                // native selected-tab inflation otherwise leaves disconnected strips.
                rect.Y = bottom ? header.Top : header.Top + 2;
                rect.Height = Math.Max(1, header.Height - 2);
                if (rect.Width < 3 || rect.Height < 3) continue;
                bool selected = i == SelectedIndex;
                int join = bottom ? header.Top : header.Bottom - 1;
                using var outline = TabOutline(rect, join, bottom);
                var smoothing = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(i == hovered && !selected ? Color.FromArgb(226, 234, 243) : ShellPresentation.Surface))
                    e.Graphics.FillPath(brush, outline);
                if (selected)
                {
                    using var pen = new Pen(ShellPresentation.Accent);
                    e.Graphics.DrawPath(pen, outline);
                    e.Graphics.SmoothingMode = smoothing;
                    using var surfacePen = new Pen(ShellPresentation.Surface);
                    e.Graphics.DrawLine(surfacePen, rect.Left + 1, join, rect.Right - 2, join);
                }
                else
                {
                    using var pen = new Pen(ShellPresentation.Line);
                    outline.CloseFigure();
                    e.Graphics.DrawPath(pen, outline);
                }
                e.Graphics.SmoothingMode = smoothing;
                var text = Rectangle.Inflate(rect, -(int)Math.Round(3 * DeviceDpi / 96f), -2);
                TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Font, text,
                    Enabled && TabPages[i].Enabled ? Color.FromArgb(32, 32, 32) : SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                if (selected && Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(rect, -3, -3));
            }
        }
    }
}

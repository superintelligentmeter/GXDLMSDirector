// Intelmeter contributors, 2026-10-17. Consistent native dashboard button rendering. GPL-2.0-only.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    internal static class DashboardButtonPaint
    {
        internal static void Draw(ButtonBase button, PaintEventArgs e, bool active)
        {
            var bounds = new Rectangle(1, 1, button.Width - 3, button.Height - 3);
            if (bounds.Width < 2 || bounds.Height < 2) return;
            int radius = System.Math.Min((int)(6 * button.DeviceDpi / 96f), bounds.Height / 2), diameter = radius * 2;
            using var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90); path.CloseFigure();
            e.Graphics.Clear(button.Parent?.BackColor ?? SystemColors.Control);
            bool hover = button.ClientRectangle.Contains(button.PointToClient(Control.MousePosition));
            bool primary = button.BackColor.GetBrightness() < 0.55f;
            Color fill = !button.Enabled ? SystemColors.Control : SystemInformation.HighContrast ? SystemColors.Control : active || hover ? primary ? Color.FromArgb(0, 95, 180) : Color.FromArgb(230, 241, 253) : button.BackColor;
            using var brush = new SolidBrush(fill);
            using var pen = new Pen(SystemInformation.HighContrast ? SystemColors.ControlText : button.Focused || active ? Color.FromArgb(0, 120, 215) : Color.FromArgb(212, 223, 237));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path);
            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, bounds, button.Enabled ? SystemInformation.HighContrast ? SystemColors.ControlText : button.ForeColor : SystemColors.GrayText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            // The solid rounded border above indicates focus without a dotted inset.
        }
    }
    internal sealed class DashboardButton : Button
    {
        internal DashboardButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e) => DashboardButtonPaint.Draw(this, e, false);
    }
    internal sealed class DashboardMonitor : CheckBox
    {
        internal DashboardMonitor() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); BackColor = Color.White; }
        protected override void OnPaint(PaintEventArgs e) => DashboardButtonPaint.Draw(this, e, Checked);
    }
}

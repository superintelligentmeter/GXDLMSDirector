// Intelmeter contributors, 2026-10-17. Rounded frames around native editors. GPL-2.0-only.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    internal sealed class DashboardFieldHost : Panel
    {
        internal Control Editor { get; }
        internal DashboardFieldHost(Control editor)
        {
            Editor = editor; TabStop = false; BackColor = Color.White;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            if (editor is TextBox text) text.BorderStyle = BorderStyle.None;
            if (editor is NumericUpDown number) number.BorderStyle = BorderStyle.None;
            if (editor is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
            editor.Dock = DockStyle.None; editor.Anchor = AnchorStyles.None; editor.Margin = Padding.Empty; editor.TabIndex = 0;
            Controls.Add(editor);
            editor.Enter += (_, _) => Invalidate(); editor.Leave += (_, _) => Invalidate();
            editor.FontChanged += (_, _) => PerformLayout();
            editor.SizeChanged += (_, _) => ClipCombo();
            MouseDown += (_, _) => { editor.Focus(); if (editor is ComboBox list) list.DroppedDown = true; };
        }
        internal static DashboardFieldHost Wrap(Control editor)
        {
            var margin = editor.Margin;
            return new DashboardFieldHost(editor) { Dock = DockStyle.Fill, Margin = margin, AccessibleName = editor.AccessibleName };
        }
        private void ClipCombo()
        {
            if (Editor is not ComboBox combo || combo.Width < 3 || combo.Height < 3) return;
            // Suppress only the native rectangular frame. The edit/display surface,
            // arrow, list popup, keyboard navigation and accessibility remain native.
            var old = combo.Region; combo.Region = new Region(new Rectangle(1, 1, combo.Width - 2, combo.Height - 2)); old?.Dispose();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e); if (Editor == null) return;
            int inset = Math.Max(5, (int)Math.Round(8 * DeviceDpi / 96.0));
            int h = Editor.PreferredSize.Height;
            Editor.SetBounds(inset, Math.Max(2, (ClientSize.Height - h) / 2), Math.Max(1, ClientSize.Width - inset * 2), h);
            ClipCombo();
        }
        protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); PerformLayout(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); if (Width < 3 || Height < 3) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float radius = 6 * DeviceDpi / 96f, d = Math.Min(radius * 2, Height - 2);
            using var path = new GraphicsPath();
            path.AddArc(0.5f, 0.5f, d, d, 180, 90); path.AddArc(Width - d - 1.5f, 0.5f, d, d, 270, 90);
            path.AddArc(Width - d - 1.5f, Height - d - 1.5f, d, d, 0, 90); path.AddArc(0.5f, Height - d - 1.5f, d, d, 90, 90); path.CloseFigure();
            using var pen = new Pen(SystemInformation.HighContrast ? SystemColors.ControlText : ContainsFocus ? Color.FromArgb(0, 120, 215) : Color.FromArgb(212, 223, 237));
            e.Graphics.DrawPath(pen, path);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && Editor != null) { var clip = Editor.Region; Editor.Region = null; clip?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GXDLMSDirector
{
    // Retain the native sizable-window styles and system commands. Only the
    // caption is replaced; fonts and the application DPI policy are untouched.
    public class CaptionMenuForm : Form
    {
        private CaptionBar caption;
        protected void IntegrateMenu(MenuStrip menu)
        {
            SuspendLayout();
            int position = Controls.GetChildIndex(menu);
            caption = new CaptionBar(this, menu) { Name = "IntegratedTitleBar", Dock = DockStyle.Top };
            Controls.Add(caption);
            Controls.SetChildIndex(caption, position);
            Padding = new Padding(ScaleCaption(4));
            caption.UpdateMetrics();
            ResumeLayout(true);
            if (IsHandleCreated) RecreateHandle();
        }

        internal int ScaleCaption(int value) => (int)Math.Round(value * DeviceDpi / 96f);
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); caption?.UpdateTitle(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); caption?.Invalidate(true); }

        protected override void WndProc(ref Message m)
        {
            if (caption != null && m.Msg == 0x2E0) // WM_DPICHANGED
            {
                // Let WinForms finish its DPI scaling and suggested-bounds update
                // before applying caption metrics. Batch this as one layout pass.
                var suspended = new List<Control>();
                void SuspendTree(Control control)
                {
                    control.SuspendLayout();
                    suspended.Add(control);
                    if (control is TreeView tree) tree.BeginUpdate();
                    if (control is ListView list) list.BeginUpdate();
                    foreach (Control child in control.Controls) SuspendTree(child);
                }
                SuspendTree(this);
                try
                {
                    base.WndProc(ref m);
                    Padding = new Padding(ScaleCaption(4));
                    caption.UpdateMetrics();
                }
                finally
                {
                    for (int i = suspended.Count - 1; i >= 0; --i)
                    {
                        var control = suspended[i];
                        if (control.IsDisposed) continue;
                        control.ResumeLayout(true);
                        if (control is TreeView tree) tree.EndUpdate();
                        if (control is ListView list) list.EndUpdate();
                    }
                }
                return;
            }
            if (caption != null && m.Msg == 0x83 && m.WParam != IntPtr.Zero) // WM_NCCALCSIZE
            {
                if (IsZoomed(Handle))
                {
                    // During a cross-monitor move the HWND can still be associated
                    // with the old monitor. Use the proposed window rectangle.
                    var proposed = Marshal.PtrToStructure<NativeRect>(m.LParam);
                    var area = Screen.FromRectangle(Rectangle.FromLTRB(proposed.Left, proposed.Top, proposed.Right, proposed.Bottom)).WorkingArea;
                    Marshal.StructureToPtr(new NativeRect { Left = area.Left, Top = area.Top, Right = area.Right, Bottom = area.Bottom }, m.LParam, false);
                }
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
            if (caption == null || m.Msg != 0x84) return; // WM_NCHITTEST
            long xy = m.LParam.ToInt64();
            Point point = PointToClient(new Point((short)xy, (short)(xy >> 16)));
            int edge = ScaleCaption(5);
            if (WindowState == FormWindowState.Normal)
            {
                bool left = point.X < edge, right = point.X >= ClientSize.Width - edge;
                bool top = point.Y < edge, bottom = point.Y >= ClientSize.Height - edge;
                int hit = top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 0;
                if (hit != 0) { m.Result = (IntPtr)hit; return; }
            }
            if (caption.Bounds.Contains(point)) m.Result = (IntPtr)2; // Native caption drag/double click/snap.
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsZoomed(IntPtr window);

        private sealed class CaptionBar : Panel
        {
            private readonly CaptionMenuForm owner;
            private readonly MenuStrip menu;
            private readonly CaptionLabel title;
            private readonly CaptionButton minimize, maximize, close;
            private readonly PictureBox icon;
            private bool arranging;
            public CaptionBar(CaptionMenuForm owner, MenuStrip menu)
            {
                this.owner = owner;
                this.menu = menu;
                BackColor = ShellPresentation.Chrome;
                title = new CaptionLabel { AutoEllipsis = true, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft, ForeColor = SystemColors.GrayText, AccessibleName = "Window title" };
                icon = new PictureBox { SizeMode = PictureBoxSizeMode.CenterImage, AccessibleName = "Window menu", TabStop = false };
                if (owner.Icon != null) icon.Image = owner.Icon.ToBitmap();
                icon.MouseDoubleClick += (_, _) => owner.Close();
                icon.MouseClick += (_, e) =>
                {
                    var pt = icon.PointToScreen(new Point(0, icon.Height));
                    int command = TrackPopupMenu(GetSystemMenu(owner.Handle, false), 0x100, pt.X, pt.Y, 0, owner.Handle, IntPtr.Zero);
                    if (command != 0) SendMessage(owner.Handle, 0x112, (IntPtr)command, IntPtr.Zero);
                };
                minimize = new CaptionButton(owner, 0) { AccessibleName = "Minimize" };
                maximize = new CaptionButton(owner, 1) { AccessibleName = "Maximize or restore" };
                close = new CaptionButton(owner, 2) { AccessibleName = "Close" };
                minimize.Click += (_, _) => owner.WindowState = FormWindowState.Minimized;
                maximize.Click += (_, _) => owner.WindowState = owner.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                close.Click += (_, _) => owner.Close();
                menu.Dock = DockStyle.None;
                menu.AutoSize = false;
                menu.CanOverflow = true;
                Controls.AddRange(new Control[] { menu, title, icon, minimize, maximize, close });
                menu.Layout += (_, _) => { if (!arranging) PerformLayout(); };
                UpdateTitle();
            }
            public void UpdateTitle() { title.Text = owner.Text; }
            public void UpdateMetrics()
            {
                Height = owner.ScaleCaption(32);
                if (owner.Icon != null)
                {
                    using var source = owner.Icon.ToBitmap();
                    var previous = icon.Image;
                    icon.Image = new Bitmap(source, new Size(owner.ScaleCaption(20), owner.ScaleCaption(20)));
                    previous?.Dispose();
                }
                PerformLayout();
            }
            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                if (menu == null || arranging) return;
                arranging = true;
                try
                {
                int buttonWidth = owner.ScaleCaption(44), iconWidth = owner.ScaleCaption(30);
                close.Bounds = new Rectangle(Math.Max(0, Width - buttonWidth), 0, buttonWidth, Height);
                maximize.Bounds = new Rectangle(close.Left - buttonWidth, 0, buttonWidth, Height);
                minimize.Bounds = new Rectangle(maximize.Left - buttonWidth, 0, buttonWidth, Height);
                icon.Bounds = new Rectangle(0, 0, iconWidth, Height);
                int desired = menu.Padding.Horizontal;
                foreach (ToolStripItem item in menu.Items) if (item.Available) desired += item.GetPreferredSize(Size.Empty).Width + item.Margin.Horizontal;
                int available = Math.Max(0, minimize.Left - iconWidth - owner.ScaleCaption(40));
                menu.Bounds = new Rectangle(iconWidth, 0, Math.Min(desired + owner.ScaleCaption(8), available), Height);
                title.Bounds = new Rectangle(menu.Right + owner.ScaleCaption(12), 0, Math.Max(0, minimize.Left - menu.Right - owner.ScaleCaption(20)), Height);
                }
                finally { arranging = false; }
            }
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == 0x84) m.Result = (IntPtr)(-1); // Let the form handle empty caption space.
            }
            protected override void Dispose(bool disposing) { if (disposing) icon.Image?.Dispose(); base.Dispose(disposing); }
            [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool revert);
            [DllImport("user32.dll")] private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr owner, IntPtr rect);
            [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        }

        private sealed class CaptionLabel : Label
        {
            protected override void WndProc(ref Message m) { base.WndProc(ref m); if (m.Msg == 0x84) m.Result = (IntPtr)(-1); }
        }

        private sealed class CaptionButton : Button
        {
            private readonly CaptionMenuForm owner;
            private readonly int kind;
            private bool hover;
            public CaptionButton(CaptionMenuForm owner, int kind)
            {
                this.owner = owner; this.kind = kind;
                FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; TabStop = false;
            }
            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(hover ? kind == 2 ? Color.FromArgb(196, 43, 28) : SystemColors.ControlLight : ShellPresentation.Chrome);
                using var pen = new Pen(hover && kind == 2 ? Color.White : SystemColors.ControlText);
                int size = owner.ScaleCaption(10), x = (Width - size) / 2, y = (Height - size) / 2;
                if (kind == 0) e.Graphics.DrawLine(pen, x, y + size / 2, x + size, y + size / 2);
                else if (kind == 2) { e.Graphics.DrawLine(pen, x, y, x + size, y + size); e.Graphics.DrawLine(pen, x + size, y, x, y + size); }
                else
                {
                    if (owner.WindowState == FormWindowState.Maximized) { e.Graphics.DrawRectangle(pen, x + 2, y - 2, size - 2, size - 2); }
                    e.Graphics.DrawRectangle(pen, x, y, size - 2, size - 2);
                }
            }
        }
    }

    public class CaptionMenuStrip : MenuStrip
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84)
            {
                long xy = m.LParam.ToInt64();
                var point = PointToClient(new Point((short)xy, (short)(xy >> 16)));
                if (GetItemAt(point) == null && !OverflowButton.Bounds.Contains(point)) m.Result = (IntPtr)(-1);
            }
        }
    }
}

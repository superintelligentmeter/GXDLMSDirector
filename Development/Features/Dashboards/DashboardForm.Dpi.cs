// superintelligentmeter contributors, 2026-10-17. Dashboard typography roles across real DPI transitions. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm
    {
        private bool dashboardTypographyDirty;
        // Moving the window to another monitor rescales every card one control at a time, and each step
        // lays out and repaints. Windows tells the cards first (WM_DPICHANGED_BEFOREPARENT, bottom-up), so the
        // first card to hear it pauses layout and drawing of the whole card area; it is resumed once, a moment
        // after the last notification, with the cards laid out again at the new scale.
        private readonly System.Windows.Forms.Timer dpiSettle = new() { Interval = 150 };
        private readonly List<Control> dpiPaused = new();
        private bool dpiFrozen, dpiTimerHooked;
        private const int WM_SETREDRAW = 0x000B;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private void HookDpiFreeze(Control root)
        {
            if (!dpiTimerHooked) { dpiTimerHooked = true; dpiSettle.Tick += (_, _) => ReleaseDpiFreeze(); }
            root.DpiChangedBeforeParent += (_, _) => FreezeForDpi();
            foreach (Control child in root.Controls) HookDpiFreeze(child);
        }
        private void FreezeForDpi()
        {
            if (IsDisposed || widgets == null || !widgets.IsHandleCreated) return;
            if (!dpiFrozen)
            {
                dpiFrozen = true;
                SendMessage(widgets.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                PauseLayout(widgets);
            }
            dpiSettle.Stop(); dpiSettle.Start();
        }
        private void PauseLayout(Control control)
        {
            control.SuspendLayout(); dpiPaused.Add(control);
            foreach (Control child in control.Controls) if (child.Controls.Count > 0) PauseLayout(child);
        }
        private void ReleaseDpiFreeze()
        {
            dpiSettle.Stop();
            if (!dpiFrozen) return;
            dpiFrozen = false;
            for (int i = dpiPaused.Count - 1; i >= 0; --i) if (!dpiPaused[i].IsDisposed) dpiPaused[i].ResumeLayout(false);
            dpiPaused.Clear();
            if (IsDisposed || !widgets.IsHandleCreated) return;
            SendMessage(widgets.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            ResizeWidgets(); widgets.Invalidate(true);
        }
        protected override void OnDpiChangedBeforeParent(EventArgs e)
        {
            base.OnDpiChangedBeforeParent(e);
            FreezeForDpi();
        }
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            dashboardTypographyDirty = true; QueueWorkspaceLayout();
        }
        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            dashboardTypographyDirty = true; QueueWorkspaceLayout();
        }
        private bool DashboardTypographyMatchesOwner()
        {
            var body = ReferenceEquals(DialogOwner, this) ? Font : DialogOwner.Font;
            float ratio = body.SizeInPoints / Gurux.DLMS.UI.ApplicationTypography.BodyFontPoints;
            return headingFont != null && valueFont != null && Math.Abs(Font.SizeInPoints - body.SizeInPoints) < 0.01f
                && Math.Abs(headingFont.SizeInPoints - 15 * ratio) < 0.01f && Math.Abs(valueFont.SizeInPoints - 19 * ratio) < 0.01f;
        }
        private void RestoreDashboardTypography()
        {
            dashboardTypographyDirty = false;
            // Match the application's CURRENT font. Resetting to the startup 9pt
            // after a DPI change made this module larger than the original UI.
            var owner = DialogOwner;
            var body = ReferenceEquals(owner, this) ? Font : owner.Font;
            float ratio = body.SizeInPoints / Gurux.DLMS.UI.ApplicationTypography.BodyFontPoints;
            var oldHeading = headingFont; var oldStrong = strongFont; var oldValue = valueFont;
            headingFont = new Font(body.FontFamily, 15 * ratio, FontStyle.Bold);
            strongFont = new Font(body, FontStyle.Bold);
            valueFont = new Font(body.FontFamily, 19 * ratio, FontStyle.Bold);
            void Restore(Control control)
            {
                bool bold = control.Font.Bold;
                var target = ReferenceEquals(control, heading) ? headingFont : Equals(control.Tag, "DashboardValue") ? valueFont : bold ? strongFont : body;
                // Assigning an equal font still re-lays out the control, so only change it when it differs.
                if (!control.Font.Equals(target)) control.Font = target;
                if (control is TileSurface tile) tile.Invalidate();
                if (control is DashboardButton button)
                {
                    button.MinimumSize = new Size(Scale(74), Scale(32));
                    button.Size = new Size(DashboardCommandWidth(button), Scale(32));
                }
                foreach (Control child in control.Controls) Restore(child);
            }
            Font = body;
            foreach (Control child in Controls) Restore(child);
            oldHeading?.Dispose(); oldStrong?.Dispose(); oldValue?.Dispose();
            if (objectPicker?.SmallImageList != null) objectPicker.SmallImageList.ImageSize = new Size(1, Scale(28));
            // Font changes also change command widths. Keep the title's label
            // bounds out of the button row even before the next window resize.
            LayoutHeader(); AlignMonitoring();
        }
    }
}

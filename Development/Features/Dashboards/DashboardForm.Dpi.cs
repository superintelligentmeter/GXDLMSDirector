// Intelmeter contributors, 2026-10-17. Dashboard typography roles across real DPI transitions. GPL-2.0-only.
using System;
using System.Drawing;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm
    {
        private bool dashboardTypographyDirty;
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
                control.Font = ReferenceEquals(control, heading) ? headingFont : Equals(control.Tag, "DashboardValue") ? valueFont : bold ? strongFont : body;
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

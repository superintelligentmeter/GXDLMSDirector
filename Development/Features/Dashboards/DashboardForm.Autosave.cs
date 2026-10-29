// Intelmeter contributors, 2026-10-17. Dashboard-only automatic persistence. GPL-2.0-only.
using System;
using System.Drawing;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm
    {
        private readonly Timer autoSaveTimer = new() { Interval = 400 };
        private LinkLabel saveIndicator;
        private bool autoSaveFailed, cardResizeInProgress;
        private void MarkChanged()
        {
            dirty = true;
            if (!autoSaveFailed) UpdateSaveIndicator("Saving…", false);
            autoSaveTimer.Stop(); autoSaveTimer.Start();
        }
        private bool FlushAutoSave()
        {
            autoSaveTimer.Stop();
            if (!dirty) return true;
            try { Save(); return true; }
            catch (Exception ex)
            {
                autoSaveFailed = true; UpdateSaveIndicator("Not saved · Retry", true);
                dashboardTips.SetToolTip(saveIndicator, "Changes remain in memory. " + ex.Message);
                LayoutWorkspacePanels(); QueueWorkspaceLayout();
                return false;
            }
        }
        private void UpdateSaveIndicator(string text, bool failed)
        {
            if (saveIndicator == null) return;
            saveIndicator.Text = text; saveIndicator.ForeColor = saveIndicator.LinkColor = failed ? Color.Firebrick : muted;
            saveIndicator.LinkArea = failed ? new LinkArea(0, text.Length) : new LinkArea(0, 0);
            saveIndicator.TabStop = failed;
            saveIndicator.AccessibleDescription = failed ? "Changes have not been saved. Activate to retry." : text;
            if (!failed) dashboardTips.SetToolTip(saveIndicator, text);
        }
    }
}

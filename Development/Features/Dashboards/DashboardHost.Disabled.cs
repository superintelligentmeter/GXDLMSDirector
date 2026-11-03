// superintelligentmeter contributors, 2026-10-17. No-op host adapter for feature-free builds. GPL-2.0-only.
using System.Drawing;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    partial class MainForm
    {
        private void InitializeDashboards() { }
        private bool TrySelectDashboardNode(TreeNode node) => false;
        private void OnDashboardTreeSelection() { }
        private void RestoreDashboardNavigation() { }
        private bool TryHandleDashboardDelete() => false;
        private bool TryShowDashboardMenu(TreeNode node, Point location) => false;
        private void RefreshDashboardNavigation() { }
        private bool PrepareDashboardsForClose() => true;
        // Dashboards stay in the file untouched while the feature is off.
        private string dashboardRawJson;
        private void ClearDashboardWorkspace() { dashboardRawJson = null; }
        private void LoadDashboardJson(string json) { dashboardRawJson = json; }
        private string DashboardJsonForSave() => dashboardRawJson;
        private void FlushDashboardChanges() { }
        private void YieldDashboardToUser() { }
        private System.Threading.Tasks.Task PauseDashboardRead() => System.Threading.Tasks.Task.CompletedTask;
        private string DashboardTracePrefix(GXDLMSDevice meter) => "";
    }
}

// Intelmeter contributors, 2026-10-17. No-op host adapter for feature-free builds. GPL-2.0-only.
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
        private bool PrepareDashboardsForClose() => true;
        private void ClearDashboardWorkspace() { }
    }
}

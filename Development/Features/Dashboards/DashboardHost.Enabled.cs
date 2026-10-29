// Intelmeter contributors, 2026-10-17. Embedded dashboard workspace and read coordination. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Gurux.DLMS.Objects;

namespace GXDLMSDirector
{
    partial class MainForm
    {
        private Panel dashboardWorkspace;
        private bool dashboardScreenActive;
        private Panel objectWorkspacePage, dashboardWorkspacePage;
        private Panel dashboardHost;
        private Label dashboardEmpty;
        private DashboardForm activeDashboard;
        internal string DashboardStoragePath;
        private readonly Dictionary<Control, bool> dashboardCommandState = new();
        private bool dashboardReadActive;
        private sealed class DashboardTreeNode : TreeNode
        {
            internal DashboardDefinition Definition;
            internal bool Create;
            internal DashboardTreeNode(string text, GXDLMSDevice meter, DashboardDefinition definition = null, bool create = false) : base(text)
            {
                Tag = null; Definition = definition; Create = create; Name = "DashboardNavigation";
                ImageKey = SelectedImageKey = create ? "dashboard-new" : definition == null ? "dashboards" : "dashboard";
            }
        }
        private void SyncDashboardTree(GXDLMSDevice meter, DashboardForm dashboard)
        {
            var root = ObjectTree.Nodes.Cast<TreeNode>().OfType<DashboardTreeNode>().FirstOrDefault();
            if (root == null) { root = new DashboardTreeNode("Dashboards", null); ObjectTree.Nodes.Add(root); }
            if (dashboard == null) return;
            // Keep existing nodes stable so selection changes cannot recursively rebuild the tree.
            var definitions = dashboard.Definitions.ToArray();
            foreach (var node in root.Nodes.Cast<TreeNode>().OfType<DashboardTreeNode>().ToArray())
                if (!node.Create && !definitions.Contains(node.Definition)) node.Remove();
            foreach (var d in definitions)
            {
                var node = root.Nodes.Cast<TreeNode>().OfType<DashboardTreeNode>().FirstOrDefault(n => ReferenceEquals(n.Definition, d));
                if (node == null) root.Nodes.Insert(Math.Max(0, root.Nodes.Count - 1), new DashboardTreeNode(d.Name, meter, d)); else node.Text = d.Name;
            }
            if (!root.Nodes.Cast<TreeNode>().OfType<DashboardTreeNode>().Any(n => n.Create)) root.Nodes.Add(new DashboardTreeNode("New dashboard", meter, null, true));
            root.Expand();
        }

        private void InitializeDashboards()
        {
            if (!DashboardFeature.Enabled) return;
            // Keep all existing editors, including their event handlers, in the Objects page.
            var workspace = tabControl2.Parent;
            dashboardWorkspace = new Panel { Name = "DashboardWorkspace", Dock = DockStyle.Fill };
            objectWorkspacePage = new Panel { Name = "ObjectWorkspace", Dock = DockStyle.Fill, BackColor = ShellPresentation.Surface };
            dashboardWorkspacePage = new Panel { Name = "DashboardWorkspacePage", Dock = DockStyle.Fill, Visible = false, BackColor = ShellPresentation.Surface };
            foreach (var control in new Control[] { DeviceListView, DeviceInfoView, tabControl2, ObjectValueView })
                objectWorkspacePage.Controls.Add(control);
            dashboardWorkspace.Controls.AddRange(new Control[] { objectWorkspacePage, dashboardWorkspacePage });
            workspace.Controls.Add(dashboardWorkspace);
            dashboardWorkspace.BringToFront();
            dashboardHost = new Panel { Dock = DockStyle.Fill };
            dashboardEmpty = new Label { Dock = DockStyle.Fill, Padding = new Padding(20), Text = "Select Dashboards in the object tree to create or open a dashboard." };
            dashboardHost.Controls.Add(dashboardEmpty);
            dashboardWorkspacePage.Controls.Add(dashboardHost);
            contextMenuStrip1.Items.Add(new ToolStripSeparator());
            contextMenuStrip1.Items.Add("Add to dashboard...", null, (_, _) => OpenDashboards(true));
            SyncDashboardTree(null, null);
            Disposed += (_, _) => activeDashboard?.Dispose();
        }
        private void RestoreDashboardNavigation() { if (dashboardWorkspace != null) SyncDashboardTree(null, null); }
        private bool TrySelectDashboardNode(TreeNode node)
        {
            if (node is not DashboardTreeNode) return false;
            OnDashboardTreeSelection(); return true;
        }
        private bool TryHandleDashboardDelete()
        {
            if (ObjectTree.SelectedNode is not DashboardTreeNode) return false;
            MessageBox.Show(this, "Use the dashboard More menu to delete a dashboard.", "Dashboards"); return true;
        }
        private GXDLMSDevice DashboardSelectedMeter()
        {
            for (var node = ObjectTree.SelectedNode; node != null; node = node.Parent)
            {
                if (node.Tag is GXDLMSDevice device) return device;
                if (node.Tag is GXDLMSObject obj && obj.Parent?.Tag is GXDLMSDevice owner) return owner;
            }
            return null;
        }
        private void OpenDashboards(bool addSelected)
        {
            try
            {
                ShowDashboardScreen(true);
                UpdateDashboardWorkspace(addSelected);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Dashboards", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void OnDashboardTreeSelection()
        {
            if (dashboardWorkspace == null) return;
            if (ObjectTree.SelectedNode is DashboardTreeNode node)
            {
                ShowDashboardScreen(true);
                UpdateDashboardWorkspace(false);
                UpdateDeviceUI(null, DeviceState.None);
                if (node.Create) activeDashboard?.NewDashboard(); else activeDashboard?.SelectDashboard(node.Definition);
                return;
            }
            ShowDashboardScreen(false);
            UpdateDashboardWorkspace(false);
        }
        private void ShowDashboardScreen(bool dashboard)
        {
            if (dashboardWorkspace == null) return;
            dashboardScreenActive = dashboard;
            objectWorkspacePage.Visible = !dashboard;
            dashboardWorkspacePage.Visible = dashboard;
            (dashboard ? dashboardWorkspacePage : objectWorkspacePage).BringToFront();
            if (!dashboard) activeDashboard?.SetWorkspaceActive(false);
        }
        private void UpdateDashboardWorkspace(bool addSelected)
        {
            if (dashboardWorkspace == null) return;
            if (!dashboardScreenActive)
            {
                activeDashboard?.SetWorkspaceActive(false);
                return;
            }
            DisplayDashboard(DashboardSelectedMeter(), addSelected ? ObjectTree.SelectedNode?.Tag as GXDLMSObject : null);
        }
        private GXDLMSDevice[] DashboardMeters()
        {
            return Devices.Cast<object>().OfType<GXDLMSDevice>().ToArray();
        }
        private void EnsureDashboardMeterIds()
        {
            var used = new HashSet<string>();
            foreach (var meter in DashboardMeters())
                if (string.IsNullOrEmpty(meter.DashboardId) || !used.Add(meter.DashboardId))
                {
                    meter.DashboardId = Guid.NewGuid().ToString("N"); used.Add(meter.DashboardId); SetDirty(true);
                }
        }
        private void DisplayDashboard(GXDLMSDevice device, GXDLMSObject initial = null)
        {
            if (dashboardWorkspace == null) return;
            EnsureDashboardMeterIds();
            if (activeDashboard == null)
            {
                activeDashboard = new DashboardForm(device, null, () => BeginDashboardRead(null), EndDashboardRead, DashboardMeters, DashboardStoragePath)
                {
                    TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill,
                    MinimumSize = Size.Empty, ShowInTaskbar = false
                };
                dashboardHost.Controls.Add(activeDashboard);
                activeDashboard.DefinitionsChanged += () => SyncDashboardTree(null, activeDashboard);
            }
            dashboardEmpty.Visible = false;
            activeDashboard.SetWorkspaceActive(true);
            activeDashboard.Show(); activeDashboard.BringToFront();
            activeDashboard.InitializeWorkspace(initial);
            activeDashboard.RefreshMeters();
            SyncDashboardTree(null, activeDashboard);
        }
        private bool BeginDashboardRead(GXDLMSDevice device)
        {
            // UI-thread acquisition happens before the worker starts. The media lock also
            // serializes requests with an already-running keep-alive on the same connection.
            if (IsDisposed || !Enabled || dashboardReadActive ||
                !dashboardScreenActive || activeDashboard == null ||
                (TransactionWork != null && TransactionWork.IsRunning)) return false;
            dashboardReadActive = true;
            dashboardCommandState.Clear();
            foreach (var control in new Control[] { menuStrip1, toolStrip1, objectWorkspacePage, contextMenuStrip1 })
            {
                dashboardCommandState[control] = control.Enabled; control.Enabled = false;
            }
            return true;
        }
        private void EndDashboardRead()
        {
            dashboardReadActive = false;
            foreach (var item in dashboardCommandState)
                if (!item.Key.IsDisposed) item.Key.Enabled = item.Value;
            dashboardCommandState.Clear();
        }
        private bool PrepareDashboardsForClose()
        {
            activeDashboard?.SetWorkspaceActive(false);
            if (dashboardReadActive || activeDashboard?.IsReading == true)
            {
                StatusLbl.Text = "Waiting for the dashboard read to finish. Retry after the current request completes.";
                return false;
            }
            if (activeDashboard != null && !activeDashboard.PrepareToClose()) return false;
            return true;
        }
        private void ClearDashboardWorkspace()
        {
            activeDashboard?.Dispose(); activeDashboard = null;
            foreach (var root in ObjectTree.Nodes.Cast<TreeNode>().OfType<DashboardTreeNode>().ToArray()) root.Remove();
            if (dashboardWorkspace != null)
            {
                ShowDashboardScreen(false);
                dashboardEmpty.Visible = true;
                SyncDashboardTree(null, null);
            }
        }
    }
}

// superintelligentmeter contributors, 2026-10-17. Embedded dashboard workspace and read coordination. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Gurux.Common;
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
        private DashboardWorkspaceStore dashboardStore;
        private DashboardWorkspaceStore NewDashboardStore()
        {
            var created = new DashboardWorkspaceStore();
            created.Changed += () => { if (!IsDisposed) SetDirty(true); };
            return created;
        }
        // Called when a .gxc file is opened: dashboards come from that file only.
        private void LoadDashboardJson(string json)
        {
            activeDashboard?.Dispose(); activeDashboard = null;
            dashboardStore = NewDashboardStore();
            dashboardStore.Load(json);
        }
        private string DashboardJsonForSave() => dashboardStore?.ToJson();
        private void FlushDashboardChanges() => activeDashboard?.FlushPending();
        private void YieldDashboardToUser() => activeDashboard?.YieldToUser();
        private bool dashboardReadActive;
        private System.Threading.Tasks.TaskCompletionSource<bool> dashboardReadCompletion;
        private System.Threading.Tasks.Task PauseDashboardRead()
        {
            YieldDashboardToUser();
            return dashboardReadCompletion?.Task ?? System.Threading.Tasks.Task.CompletedTask;
        }
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
        private void SyncDashboardTree(GXDLMSDevice meter, DashboardForm dashboard, bool expandNew = false)
        {
            var root = ObjectTree.Nodes.Cast<TreeNode>().OfType<DashboardTreeNode>().FirstOrDefault();
            bool added = false;
            if (root == null) { root = new DashboardTreeNode("Dashboards", null); ObjectTree.Nodes.Add(root); added = true; }
            if (dashboard == null) return;
            // Keep existing nodes stable so selection changes cannot recursively rebuild the tree.
            var definitions = dashboard.Definitions.ToArray();
            foreach (var node in root.Nodes.Cast<TreeNode>().OfType<DashboardTreeNode>().ToArray())
                if (!node.Create && !definitions.Contains(node.Definition)) node.Remove();
            foreach (var d in definitions)
            {
                var node = root.Nodes.Cast<TreeNode>().OfType<DashboardTreeNode>().FirstOrDefault(n => ReferenceEquals(n.Definition, d));
                if (node == null) { root.Nodes.Add(new DashboardTreeNode(d.Name, meter, d)); added = true; }
                else node.Text = d.Name;
            }
            // Open the branch only when the user adds a dashboard; loading a file leaves it collapsed.
            if (added && expandNew) root.Expand();
            ObjectTree.Invalidate();
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
        private void CreateDashboardFromTree()
        {
            ShowDashboardScreen(true);
            UpdateDashboardWorkspace(false);
            activeDashboard?.NewDashboard();
        }
        private void RestoreDashboardNavigation() { if (dashboardWorkspace != null) { SyncDashboardTree(null, null); RefreshDashboardNavigation(); } }
        private bool TrySelectDashboardNode(TreeNode node)
        {
            if (node is not DashboardTreeNode) return false;
            OnDashboardTreeSelection(); return true;
        }
        private bool TryHandleDashboardDelete()
        {
            if (ObjectTree.SelectedNode is not DashboardTreeNode node) return false;
            if (node.Definition != null) activeDashboard?.DeleteDashboard(node.Definition);
            return true;
        }
        // Dedicated menu for the Dashboards branch: only commands that apply to dashboards.
        private bool TryShowDashboardMenu(TreeNode treeNode, Point location)
        {
            if (treeNode is not DashboardTreeNode node || dashboardWorkspace == null) return false;
            ObjectTree.SelectedNode = node;
            EnsureDashboardForm(null);
            var menu = new ContextMenuStrip();
            menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
            var dashboard = activeDashboard;
            bool busy = dashboard == null || dashboard.IsReading || dashboardReadActive;
            ToolStripItem Add(string text, Action action, bool enabled = true)
            {
                var item = menu.Items.Add(text, null, (_, _) => action());
                item.Enabled = enabled && !busy; return item;
            }
            if (node.Definition != null)
            {
                Add("Rename...", () => dashboard.RenameDashboard(node.Definition));
                Add("Delete", () => dashboard.DeleteDashboard(node.Definition));
                menu.Items.Add(new ToolStripSeparator());
                Add("New dashboard...", CreateDashboardFromTree);
            }
            else
            {
                Add("New dashboard...", CreateDashboardFromTree);
            }
            menu.Show(ObjectTree, location);
            return true;
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
                AttachDashboardTrace();
                if (node.Create) activeDashboard?.NewDashboard(); else activeDashboard?.SelectDashboard(node.Definition);
                return;
            }
            ShowDashboardScreen(false);
            UpdateDashboardWorkspace(false);
        }
        // Dashboard reads go through the same communicator as every other read, so their traffic is shown in the
        // trace view while the dashboard is on screen. Only the selected meter is traced otherwise.
        private void AttachDashboardTrace() { foreach (var meter in DashboardMeters()) meter.OnTrace = OnTrace; }
        private void DetachDashboardTrace() { foreach (var meter in DashboardMeters()) if (!ReferenceEquals(meter, selectedDevice)) meter.OnTrace = null; }
        private string DashboardTracePrefix(GXDLMSDevice meter) => dashboardScreenActive && meter != null ? "[" + meter.Name + "] " : "";
        private bool ConnectDashboardMeters(GXDLMSDevice[] meters)
        {
            if (IsDisposed || dashboardReadActive || (TransactionWork != null && TransactionWork.IsRunning)) return false;
            AttachDashboardTrace();
            ClearTrace();
            TransactionWork = CreateUserWork(this, OnAsyncStateChange, ConnectDashboardMeterList, OnError, null, new object[] { meters });
            TransactionWork.Start();
            return true;
        }
        private bool DisconnectDashboardMeters(GXDLMSDevice[] meters)
        {
            if (IsDisposed || dashboardReadActive || (TransactionWork != null && TransactionWork.IsRunning)) return false;
            AttachDashboardTrace();
            var devices = new GXDLMSDeviceCollection();
            devices.AddRange(meters);
            TransactionWork = CreateUserWork(this, OnAsyncStateChange, Disconnect, OnError, null, new object[] { devices });
            TransactionWork.Start();
            return true;
        }
        private void ConnectDashboardMeterList(object sender, GXAsyncWork work, object[] parameters)
        {
            // One meter at a time through the regular connect path, so objects are read on a first connection.
            foreach (var meter in (GXDLMSDevice[])parameters[0])
                if (!meter.Media.IsOpen) Connect(sender, work, new object[] { meter });
        }
        private void ShowDashboardScreen(bool dashboard)
        {
            if (dashboardWorkspace == null) return;
            dashboardScreenActive = dashboard;
            if (dashboard) AttachDashboardTrace(); else DetachDashboardTrace();
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
            EnsureDashboardForm(device);
            dashboardEmpty.Visible = false;
            activeDashboard.SetWorkspaceActive(true);
            activeDashboard.Show(); activeDashboard.BringToFront();
            activeDashboard.InitializeWorkspace(initial);
            activeDashboard.RefreshMeters();
            SyncDashboardTree(null, activeDashboard);
        }
        private void EnsureDashboardForm(GXDLMSDevice device)
        {
            if (activeDashboard != null) return;
            activeDashboard = new DashboardForm(device, null, () => BeginDashboardRead(null), EndDashboardRead, DashboardMeters, null, (dashboardStore ??= NewDashboardStore()), ConnectDashboardMeters, DisconnectDashboardMeters, () => TransactionWork != null && TransactionWork.IsRunning)
            {
                TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill,
                MinimumSize = Size.Empty, ShowInTaskbar = false
            };
            dashboardHost.Controls.Add(activeDashboard);
            activeDashboard.DefinitionsChanged += () => SyncDashboardTree(null, activeDashboard, true);
        }
        // List the saved dashboards in the tree right away, without opening the dashboard workspace.
        private void RefreshDashboardNavigation()
        {
            if (dashboardWorkspace == null) return;
            EnsureDashboardForm(null);
            EnsureDashboardMeterIds();
            activeDashboard.LoadNavigation();
            SyncDashboardTree(null, activeDashboard);
        }
        private bool BeginDashboardRead(GXDLMSDevice device)
        {
            // UI-thread acquisition happens before the worker starts. The media lock also
            // serializes requests with an already-running keep-alive on the same connection.
            if (IsDisposed || !Enabled || dashboardReadActive || System.Threading.Volatile.Read(ref pendingUserMeterOperations) != 0 ||
                !dashboardScreenActive || activeDashboard == null ||
                (TransactionWork != null && TransactionWork.IsRunning)) return false;
            // The application stays usable during a read: nothing is disabled. A meter operation started meanwhile
            // asks the dashboard to yield (see YieldDashboardToUser).
            dashboardReadCompletion = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
            dashboardReadActive = true;
            return true;
        }
        private void EndDashboardRead()
        {
            dashboardReadActive = false;
            dashboardReadCompletion?.TrySetResult(true);
            dashboardReadCompletion = null;
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
            dashboardStore = null;
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

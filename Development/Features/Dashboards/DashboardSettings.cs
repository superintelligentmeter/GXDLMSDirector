// Intelmeter contributors, 2026-10-17. Optional Settings extension. GPL-2.0-only.
using System;
using System.Configuration;
using System.Drawing;
using System.Windows.Forms;
using Gurux.Common;
namespace GXDLMSDirector.Properties
{
    internal sealed partial class Settings
    {
        [UserScopedSetting, DefaultSettingValue("")]
        public string DashboardPreference
        {
            get => (string)this[nameof(DashboardPreference)];
            set => this[nameof(DashboardPreference)] = value;
        }
    }
}
namespace GXDLMSDirector
{
    // Discovered by Gurux's existing Settings extension mechanism.
    public sealed class DashboardSettingsPage : Form, IGXSettingsPage
    {
        private readonly CheckBox enabled = new() { Text = "Enable dashboards", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 8), AccessibleName = "Enable dashboards" };
        private readonly Func<bool> readEnabled;
        private readonly Action<bool> saveEnabled;
        public DashboardSettingsPage() : this(() => DashboardFeature.Enabled, value =>
        {
            Properties.Settings.Default.DashboardPreference = value.ToString(); Properties.Settings.Default.Save();
        }) { }
        internal DashboardSettingsPage(Func<bool> readEnabled, Action<bool> saveEnabled)
        {
            this.readEnabled = readEnabled; this.saveEnabled = saveEnabled;
            Font = Gurux.DLMS.UI.ApplicationTypography.BodyFont;
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = Color.White };
            var help = new Label { Text = "Create dashboards from objects across your meters.\n\nChanges take effect after restarting GXDLMSDirector.\nYour saved dashboards are kept when this feature is disabled.", Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
            content.Controls.Add(help); content.Controls.Add(enabled); Controls.Add(content);
            enabled.CheckedChanged += (_, _) => Dirty = true;
        }
        public string Caption => "Dashboards";
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Dirty { get; set; }
        public void Initialize() { enabled.Checked = readEnabled(); Dirty = false; }
        public void Apply()
        {
            if (!Dirty) return;
            saveEnabled(enabled.Checked); Dirty = false;
        }
    }
}

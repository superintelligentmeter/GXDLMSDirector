// Intelmeter contributors, 2026-10-17. Optional dashboard identity compatibility. GPL-2.0-only.
using System.ComponentModel;
namespace GXDLMSDirector
{
    public partial class GXDLMSDevice
    {
        // Keep this serialized metadata even in feature-free builds so saving a meter file
        // while dashboards are disabled preserves bindings when the feature is re-enabled.
        [Browsable(false)]
        public string DashboardId { get; set; } = "";
#if DASHBOARDS
        internal bool IsKeepAliveRunning => KeepAlive.Enabled;
#endif
    }
}

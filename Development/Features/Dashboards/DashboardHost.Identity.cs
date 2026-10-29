// Intelmeter contributors, 2026-10-17. Identity survives feature toggling. GPL-2.0-only.
using System;
namespace GXDLMSDirector
{
    partial class MainForm
    {
        private static void ResetDashboardIdentity(object source)
        {
            // Clone a saved ID only after assigning a new one, including disabled builds.
            if (source is GXDLMSDevice meter && !string.IsNullOrEmpty(meter.DashboardId)) meter.DashboardId = Guid.NewGuid().ToString("N");
        }
    }
}

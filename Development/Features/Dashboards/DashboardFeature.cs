// Intelmeter contributors, 2026-10-17. Dashboard feature startup switch. GPL-2.0-only.
using System;
namespace GXDLMSDirector
{
    internal static class DashboardFeature
    {
        internal const string SwitchName = "GXDLMSDirector.Dashboards.Enabled";
        internal static bool Enabled => ResolveEnabled(Properties.Settings.Default.DashboardPreference,
            !AppContext.TryGetSwitch(SwitchName, out bool enabled) || enabled);
        internal static bool ResolveEnabled(string preference, bool defaultValue) => bool.TryParse(preference, out bool enabled) ? enabled : defaultValue;
    }
}

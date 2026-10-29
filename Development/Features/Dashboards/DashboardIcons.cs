// Intelmeter contributors, 2026-10-17. Optional dashboard navigation icons. GPL-2.0-only.
using System.Collections.Generic;
namespace GXDLMSDirector
{
    public static partial class TreeIcons
    {
        static partial void RegisterFeatureGlyphs(Dictionary<string, Shape[]> glyphs)
        {
            glyphs["dashboards"] = new[] { R(true,2,2,8,8), R(false,14,2,8,8), R(false,2,14,8,8), R(true,14,14,8,8) };
            glyphs["dashboard"] = new[] { R(false,2,3,20,18), P(false,2,8,22,8), P(true,6,17,6,13), P(true,11,17,11,10), P(true,16,17,16,12) };
            glyphs["dashboard-new"] = new[] { P(false,11,21,2,21,2,3,22,3,22,11), P(false,2,8,22,8), P(true,17,12,17,22), P(true,12,17,22,17) };
        }
        static partial void AppendFeatureIcons(List<string> names)
        {
            if (DashboardFeature.Enabled) names.AddRange(new[] { "dashboards", "dashboard", "dashboard-new" });
        }
    }
}

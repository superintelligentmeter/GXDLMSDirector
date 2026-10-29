// Intelmeter contributors, 2026-10-17. Per-refresh reference indexes, never object copies. GPL-2.0-only.
using System.Collections.Generic;
using Gurux.DLMS.Objects;
namespace GXDLMSDirector
{
    internal sealed class DashboardMeterCatalog
    {
        private readonly Dictionary<string, GXDLMSDevice> meters = new();
        private readonly Dictionary<string, Dictionary<(int Class, string Obis), GXDLMSObject>> objects = new();
        internal DashboardMeterCatalog(IEnumerable<GXDLMSDevice> source)
        {
            foreach (var meter in source) if (!string.IsNullOrEmpty(meter.DashboardId)) meters.TryAdd(meter.DashboardId, meter);
        }
        internal GXDLMSDevice Meter(string id) => id != null && meters.TryGetValue(id, out var meter) ? meter : null;
        internal GXDLMSObject Resolve(DashboardBinding binding)
        {
            var meter = Meter(binding.MeterId); if (meter == null) return null;
            if (!objects.TryGetValue(binding.MeterId, out var index))
            {
                index = new();
                foreach (GXDLMSObject obj in meter.Objects) index.TryAdd(((int)obj.ObjectType, obj.LogicalName), obj);
                objects.Add(binding.MeterId, index);
            }
            return index.TryGetValue((binding.ClassId, binding.Obis), out var result) ? result : null;
        }
    }
}

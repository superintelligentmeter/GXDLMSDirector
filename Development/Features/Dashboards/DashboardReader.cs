// Intelmeter contributors, 2026-10-17. Testable read-only dashboard sampling. GPL-2.0-only.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Gurux.DLMS.Objects;
[assembly: InternalsVisibleTo("DashboardTests")]
namespace GXDLMSDirector
{
    internal sealed record DashboardReading(string Key, string Text, string Unit, double? Number, string Error, DateTime Time);
    internal static class DashboardReader
    {
        internal static async System.Threading.Tasks.Task<List<DashboardReading>> ReadMeters(
            IEnumerable<DashboardBinding> bindings, Func<string, DashboardBinding[], List<DashboardReading>> readMeter, Func<bool> cancelled)
        {
            // Bound transport concurrency even when a dashboard contains many meters.
            using var slots = new System.Threading.SemaphoreSlim(4);
            var jobs = bindings.GroupBy(b => b.MeterId).Select(async group =>
            {
                var requests = group.GroupBy(b => b.Key).Select(g => g.First()).ToArray();
                await slots.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (cancelled()) return new List<DashboardReading>();
                    return await System.Threading.Tasks.Task.Run(() => readMeter(group.Key, requests)).ConfigureAwait(false);
                }
                catch (Exception ex) { return requests.Select(b => new DashboardReading(b.Key, null, null, null, ex.Message, DateTime.UtcNow)).ToList(); }
                finally { slots.Release(); }
            }).ToArray();
            return (await System.Threading.Tasks.Task.WhenAll(jobs).ConfigureAwait(false)).SelectMany(x => x).ToList();
        }
        internal static List<DashboardReading> Read(IEnumerable<DashboardBinding> bindings,
            Func<DashboardBinding, GXDLMSObject> resolve, Func<GXDLMSObject, int, bool> canRead,
            Action<GXDLMSObject, int> read, Func<bool> cancelled)
        {
            var result = new List<DashboardReading>();
            var reads = new HashSet<(GXDLMSObject Object, int Attribute)>();
            void ReadOnce(GXDLMSObject obj, int index)
            {
                if (reads.Contains((obj, index))) return;
                read(obj, index); reads.Add((obj, index));
            }
            foreach (var b in bindings.GroupBy(b => b.Key).Select(g => g.First()))
            {
                if (cancelled()) break;
                try
                {
                    var o = resolve(b) ?? throw new InvalidOperationException("Object missing on this meter");
                    string error = DashboardAccessPolicy.AttributeError(o, b.Attribute, canRead);
                    if (error != null) throw new InvalidOperationException(error);
                    int scaler = DashboardAccessPolicy.ScalerAttribute(o, b.Attribute);
                    // Package UpdateValue applies scaling. Never multiply the returned value again.
                    if (scaler != 0 && !reads.Contains((o, scaler)))
                    {
                        if (!canRead(o, scaler)) throw new InvalidOperationException("Scaler/unit not readable; cannot safely display this value");
                        ReadOnce(o, scaler);
                    }
                    if (cancelled()) break;
                    ReadOnce(o, b.Attribute);
                    var value = o.GetValues()[b.Attribute - 1];
                    string unit = o is GXDLMSRegister reg && b.Attribute == 2 ? Gurux.DLMS.GXDLMSConverter.GetUnit(reg.Unit) : o is GXDLMSDemandRegister dem && (b.Attribute == 2 || b.Attribute == 3) ? Gurux.DLMS.GXDLMSConverter.GetUnit(dem.Unit) : "";
                    double? number = value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : null;
                    if (number.HasValue && !double.IsFinite(number.Value)) number = null;
                    result.Add(new DashboardReading(b.Key, Format(value), unit, number, null, DateTime.UtcNow));
                }
                catch (Exception ex) { result.Add(new DashboardReading(b.Key, null, null, null, ex.Message, DateTime.UtcNow)); }
            }
            return result;
        }
        private static string Format(object value, int depth = 0)
        {
            if (value == null) return "—";
            if (value is byte[] bytes) return Convert.ToHexString(bytes.Take(128).ToArray()) + (bytes.Length > 128 ? "…" : "");
            if (value is IEnumerable items && value is not string) return depth > 3 ? "[…]" : "[" + string.Join(", ", items.Cast<object>().Take(32).Select(v => Format(v, depth + 1))) + "]";
            var text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? "—"; return text.Length > 512 ? text.Substring(0, 512) + "…" : text;
        }
    }
}

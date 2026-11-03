// superintelligentmeter contributors, 2026-11-01. Dashboards saved inside the meter (.gxc) file. GPL-2.0-only.
using System;
namespace GXDLMSDirector
{
    /// <summary>Dashboards of the open .gxc file. They are written to the file when it is saved.</summary>
    internal sealed class DashboardWorkspaceStore
    {
        internal DashboardDocument Document = new();
        /// <summary>The original text when it could not be read; it is written back unchanged.</summary>
        internal string Raw;
        internal string LoadError;
        internal event Action Changed;
        internal void Load(string json)
        {
            Document = new(); Raw = null; LoadError = null;
            if (string.IsNullOrWhiteSpace(json)) return;
            try { Document = DashboardStore.Parse(json); }
            catch (Exception ex) { Raw = json; LoadError = ex.Message; }
        }
        internal void Commit() => Changed?.Invoke();
        /// <summary>Text for the .gxc file, or null when there are no dashboards to store.</summary>
        internal string ToJson()
        {
            if (LoadError != null) return Raw;
            if (Document.Dashboards.Count == 0 && Document.MigratedSources.Count == 0) return null;
            return DashboardStore.Serialize(Document);
        }
    }
}

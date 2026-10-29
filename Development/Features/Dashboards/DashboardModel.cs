// Intelmeter contributors, 2026-10-17. Dashboard definitions and persistence. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GXDLMSDirector
{
    public sealed class DashboardCardSize
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
    public sealed class DashboardBinding
    {
        public DashboardCardSize Size { get; set; }
        public string MeterId { get; set; } = "";
        public string MeterName { get; set; } = "";
        public int ClassId { get; set; }
        public string Obis { get; set; } = "";
        public int Attribute { get; set; }
        public string Label { get; set; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public string Key => MeterId + ":" + ClassId + ":" + Obis + ":" + Attribute;
    }
    public sealed class DashboardWidget
    {
        public DashboardCardSize Size { get; set; }
        public int? LayoutOrder { get; set; }
        public List<int> TileOrder { get; set; } = new();
        public string Title { get; set; } = "Measurements";
        public string Kind { get; set; } = "Values";
        public List<DashboardBinding> Bindings { get; set; } = new();
    }
    public sealed class DashboardDefinition
    {
        public string Name { get; set; } = "Electrical overview";
        public int RefreshSeconds { get; set; } = 5;
        public List<DashboardWidget> Widgets { get; set; } = new();
        public override string ToString() => Name;
    }
    public sealed class DashboardDocument
    {
        public int Version { get; set; } = 2;
        public List<string> MigratedSources { get; set; } = new();
        public List<DashboardDefinition> Dashboards { get; set; } = new();
    }
    public static class DashboardStore
    {
        private static void ValidateSize(DashboardCardSize size)
        {
            if (size != null && (size.Width < 160 || size.Width > 2400 || size.Height < 150 || size.Height > 1600))
                throw new InvalidDataException("Invalid dashboard card size.");
        }
        public static void Validate(DashboardDocument doc)
        {
            if (doc == null || (doc.Version != 1 && doc.Version != 2) || doc.Dashboards == null || doc.Dashboards.Count > 50)
                throw new InvalidDataException("Unsupported dashboard file (expected version 1 or 2, at most 50 dashboards).");
            if (doc.MigratedSources == null || doc.MigratedSources.Count > 1000 || doc.MigratedSources.Any(x => x == null || x.Length > 100)) throw new InvalidDataException("Invalid migration record.");
            foreach (var d in doc.Dashboards)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.Name) || d.Name.Length > 100 || d.RefreshSeconds < 2 || d.RefreshSeconds > 3600 || d.Widgets == null || d.Widgets.Count > 30)
                    throw new InvalidDataException("Invalid dashboard name, refresh interval or widget count.");
                foreach (var w in d.Widgets)
                {
                    if (w == null || string.IsNullOrWhiteSpace(w.Title) || w.Title.Length > 100 || !new[] { "Values", "Trend", "Table" }.Contains(w.Kind) || w.Bindings == null || w.Bindings.Count == 0 || w.Bindings.Count > 32)
                        throw new InvalidDataException("Invalid widget. Select 1–32 attributes and a supported display type.");
                    ValidateSize(w.Size);
                    if (w.LayoutOrder < 0 || w.LayoutOrder > 10000 || w.TileOrder == null ||
                        (w.TileOrder.Count != 0 && (w.Kind != "Values" || w.TileOrder.Count != w.Bindings.Count)) || w.TileOrder.Any(x => x < 0 || x > 10000))
                        throw new InvalidDataException("Invalid dashboard layout order.");
                    if (w.Bindings.Where(b => b != null).Select(b => b.Key).Distinct().Count() != w.Bindings.Count) throw new InvalidDataException("Duplicate attributes in a widget.");
                    foreach (var b in w.Bindings)
                    {
                        if (b != null) ValidateSize(b.Size);
                        if (b == null || b.MeterId == null || b.MeterId.Length > 100 || b.MeterName == null || b.MeterName.Length > 200 || b.ClassId < 1 || b.ClassId > 65535 || b.Attribute < 1 || b.Attribute > 255 || b.Label == null || b.Label.Length > 200 || b.Obis == null || b.Obis.Split('.').Length != 6 || b.Obis.Split('.').Any(x => !byte.TryParse(x, out _)))
                            throw new InvalidDataException("Invalid object binding in dashboard file.");
                    }
                }
            }
        }
        public static DashboardDocument Load(string path)
        {
            if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("Dashboard file exceeds 2 MB.");
            var doc = JsonSerializer.Deserialize<DashboardDocument>(File.ReadAllText(path));
            Validate(doc);
            return doc;
        }
        public static void Save(string path, DashboardDocument doc)
        {
            Validate(doc);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}

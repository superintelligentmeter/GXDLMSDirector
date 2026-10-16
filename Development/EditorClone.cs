// Modified for the Intelmeter fork, 2026-10-16: .NET 10 migration, UI modernization and compatibility fixes. GPL-2.0-only; original notices retained.
using System.IO;
using System.Xml.Serialization;
using Gurux.DLMS.ManufacturerSettings;

namespace GXDLMSDirector
{
    // In-memory editing copies only. No executable object deserialization.
    internal static class EditorClone
    {
        internal static T Copy<T>(T source)
        {
            var overrides = new XmlAttributeOverrides();
            overrides.Add(typeof(GXManufacturer), nameof(GXManufacturer.Removed), new XmlAttributes { XmlIgnore = false });
            overrides.Add(typeof(GXObisCode), nameof(GXObisCode.AttributeIndex), new XmlAttributes { XmlIgnore = false });
            var serializer = new XmlSerializer(typeof(T), overrides);
            using var stream = new MemoryStream();
            serializer.Serialize(stream, source);
            stream.Position = 0;
            return (T)serializer.Deserialize(stream);
        }
    }
}

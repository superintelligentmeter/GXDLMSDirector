// Modified for the superintelligentmeter fork, 2026-10-16: .NET 10 migration, UI modernization and compatibility fixes. GPL-2.0-only; original notices retained.
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
            // Manufacturer files can contain GXDLMSAttribute instances in the
            // base-typed attribute collection. Match the file loader's known type.
            var serializer = new XmlSerializer(typeof(T), overrides,
                new[] { typeof(GXDLMSAttribute) }, null, null);
            using var stream = new MemoryStream();
            serializer.Serialize(stream, source);
            stream.Position = 0;
            return (T)serializer.Deserialize(stream);
        }
    }
}

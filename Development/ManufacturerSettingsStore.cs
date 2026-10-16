using System;
using System.IO;
using System.Xml.Serialization;
using Gurux.DLMS.ManufacturerSettings;

namespace GXDLMSDirector
{
    internal static class ManufacturerSettingsStore
    {
        // Preserve the package's OBX format without its Framework-only ACL call.
        // Existing files retain permissions; new files inherit directory permissions.
        internal static void Save(GXManufacturerCollection manufacturers, string directory)
        {
            if (manufacturers.Count == 0) return;
            Directory.CreateDirectory(directory);
            Type[] types = { typeof(GXManufacturerCollection), typeof(GXManufacturer),
                typeof(GXObisCodeCollection), typeof(GXObisCode), typeof(GXObisValueItem),
                typeof(GXObisValueItemCollection), typeof(GXDLMSAttribute), typeof(GXAttributeCollection) };
            var serializer = new XmlSerializer(typeof(GXManufacturer), types);
            foreach (GXManufacturer manufacturer in manufacturers)
            {
                string name = manufacturer.Identification.ToLower();
                if (manufacturer.Identification == "AUX" || manufacturer.Identification == "CON") name = "_" + name;
                string path = Path.Combine(directory, name + ".obx");
                if (manufacturer.Removed) { File.Delete(path); continue; }
                // Serialize before touching the file so a serialization failure cannot truncate it.
                using var content = new MemoryStream();
                serializer.Serialize(content, manufacturer);
                content.Position = 0;
                using var output = File.Open(path, FileMode.Create, FileAccess.Write);
                content.CopyTo(output);
            }
        }
    }
}

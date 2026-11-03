// Modified by Superintelligent Meter contributors, 2026-10-31: online update and first-run check without the Framework-only ACL call. GPL-2.0-only; original notices retained.
// Modified for the superintelligentmeter fork, 2026-10-16: .NET 10 migration, UI modernization and compatibility fixes. GPL-2.0-only; original notices retained.
using System;
using System.IO;
using System.Net.Http;
using System.Xml;
using System.Xml.Serialization;
using Gurux.DLMS.ManufacturerSettings;

namespace GXDLMSDirector
{
    // Director loads the package's net48 build (its concentrator plugins need the desktop API).
    // There, GXManufacturerCollection's WriteManufacturerSettings, IsFirstRun and
    // UpdateManufactureSettings open each file they write to Everyone with File.GetAccessControl,
    // which exists only on .NET Framework, so on .NET 10 they throw MissingMethodException
    // part-way through. These are the package's steps without that call.
    internal static class ManufacturerSettingsStore
    {
        // Gurux's manufacturer settings feed, as in GXManufacturerCollection.
        private const string Feed = "https://www.gurux.fi/obis/";
        private static readonly HttpClient Http = new HttpClient();

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

        // GXManufacturerCollection.IsFirstRun: true while the file list is missing (it is then
        // created empty) or empty.
        internal static bool IsFirstRun() => IsFirstRun(GXManufacturerCollection.ObisCodesPath);

        internal static bool IsFirstRun(string directory)
        {
            string path = Path.Combine(directory, "files.xml");
            Directory.CreateDirectory(directory);
            if (!File.Exists(path))
            {
                File.Create(path).Dispose();
                return true;
            }
            return new FileInfo(path).Length == 0;
        }

        // GXManufacturerCollection.UpdateManufactureSettings: downloads the file list, then every
        // listed file, into the OBIS folder. An existing file whose content changes is first copied
        // to backup\<new guid>. Returns that backup folder, or null when nothing was backed up.
        internal static string UpdateManufactureSettings() =>
            UpdateManufactureSettings(GXManufacturerCollection.ObisCodesPath, Download);

        internal static string UpdateManufactureSettings(string directory, Func<string, byte[]> download)
        {
            string backupPath = Path.Combine(directory, "backup");
            Directory.CreateDirectory(backupPath);
            backupPath = Path.Combine(backupPath, Guid.NewGuid().ToString());
            byte[] files = download(Feed + "files.xml" + NoCache());
            if (files == null) return null;
            File.WriteAllBytes(Path.Combine(directory, "files.xml"), files);
            var xml = new XmlDocument();
            using (var list = new MemoryStream(files)) xml.Load(list);
            bool backup = false;
            foreach (XmlNode it in xml.ChildNodes[1].ChildNodes)
            {
                string path = Path.Combine(directory, it.InnerText);
                byte[] data = download(Feed + it.InnerText + NoCache());
                if (File.Exists(path) && !data.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
                {
                    Directory.CreateDirectory(backupPath);
                    backup = true;
                    File.Copy(path, Path.Combine(backupPath, it.InnerText));
                }
                File.WriteAllBytes(path, data);
            }
            return backup ? backupPath : null;
        }

        // The package's cache buster, as it sends it.
        private static string NoCache() => "?random = " + Random.Shared.Next();

        // Blocking, like the package's WebClient; the system proxy applies by default.
        private static byte[] Download(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage response = Http.Send(request);
            response.EnsureSuccessStatusCode();
            using var content = new MemoryStream();
            response.Content.ReadAsStream().CopyTo(content);
            return content.ToArray();
        }
    }
}

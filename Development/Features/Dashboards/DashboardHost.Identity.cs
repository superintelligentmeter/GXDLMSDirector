// superintelligentmeter contributors, 2026-10-17. Identity survives feature toggling. GPL-2.0-only.
using System;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
namespace GXDLMSDirector
{
    partial class MainForm
    {
        // Dashboards are stored in the .gxc as one extra, optional element under the root. Older versions of
        // the application skip unknown elements, so a file with dashboards still opens there unchanged.
        // The XML declaration must say UTF-8, as it does when written straight to the file.
        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => new UTF8Encoding(false);
        }
        private const string DashboardElementName = "DashboardWorkspace";
        private static string ReadDashboardJson(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                using var reader = XmlReader.Create(path);
                reader.MoveToContent();
                if (reader.IsEmptyElement) return null;
                reader.Read();
                while (reader.MoveToContent() == XmlNodeType.Element)
                {
                    if (reader.LocalName == DashboardElementName) return reader.ReadElementContentAsString();
                    reader.Skip();
                }
            }
            catch (Exception) { }
            return null;
        }
        private static string InsertDashboardJson(string xml, string json)
        {
            if (string.IsNullOrEmpty(json)) return xml;
            string element = "<" + DashboardElementName + ">" + SecurityElement.Escape(json) + "</" + DashboardElementName + ">";
            string trimmed = xml.TrimEnd();
            if (trimmed.EndsWith("/>", StringComparison.Ordinal))
            {
                // Root without devices is written as one self-closed tag.
                var root = Regex.Match(trimmed, @"<([^\s/>?!]+)");
                if (!root.Success) return xml;
                return trimmed.Substring(0, trimmed.Length - 2) + ">" + element + "</" + root.Groups[1].Value + ">";
            }
            int end = trimmed.LastIndexOf("</", StringComparison.Ordinal);
            return end < 0 ? xml : trimmed.Substring(0, end) + element + trimmed.Substring(end);
        }
        private static void ResetDashboardIdentity(object source)
        {
            // Clone a saved ID only after assigning a new one, including disabled builds.
            if (source is GXDLMSDevice meter && !string.IsNullOrEmpty(meter.DashboardId)) meter.DashboardId = Guid.NewGuid().ToString("N");
        }
    }
}

// Modified for the Intelmeter fork, 2026-10-16: .NET 10 migration, UI modernization and compatibility fixes. GPL-2.0-only; original notices retained.
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GXDLMSDirector
{
    internal static class ApplicationWindowIcons
    {
        private static Icon icon;
        internal static void Install()
        {
            if (icon != null) return;
            using var stream = typeof(ApplicationWindowIcons).Assembly.GetManifestResourceStream("GXDLMSDirector.ApplicationIcon");
            using var source = new Icon(stream);
            icon = (Icon)source.Clone();
            // Modal and modeless forms, including library dialogs, join OpenForms.
            // Keep deliberately hidden icons hidden and leave error-provider icons alone.
            Application.Idle += (_, _) =>
            {
                foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
                    if (!form.IsDisposed && form.TopLevel && !ReferenceEquals(form.Icon, icon)) form.Icon = icon;
            };
        }
    }
}

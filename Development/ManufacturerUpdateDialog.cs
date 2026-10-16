// Modified for the Intelmeter fork, 2026-10-16: .NET 10 migration, UI modernization and compatibility fixes. GPL-2.0-only; original notices retained.
using System.Windows.Forms;

namespace GXDLMSDirector
{
    internal static class ManufacturerUpdateDialog
    {
        internal static DialogResult Confirm(Form owner)
        {
            // Let Windows own the prompt's font, DPI, layout and button sizes.
            return MessageBox.Show(owner,
                Properties.Resources.UpdateManufacturersOnlineTxt,
                Properties.Resources.GXDLMSDirectorTxt,
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
        }
    }
}
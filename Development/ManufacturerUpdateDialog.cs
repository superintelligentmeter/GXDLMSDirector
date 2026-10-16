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
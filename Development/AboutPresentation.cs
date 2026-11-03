// Modified by Superintelligent Meter contributors, 2026-11-01: About box for the fork. GPL-2.0-only; original notices retained.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace GXDLMSDirector
{
    // The About box is Gurux.Common's (the package supplies its dialog, artwork and www.gurux.org link).
    // This only fills in its texts and adds the two repository links; the Gurux copyright and the
    // package stay as they are.
    internal static class AboutPresentation
    {
        internal const string ForkRepository = "https://github.com/superintelligentmeter/GXDLMSDirector";
        internal const string OriginalRepository = "https://github.com/Gurux/GXDLMSDirector";
        internal const string ForkNotice = "Fork modifications© 2026\r\nsuperintelligentmeter contributors.\r\nGPL-2.0-only. Not an official Gurux release.";
        internal const string DialogTypeName = "Gurux.Common.AboutBox1";

        // Our release version and the actual Gurux dependency version have separate lifecycles.
        internal static string DisplayVersion()
        {
            Assembly assembly = typeof(AboutPresentation).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion
                ?? assembly.GetName().Version?.ToString() ?? "";
        }

        internal static string GuruxVersion()
        {
            Assembly assembly = typeof(Gurux.DLMS.GXDLMSClient).Assembly;
            return FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion
                ?? assembly.GetName().Version?.ToString() ?? "";
        }

        internal static void Show(IWin32Window owner)
        {
            var about = new Gurux.Common.GXAboutForm
            {
                Title = "About GXDLMSDirector (superintelligentmeter fork)",
                Application = "GXDLMSDirector — superintelligentmeter fork",
                AboutText = "Source code and project information:",
                CopyrightText = Properties.Resources.CopyrightTxt + "\r\n" + ForkNotice,
            };
            // The package shows its dialog modally from ShowAbout; adjust it once it is showing.
            int attempts = 0;
            using var timer = new System.Windows.Forms.Timer { Interval = 20 };
            timer.Tick += (_, _) =>
            {
                Form dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().FullName == DialogTypeName);
                if (dialog != null && dialog.Visible && dialog.IsHandleCreated)
                {
                    timer.Stop();
                    // After the messages already queued, so a DPI change from being placed on another
                    // monitor has been applied before the layout is adjusted.
                    dialog.BeginInvoke((Action)(() => Customize(dialog)));
                }
                else if (++attempts > 100) timer.Stop();
            };
            timer.Start();
            about.ShowAbout(owner, DisplayVersion());
        }

        // Moves the copyright up under the version, gives the artwork panel two more link rows and
        // lists the fork repository, the original repository and www.gurux.org, one per row.
        internal static void Customize(Form dialog)
        {
            if (dialog.IsDisposed) return;
            Control Find(string name) => dialog.Controls.Find(name, true).FirstOrDefault();
            var version = Find("VersionLbl"); var copyright = Find("CopyrightTextLbl");
            var panel = Find("panel1") as Panel; var aboutText = Find("AboutTextLbl"); var gurux = Find("linkLabel1") as LinkLabel;
            if (version == null || copyright == null || panel == null || aboutText == null || gurux == null) return;
            int pitch = gurux.Top - aboutText.Top;
            if (pitch <= 0) return;
            var ok = Find("okButton");
            dialog.SuspendLayout();
            try
            {
                version.AutoSize = true;
                version.Text = "Application version: " + DisplayVersion() + "\r\nGurux.DLMS version: " + GuruxVersion();
                copyright.Top = version.Bottom + pitch / 2;
                // The longer copyright must end above the artwork panel; move the panel (and the OK button
                // below it, which is anchored to the bottom) down by whatever it needs.
                int shift = Math.Max(0, copyright.Bottom + pitch / 3 - panel.Top);
                panel.Top += shift;
                LinkLabel Add(string text, string url, int row)
                {
                    var link = new LinkLabel
                    {
                        AutoSize = true, Font = gurux.Font, LinkColor = gurux.LinkColor, ActiveLinkColor = gurux.ActiveLinkColor,
                        VisitedLinkColor = gurux.VisitedLinkColor, ForeColor = aboutText.ForeColor, Text = text,
                        Location = new Point(gurux.Left, aboutText.Top + row * pitch), TabIndex = gurux.TabIndex,
                    };
                    // Only the repository name is the link; the text is kept short so it stays clear of the artwork.
                    int start = text.IndexOf(": ", StringComparison.Ordinal) + 2;
                    link.Links.Add(start, text.Length - start, url);
                    link.LinkClicked += (_, e) =>
                    {
                        try { Process.Start(new ProcessStartInfo((string)e.Link.LinkData) { UseShellExecute = true }); }
                        catch (Exception) { /* No browser is registered; nothing to show. */ }
                    };
                    panel.Controls.Add(link);
                    return link;
                }
                Add("This fork: superintelligentmeter/GXDLMSDirector", ForkRepository, 1);
                Add("Original: Gurux/GXDLMSDirector", OriginalRepository, 2);
                gurux.Top = aboutText.Top + 3 * pitch;
                panel.Height += 2 * pitch;
                dialog.ClientSize = new Size(dialog.ClientSize.Width, dialog.ClientSize.Height + 2 * pitch + shift);
                // A dialog placed on a monitor with another DPI keeps stale anchors (its OK button ends up
                // under the panel), so place the panel width and the OK button directly, again after
                // every DPI or size change.
                // The package lays the OK button out 15 logical pixels (19 at 120 DPI) above the bottom edge.
                const double okGapLogical = 15.0;
                void Place()
                {
                    if (dialog.IsDisposed) return;
                    if (panel.Width != dialog.ClientSize.Width) panel.Width = dialog.ClientSize.Width;
                    if (ok == null) return;
                    int scale(double logical) => (int)Math.Round(logical * dialog.DeviceDpi / 96.0);
                    // Room for the OK button under the panel, whatever size the rescale left the panel.
                    int needed = panel.Bottom + scale(6) + ok.Height + scale(okGapLogical);
                    if (dialog.ClientSize.Height < needed) dialog.ClientSize = new Size(dialog.ClientSize.Width, needed);
                    int top = dialog.ClientSize.Height - scale(okGapLogical) - ok.Height;
                    if (ok.Top != top) ok.Top = top;
                }
                Place();
                void Later(object sender, EventArgs e) { if (dialog.IsHandleCreated) dialog.BeginInvoke((Action)Place); }
                dialog.DpiChanged += (sender, e) => Later(sender, e);
                dialog.SizeChanged += Later;
            }
            finally { dialog.ResumeLayout(true); }
        }
    }
}

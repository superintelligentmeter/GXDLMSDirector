// Superintelligent Meter contributors, 2026-10-31. Stable device-property geometry. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GXDLMSDirector
{
    partial class DevicePropertiesForm
    {
        private const int PropertyInputWidth = 180;
        private const int WideFieldInset = 8;
        private readonly List<Action> propertyLayoutMetrics = new List<Action>();
        private bool applyingPropertyMetrics;
        private readonly Dictionary<TabPage, Control[]> propertyKeyboardOrder = new Dictionary<TabPage, Control[]>();

        private int PropertyScale(int value) => (int)Math.Round(value * DeviceDpi / 96d);

        // Layout batching. Assembling the dialog changes hundreds of fonts, margins, sizes
        // and table cells; with every container live, each change re-laid out its whole
        // ancestor chain, which doubled the time to open the dialog. Containers stay
        // suspended while the layout is assembled and then lay out once, deepest first.
        private List<Control> layoutBatch;

        private void BeginLayoutBatch()
        {
            layoutBatch = new List<Control>();
            void Suspend(Control control)
            {
                if (control.Controls.Count == 0) return;
                control.SuspendLayout();
                layoutBatch.Add(control);
                foreach (Control child in control.Controls) Suspend(child);
            }
            Suspend(this);
        }

        /// <summary>A container created while the batch is open joins it.</summary>
        private T Batched<T>(T container) where T : Control
        {
            if (layoutBatch != null)
            {
                container.SuspendLayout();
                layoutBatch.Add(container);
            }
            return container;
        }

        private void EndLayoutBatch()
        {
            var batch = layoutBatch;
            layoutBatch = null;
            if (batch == null) return;
            static int Depth(Control control)
            {
                int depth = 0;
                for (var parent = control.Parent; parent != null; parent = parent.Parent) ++depth;
                return depth;
            }
            foreach (var container in batch.OrderByDescending(Depth).ToArray())
                if (!container.IsDisposed) container.ResumeLayout(true);
        }

        private void ConfigureStablePropertiesLayout()
        {
            SuspendLayout();
            try
            {
                AutoSize = false;
                FormBorderStyle = FormBorderStyle.Sizable;
                foreach (var page in new[] { DeviceSettingsTab, AdvancedTab })
                    propertyKeyboardOrder[page] = page.Controls.Cast<Control>().OrderBy(c => c.TabIndex).ToArray();
                ConfigureDeviceFields();
                ConfigureAdvancedFields();
                // The original caption is "Wait time"; name the PDU field without
                // asserting a unit the protocol package does not document consistently.
                PduWaitTimelbl.Text = "PDU wait time:";
                void KeepFramingPage()
                {
                    if (!DeviceTab.TabPages.Contains(HdlcFrameTab) && !DeviceTab.TabPages.Contains(PlcFrame) && !DeviceTab.TabPages.Contains(PduFrame))
                        DeviceTab.TabPages.Insert(2, PduFrame);
                }
                InterfaceCb.SelectedIndexChanged += (_, _) => KeepFramingPage();
                KeepFramingPage();
                // The Keys hint shares the Keys action row (GXDeviceCipheringSettings.Layout.cs),
                // so changing Security never moves the groups; the page scrolls internally.
                foreach (TabPage page in new[] { CipheringTab, HdlcFrameTab })
                    StyleTableCaptions(page);
                ApplyPropertyMetrics();
                SetInitialPropertiesSize();
                DpiChanged += (_, _) =>
                {
                    // WinForms owns window/font scaling; refresh only logical
                    // column widths and spacing after child controls have scaled.
                    if (IsHandleCreated) BeginInvoke((Action)ApplyPropertyMetrics);
                };
            }
            finally { ResumeLayout(true); }
        }

        private void SetInitialPropertiesSize()
        {
            var work = Screen.FromControl(Owner ?? this).WorkingArea;
            var chrome = SizeFromClientSize(Size.Empty);
            int availableWidth = Math.Max(1, work.Width - chrome.Width - PropertyScale(24));
            int availableHeight = Math.Max(1, work.Height - chrome.Height - PropertyScale(24));
            MinimumSize = SizeFromClientSize(new Size(Math.Min(PropertyScale(900), availableWidth), Math.Min(PropertyScale(600), availableHeight)));
            ClientSize = new Size(Math.Min(PropertyScale(960), availableWidth), Math.Min(PropertyScale(740), availableHeight));
        }

        private void ApplyPropertyMetrics()
        {
            if (applyingPropertyMetrics || IsDisposed) return;
            applyingPropertyMetrics = true;
            // After a monitor move this re-applies every logical margin and width;
            // batch it like construction so each container lays out once.
            bool ownBatch = layoutBatch == null;
            if (ownBatch) BeginLayoutBatch();
            try { foreach (var apply in propertyLayoutMetrics) apply(); }
            finally
            {
                if (ownBatch) EndLayoutBatch();
                applyingPropertyMetrics = false;
            }
        }

        private TableLayoutPanel PropertyTable(string name, params int[] widths)
        {
            var table = Batched(new TableLayoutPanel
            {
                Name = name, Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, TabStop = false,
                ColumnCount = widths.Length, RowCount = 0, Margin = Padding.Empty
            });
            foreach (int width in widths)
                table.ColumnStyles.Add(new ColumnStyle(width == 0 ? SizeType.Percent : SizeType.Absolute, width == 0 ? 100 : PropertyScale(width)));
            propertyLayoutMetrics.Add(() =>
            {
                for (int i = 0; i < widths.Length; ++i)
                    if (widths[i] != 0) table.ColumnStyles[i].Width = PropertyScale(widths[i]);
            });
            return table;
        }

        private static int PropertyRow(TableLayoutPanel table)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return row;
        }

        private void PropertyCell(TableLayoutPanel table, Control control, int column, int row, int span = 1, bool stretch = false)
        {
            // TabIndex, value, range and event subscriptions belong to the
            // original control. A table cell must never replace any of them.
            control.Dock = stretch ? DockStyle.Fill : DockStyle.None;
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            if (control is Label label) label.AutoSize = true;
            if (control is CheckBox check) check.AutoSize = true;
            if (control is Label || control is CheckBox) KeepCaptionStyle(control);
            table.Controls.Add(control, column, row);
            table.SetColumnSpan(control, span);
            propertyLayoutMetrics.Add(() =>
            {
                control.Margin = new Padding(0, control is Label ? PropertyScale(4) : 0,
                    column + span == table.ColumnCount ? 0 : PropertyScale(16), PropertyScale(8));
                if (!stretch && (control is TextBoxBase || control is ComboBox || control is UpDownBase || control is DateTimePicker))
                    control.Width = PropertyScale(PropertyInputWidth);
            });
        }

        private static string PropertyCaption(string text)
        {
            var words = text.Trim().Split(' ');
            for (int i = 1; i < words.Length; ++i)
                if (words[i].Any(char.IsLower)) words[i] = words[i].ToLowerInvariant();
            return string.Join(" ", words);
        }

        private readonly HashSet<Control> styledCaptions = new HashSet<Control>();

        /// <summary>
        /// Sentence-style caption, with a colon for field labels. The original code
        /// still changes some captions at run time (for example Physical Server /
        /// Serial Number, Password / Signing key, payload / frame size); restyle
        /// those texts as they arrive so every interface shows the same style.
        /// Only letter case and the label colon change; the words stay the same.
        /// </summary>
        private void KeepCaptionStyle(Control control)
        {
            void Apply()
            {
                if (string.IsNullOrWhiteSpace(control.Text)) return;
                string text = control is Label ? PropertyCaption(control.Text.TrimEnd(':', ' ')) + ":" : PropertyCaption(control.Text);
                if (control.Text != text) control.Text = text;
            }
            if (styledCaptions.Add(control)) control.TextChanged += (_, _) => Apply();
            Apply();
        }

        /// <summary>Applies the caption style to field captions and check boxes of a laid-out page.</summary>
        private void StyleTableCaptions(Control root)
        {
            foreach (Control child in root.Controls)
            {
                // Field captions occupy one cell of a caption rail (a fixed or measured first
                // column). Hints, headings and inline validation messages span columns, sit
                // beside a field or start a stretching row such as the Keys action row.
                if (child is Label && child.Parent is TableLayoutPanel table && IsCaptionRail(table) &&
                    table.GetColumn(child) == 0 && table.GetColumnSpan(child) == 1)
                    KeepCaptionStyle(child);
                else if (child is CheckBox)
                    KeepCaptionStyle(child);
                StyleTableCaptions(child);
            }
        }

        private static bool IsCaptionRail(TableLayoutPanel table) =>
            table.ColumnStyles.Count == 0 || table.ColumnStyles[0].SizeType != SizeType.Percent;

        private void ConfigureDeviceFields()
        {
            var fields = PropertyTable("DevicePropertyFields", 152, PropertyInputWidth + 24, 0, 168, PropertyInputWidth);
            fields.TabIndex = 0;
            void Row(Control label, Control editor, Control rightLabel = null, Control rightEditor = null, bool wide = false)
            {
                int row = PropertyRow(fields);
                PropertyCell(fields, label, 0, row);
                PropertyCell(fields, editor, 1, row, wide ? 4 : 1, wide);
                if (rightLabel != null) PropertyCell(fields, rightLabel, 3, row);
                if (rightEditor != null) PropertyCell(fields, rightEditor, 4, row);
            }
            Row(NameLbl, NameTB, wide: true);
            Row(ManufacturerLbl, ManufacturerCB, wide: true);
            Row(InterfaceLbl, InterfaceCb, rightEditor: UseLNCB);
            Row(AuthenticationLbl, AuthenticationCB, ClientAddLbl, ClientAddTB);
            Row(PasswordLbl, PasswordTB, rightEditor: PasswordAsciiCb);
            PropertyCell(fields, SigningKeyCb, 1, 4);
            Row(WaitTimeLbl, WaitTimeTB, ResendLbl, ResendTb);
            Row(label3, ServerAddressTypeCB, rightEditor: BroadcastCb);
            Row(LogicalServerAddressLbl, LogicalServerAddressTB, PhysicalServerAddressLbl, PhysicalServerAddressTB);
            Row(label1, MediasCB, rightEditor: VerboseModeCB);

            var mediaHost = Batched(new Panel { Name = "DeviceMediaHost", Dock = DockStyle.Fill, TabStop = false, TabIndex = 1, Margin = Padding.Empty });
            var page = Batched(new TableLayoutPanel { Name = "DeviceSettingsLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                Margin = Padding.Empty, TabStop = false });
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(fields, 0, 0);
            page.Controls.Add(mediaHost, 0, 1);
            DeviceSettingsTab.Controls.Add(page);
            page.BringToFront();
            deviceMediaHost = mediaHost;
            foreach (var group in new[] { NetworkSettingsGB, SerialSettingsGB, TerminalSettingsGB, CustomSettings })
            {
                mediaHost.Controls.Add(group);
                // Connection settings ends below its own fields rather than framing the rest
                // of the page. A provider's page (CustomSettings) is measured separately and
                // scrolls inside its viewport when the dialog is short.
                group.AutoSize = !ReferenceEquals(group, CustomSettings);
                group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                group.Dock = DockStyle.Top;
                group.Text = "Connection settings";
                // Right inset keeps full-width inputs off the group frame; the property table
                // above uses the same inset so wide fields share one edge. The first and last
                // rows keep 12 pixels to the frame, like the Secured Connections groups.
                propertyLayoutMetrics.Add(() => group.Padding = new Padding(0, PropertyScale(12), PropertyScale(WideFieldInset), PropertyScale(4)));
            }
            mediaHost.SizeChanged += (_, _) => FitCustomMediaGroup();
            CustomSettings.VisibleChanged += (_, _) => FitCustomMediaGroup();
            propertyLayoutMetrics.Add(() =>
            {
                DeviceSettingsTab.Padding = new Padding(PropertyScale(16));
                mediaHost.Margin = new Padding(0, PropertyScale(8), 0, 0);
                fields.Margin = new Padding(0, 0, PropertyScale(WideFieldInset), 0);
            });
            TableLayoutPanel MediaTable(GroupBox group, int labelWidth = 152)
            {
                var table = PropertyTable(group.Name + "Fields", labelWidth, PropertyInputWidth + 24, 0);
                group.Controls.Add(table);
                return table;
            }
            void MediaRow(TableLayoutPanel table, Control label, Control input, Control extra = null, bool wide = false)
            {
                int row = PropertyRow(table);
                PropertyCell(table, label, 0, row);
                PropertyCell(table, input, 1, row, wide ? 2 : 1, wide);
                if (extra != null) PropertyCell(table, extra, 2, row);
            }
            var network = MediaTable(NetworkSettingsGB);
            MediaRow(network, HostLbl, HostNameTB, wide: true);
            MediaRow(network, PortLbl, PortTB);
            MediaRow(network, label2, NetProtocolCB, UseRemoteSerialCB);
            var serial = MediaTable(SerialSettingsGB, 236);
            MediaRow(serial, SerialPortLbl, SerialPortCB, AdvancedBtn);
            MediaRow(serial, UseMaximumBaudRateCB, MaximumBaudRateCB);
            var terminal = MediaTable(TerminalSettingsGB);
            MediaRow(terminal, TerminalPhoneNumberLbl, TerminalPhoneNumberTB);
            MediaRow(terminal, TerminalPortLbl, TerminalPortCB, TerminalAdvancedBtn);
            // The original Advanced... commands are inline field actions. Measure them so the
            // caption always fits: a fixed-size button in a table cell can be squeezed to the
            // cell's width during the batched first layout and never grows back.
            foreach (var button in new[] { AdvancedBtn, TerminalAdvancedBtn })
                Gurux.DLMS.UI.ButtonLayoutPolicy.ApplyToButton(button, minimumWidth: 87, height: 23);
        }

        private void ConfigureAdvancedFields()
        {
            AdvancedTab.AutoScroll = true;
            // Two columns of sections, so the page fits the dialog without scrolling: Association,
            // Time handling and Protocol on the left; Security options and Behaviour on the right.
            // Reading order stays the original field order (down the left column, then the right
            // one), and ProcessTabKey keeps the original Tab order.
            var sections = Batched(new TableLayoutPanel { Name = "AdvancedSections", Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, TabStop = false });
            sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            AdvancedTab.Controls.Add(sections);
            var left = PropertyTable("AdvancedPropertyFields", 144, PropertyInputWidth + 16, 0);
            var right = PropertyTable("AdvancedSecurityFields", 144, PropertyInputWidth + 16, 0);
            left.TabIndex = 0; right.TabIndex = 1;
            sections.Controls.Add(left, 0, 0);
            sections.Controls.Add(right, 1, 0);
            propertyLayoutMetrics.Add(() =>
            {
                AdvancedTab.Padding = new Padding(PropertyScale(16));
                // The property region stays within 800 logical pixels; a gutter separates the columns.
                sections.MaximumSize = new Size(PropertyScale(800), 0);
                right.Margin = new Padding(PropertyScale(24), 0, 0, 0);
            });
            var table = left;
            void Heading(string caption)
            {
                bool first = table.RowCount == 0;
                var heading = new Label { Text = caption, AutoSize = true, TabStop = false };
                table.Controls.Add(heading, 0, PropertyRow(table));
                table.SetColumnSpan(heading, 3);
                // Set emphasis after parenting so the application's current font
                // remains authoritative; no global font or point-size override.
                heading.Font = new Font(table.Font, FontStyle.Bold);
                // The first heading of each column starts at the top, so both columns align.
                propertyLayoutMetrics.Add(() => heading.Margin = new Padding(0, PropertyScale(first ? 0 : 12), 0, PropertyScale(6)));
            }
            void Row(Control label, Control input)
            {
                int row = PropertyRow(table);
                if (label != null) PropertyCell(table, label, 0, row);
                // Check boxes start on the caption rail, as in the original layout and on Secured
                // Connections and Frame; value editors keep the input edge.
                if (input is CheckBox) PropertyCell(table, input, 0, row, 3);
                else PropertyCell(table, input, 1, row);
            }
            Heading("Association");
            Row(InactivityTimeoutLbl, InactivityTimeoutTb);
            Row(PriorityLbl, PriorityCb);
            Row(ServiceClassLbl, ServiceClassCb);
            Heading("Time handling");
            Row(null, UseUtcTimeZone);
            Row(null, IgnoreTimeZoneCb);
            Row(null, IgnoreTimeStatusCb);
            // Retain the original field sequence after the time flags; these
            // transfer/protocol fields are not time settings, so give them a heading.
            Heading("Protocol");
            Row(MaxPduLbl, MaxPduTb);
            Row(GBTWindowSizeLbl, GBTWindowSizeTb);
            Row(UserIDLbl, UserIdTb);
            Row(StandardLbl, StandardCb);
            table = right;
            Heading("Security options");
            Row(null, UseProtectedReleaseCb);
            Row(null, SecurityChangeCheckCb);
            Row(ChallengeSizeLbl, ChallengeSizeCb);
            Row(null, IncludePublicKeyCb);
            Row(null, SignInitiateRequestResponseCb);
            Heading("Behaviour");
            Row(null, OverwriteAttributeAccessRightsCb);
            Row(null, IncreaseInvocationCounterForGMacAuthenticationCB);
        }

        private Panel CreateMediaContent(Form mediaForm)
        {
            // Measure the actual fields, not the unused bottom of the provider's
            // standalone form. Keep nested editors and bottom-anchored actions.
            int height = 0;
            void Measure(Control parent, int top)
            {
                foreach (Control child in parent.Controls)
                {
                    if (parent == mediaForm && child is Panel && !child.Enabled) continue;
                    if (child is Panel || child is GroupBox || child is TabControl || child is TabPage)
                        Measure(child, top + child.Top);
                    else height = Math.Max(height, top + child.Bottom + PropertyScale(8));
                }
            }
            Measure(mediaForm, 0);
            if (height == 0) height = mediaForm.ClientSize.Height;
            return new Panel { Name = "CustomMediaContent", Size = new Size(mediaForm.ClientSize.Width, height),
                Dock = DockStyle.Top, Margin = Padding.Empty, TabStop = false };
        }

        private void HostMediaContent(Panel content)
        {
            var viewport = new Panel { Name = "CustomMediaViewport", Dock = DockStyle.Fill, AutoScroll = true, TabStop = false };
            viewport.Controls.Add(content);
            CustomSettings.Controls.Add(viewport);
            // Height is the complete media property page, not the fixed legacy
            // group box. Docking supplies the width; the viewport supplies scroll.
            FitCustomMediaGroup();
        }

        private Panel deviceMediaHost;
        private bool fittingCustomMedia;

        /// <summary>
        /// Sizes the provider-page group to its measured page, capped by the space below the
        /// device fields; a taller page scrolls inside its viewport.
        /// </summary>
        private void FitCustomMediaGroup()
        {
            if (fittingCustomMedia || deviceMediaHost == null || IsDisposed) return;
            var content = CustomSettings.Controls.Find("CustomMediaContent", true).FirstOrDefault();
            if (content == null) return;
            fittingCustomMedia = true;
            try
            {
                // GroupBox content starts below its caption line and inside its padding.
                int chrome = CustomSettings.Font.Height + CustomSettings.Padding.Vertical;
                int height = Math.Max(chrome, Math.Min(deviceMediaHost.ClientSize.Height, chrome + content.Height));
                if (CustomSettings.Height != height) CustomSettings.Height = height;
            }
            finally { fittingCustomMedia = false; }
        }

        private static IEnumerable<Control> PropertyTabStops(Control root)
        {
            if (root.TabStop && root.CanSelect) yield return root;
            if (root is UpDownBase || root is TextBoxBase || root is ComboBox || root is DateTimePicker) yield break;
            foreach (Control child in root.Controls.Cast<Control>().OrderBy(c => c.TabIndex))
                foreach (var stop in PropertyTabStops(child)) yield return stop;
        }

        protected override bool ProcessTabKey(bool forward)
        {
            if (DeviceTab.SelectedTab != null && propertyKeyboardOrder.TryGetValue(DeviceTab.SelectedTab, out var originals))
            {
                // Resolve media groups now: external providers replace their
                // child controls when the selected media changes.
                var order = new List<Control> { DeviceTab };
                foreach (var original in originals.Where(c => !c.IsDisposed))
                    order.AddRange(PropertyTabStops(original));
                order.AddRange(new Control[] { InitialSettingsBtn, ShowAsHex, OKBtn, CancelBtn });
                order = order.Where(c => c.CanSelect && c.TabStop).Distinct().ToList();
                int current = order.FindIndex(c => c.Focused || c is UpDownBase && c.ContainsFocus);
                if (current >= 0)
                {
                    order[(current + (forward ? 1 : order.Count - 1)) % order.Count].Select();
                    return true;
                }
            }
            return base.ProcessTabKey(forward);
        }
    }
}

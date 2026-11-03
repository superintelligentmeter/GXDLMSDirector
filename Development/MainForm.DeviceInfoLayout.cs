// Modified by Superintelligent Meter contributors, 2026-11-01: General tab layout. GPL-2.0-only; original notices retained.
using System;
using System.Drawing;
using System.Windows.Forms;
using Gurux.DLMS.UI;

namespace GXDLMSDirector
{
    partial class MainForm
    {
        // The General tab of the device view. The original controls, values and handlers are kept; they are
        // only moved from hand-placed positions into aligned tables, in the look of the other screens: muted
        // captions, soft read-only fields with a thin light border, and a bold heading for each section instead
        // of boxed groups. Reading order is unchanged: device, authentication, addresses, conformance, then
        // Ciphering, Command line parameters and Gateway (the last two used to sit on top of the Ciphering fields).
        private void LayOutDeviceInfo()
        {
            int S(int logical) => DeviceGb.LogicalToDeviceUnits(logical);
            const int Rail = 144;   // caption column, as in Device Properties
            Color caption = SystemInformation.HighContrast ? SystemColors.WindowText : ModernPresentation.Muted;
            Color ink = SystemInformation.HighContrast ? SystemColors.WindowText : ModernPresentation.Ink;

            void Caption(Label label, string text)
            {
                label.Text = text;
                label.AutoSize = true;
                label.ForeColor = caption;
                label.Anchor = AnchorStyles.Left;
                label.Margin = new Padding(0, S(6), S(12), S(6));
            }
            void Value(Label label)
            {
                label.AutoSize = true;
                label.ForeColor = ink;
                label.Anchor = AnchorStyles.Left;
                label.Margin = new Padding(0, S(6), S(16), S(6));
            }
            // A read-only text box inside a soft frame, like the other screens' fields.
            Control Field(TextBox box, int maximum = 420, bool tall = false)
            {
                var frame = new SoftFieldFrame(box, tall)
                {
                    Padding = new Padding(S(8), S(tall ? 6 : 7), S(8), S(tall ? 6 : 5)),
                    Anchor = tall ? AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right : AnchorStyles.Left | AnchorStyles.Right,
                    Margin = new Padding(0, S(4), S(16), S(4)),
                    Height = S(tall ? 112 : 32),
                    MinimumSize = new Size(0, S(tall ? 112 : 32)),
                };
                if (maximum > 0) frame.MaximumSize = new Size(S(maximum), 0);
                box.ForeColor = ink;
                return frame;
            }
            // Caption | value | caption | value; the value columns share the remaining width.
            TableLayoutPanel Grid(string name)
            {
                var table = new TableLayoutPanel
                {
                    Name = name, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 4,
                    Dock = DockStyle.Top, Margin = Padding.Empty, Padding = Padding.Empty,
                };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(Rail)));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(Rail)));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                return table;
            }
            void Rows(TableLayoutPanel table, int count)
            {
                table.RowCount = count;
                for (int i = 0; i < count; ++i) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            Label Heading(string name, string text, float size, int top)
            {
                return new Label
                {
                    Name = name, Text = text, AutoSize = true, ForeColor = ink,
                    Font = new Font("Segoe UI", size, FontStyle.Bold),
                    Margin = new Padding(0, S(top), 0, S(8)),
                };
            }
            void Pair(TableLayoutPanel table, int row, int column, Label label, string text, Control value)
            {
                Caption(label, text);
                table.Controls.Add(label, column, row);
                if (value is Label valueLabel) Value(valueLabel);
                else if (value is TextBox box) value = Field(box);
                table.Controls.Add(value, column + 1, row);
            }

            DeviceGb.SuspendLayout();
            Ciphering.SuspendLayout();

            // --- Device ---
            var device = Grid("DeviceInfoGrid");
            Rows(device, 4);
            Caption(ManufacturerLbl, "Manufacturer:"); Value(ManufacturerValueLbl);
            device.Controls.Add(ManufacturerLbl, 0, 0);
            device.Controls.Add(ManufacturerValueLbl, 1, 0); device.SetColumnSpan(ManufacturerValueLbl, 3);
            Caption(AuthenticationLbl, "Authentication:"); Value(AuthenticationTb);
            Caption(ClientAddressLbl, "Client address:"); Value(ClientAddressValueLbl);
            device.Controls.Add(AuthenticationLbl, 0, 1); device.Controls.Add(AuthenticationTb, 1, 1);
            device.Controls.Add(ClientAddressLbl, 2, 1); device.Controls.Add(ClientAddressValueLbl, 3, 1);
            Caption(LogicalAddressLbl, "Logical address:"); Value(LogicalAddressValueLbl);
            Caption(PhysicalAddressLbl, "Physical address:"); Value(PhysicalAddressValueLbl);
            device.Controls.Add(LogicalAddressLbl, 0, 2); device.Controls.Add(LogicalAddressValueLbl, 1, 2);
            device.Controls.Add(PhysicalAddressLbl, 2, 2); device.Controls.Add(PhysicalAddressValueLbl, 3, 2);

            // Conformance: the proposed and negotiated lists side by side, scrolling instead of clipping.
            var conformance = new TableLayoutPanel
            {
                Name = "ConformanceGrid", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2,
                Dock = DockStyle.Top, Margin = Padding.Empty, Padding = Padding.Empty,
            };
            conformance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            conformance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            conformance.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            conformance.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            foreach (var pair in new[] { (label1, "Proposed:", 0), (Negotiated, "Negotiated:", 1) })
            {
                pair.Item1.Text = pair.Item2;
                pair.Item1.AutoSize = true;
                pair.Item1.ForeColor = caption;
                pair.Item1.Anchor = AnchorStyles.Left;
                pair.Item1.Margin = new Padding(0, S(6), S(12), S(2));
                conformance.Controls.Add(pair.Item1, pair.Item3, 0);
            }
            foreach (var pair in new[] { (ProposedConformanceTB, 0), (NegotiatedConformanceTB, 1) })
            {
                pair.Item1.ScrollBars = ScrollBars.Vertical;
                conformance.Controls.Add(Field(pair.Item1, 0, tall: true), pair.Item2, 1);
            }
            Caption(ConformanceLbl, "Conformance:");
            ConformanceLbl.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            device.Controls.Add(ConformanceLbl, 0, 3);
            device.Controls.Add(conformance, 1, 3); device.SetColumnSpan(conformance, 3);

            // --- Ciphering, Command line parameters and Gateway (their boxes are replaced by headings) ---
            var cipher = Grid("CipheringGrid");
            Rows(cipher, 5);
            Pair(cipher, 0, 0, SecurityLbl, "Security:", SecurityTb);
            Pair(cipher, 0, 2, SuiteLbl, "Suite:", SuiteTb);
            Pair(cipher, 1, 0, SchemeLbl, "Signing:", SigningTb);
            Pair(cipher, 2, 0, ClientSystemTitleLbl, "Client system title:", ClientSystemTitleTb);
            Pair(cipher, 2, 2, ServerSystemTitleLbl, "Server system title:", ServerSystemTitleTb);
            Pair(cipher, 3, 0, BlockCipherKeyLbl, "Block cipher key:", BlockCipherKeyTb);
            Pair(cipher, 3, 2, AuthenticationKeyLbl, "Authentication key:", AuthenticationKeyTb);
            Pair(cipher, 4, 0, BroadcastKeyLbl, "Broadcast key:", BroadcastKeyTb);
            Pair(cipher, 4, 2, DedicatedKeyLbl, "Dedicated key:", DedicatedKeyTb);

            var gateway = Grid("GatewayGrid");
            Rows(gateway, 1);
            Pair(gateway, 0, 0, NetworkIDLbl, "Network ID:", NetworkIDTb);
            Pair(gateway, 0, 2, PhysicalDeviceAddressLbl, "Device address:", PhysicalDeviceAddressTb);

            var command = new TableLayoutPanel
            {
                Name = "CommandLineGrid", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 1,
                Dock = DockStyle.Top, Margin = Padding.Empty, Padding = Padding.Empty,
            };
            command.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            command.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            command.Controls.Add(Field(CommandLineTb, 0), 0, 0);

            // --- The page: one scrolling column, headed by the device name ---
            var title = Heading("DeviceTitle", DeviceGb.Text, 11f, 0);
            DeviceGb.TextChanged += (_, _) => title.Text = DeviceGb.Text;
            var root = new TableLayoutPanel
            {
                Name = "DeviceInfoLayout", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 8,
                Dock = DockStyle.Top, Margin = Padding.Empty, Padding = Padding.Empty, MinimumSize = new Size(S(700), 0),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 8; ++i) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(title, 0, 0);
            root.Controls.Add(device, 0, 1);
            root.Controls.Add(Heading("CipheringHeading", Ciphering.Text, 9f, 18), 0, 2);
            root.Controls.Add(cipher, 0, 3);
            root.Controls.Add(Heading("CommandLineHeading", groupBox2.Text.TrimEnd(':'), 9f, 18), 0, 4);
            root.Controls.Add(command, 0, 5);
            root.Controls.Add(Heading("GatewayHeading", groupBox1.Text, 9f, 18), 0, 6);
            root.Controls.Add(gateway, 0, 7);
            var body = new Panel
            {
                Name = "DeviceInfoBody", Dock = DockStyle.Fill, AutoScroll = true,
                Padding = new Padding(S(16), S(14), S(16), S(16)),
            };
            body.Controls.Add(root);
            // The group boxes only framed the page; the device name is written to DeviceGb as before and
            // shown in the heading above.
            tabPage4.Controls.Remove(DeviceGb);
            tabPage4.Controls.Add(body);

            Ciphering.ResumeLayout(false);
            DeviceGb.ResumeLayout(false);
            tabPage4.PerformLayout();
        }

        // A read-only editor on a soft surface with a thin light border; the border turns accent colored
        // while the editor has focus.
        private sealed class SoftFieldFrame : UserControl
        {
            private static readonly Color Surface = Color.FromArgb(245, 247, 251);

            public SoftFieldFrame(TextBox editor, bool multiline)
            {
                SetStyle(ControlStyles.ResizeRedraw, true);
                BackColor = SystemInformation.HighContrast ? SystemColors.Window : Surface;
                editor.BorderStyle = BorderStyle.None;
                editor.BackColor = BackColor;
                editor.Dock = multiline ? DockStyle.Fill : DockStyle.Top;
                editor.Margin = Padding.Empty;
                Controls.Add(editor);
                editor.Enter += (_, _) => Invalidate();
                editor.Leave += (_, _) => Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Color color = SystemInformation.HighContrast ? SystemColors.WindowText :
                    ContainsFocus ? ModernPresentation.Accent : ModernPresentation.Line;
                using var pen = new Pen(color);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}

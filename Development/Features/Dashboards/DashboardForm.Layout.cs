// Intelmeter contributors, 2026-10-17. Card-level drag ordering and hover deletion. GPL-2.0-only.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace GXDLMSDirector
{
    internal sealed partial class DashboardForm
    {
        private sealed record CardItem(DashboardWidget Widget, DashboardBinding Binding);
        private sealed record CardDrag(DashboardForm Owner, CardItem Item);
        private readonly Dictionary<Control, CardItem> cardItems = new();
        private Control dropTarget;
        private bool dropAfter;
        private DateTime lastDragScroll;
        private List<CardItem> OrderedCards()
        {
            if (Current == null) return new();
            var items = new List<(CardItem Item, int Order)>();
            foreach (var w in Current.Widgets)
                if (w.Kind == "Values")
                    for (int i = 0; i < w.Bindings.Count; i++) items.Add((new CardItem(w, w.Bindings[i]), w.TileOrder.Count == w.Bindings.Count ? w.TileOrder[i] : items.Count));
                else items.Add((new CardItem(w, null), w.LayoutOrder ?? items.Count));
            return items.OrderBy(x => x.Order).Select(x => x.Item).ToList();
        }
        private void StoreCardOrder(List<CardItem> items)
        {
            foreach (var w in Current.Widgets.Where(w => w.Kind == "Values")) w.TileOrder = Enumerable.Repeat(0, w.Bindings.Count).ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Binding == null) item.Widget.LayoutOrder = i;
                else item.Widget.TileOrder[item.Widget.Bindings.IndexOf(item.Binding)] = i;
            }
        }
        internal bool MoveCard(int from, int target, bool after)
        {
            if (busy || Current == null) return false;
            var items = OrderedCards();
            if (from < 0 || target < 0 || from >= items.Count || target >= items.Count || from == target) return false;
            int insert = target + (after ? 1 : 0); if (from < insert) insert--;
            if (insert == from) return false;
            monitor.Checked = false;
            var moved = items[from]; items.RemoveAt(from); items.Insert(insert, moved);
            StoreCardOrder(items); MarkChanged(); Render(); return true;
        }
        internal void DeleteCard(int index)
        {
            if (busy || Current == null) return;
            var items = OrderedCards(); if (index < 0 || index >= items.Count) return;
            monitor.Checked = false; var item = items[index]; items.RemoveAt(index);
            if (item.Binding == null) Current.Widgets.Remove(item.Widget);
            else
            {
                item.Widget.Bindings.Remove(item.Binding);
                if (item.Widget.Bindings.Count == 0) Current.Widgets.Remove(item.Widget);
            }
            StoreCardOrder(items); MarkChanged(); Render();
        }
        private static DashboardCardSize CardSize(CardItem item) => item.Binding == null ? item.Widget.Size : item.Binding.Size;
        internal void ResizeCard(int index, int width, int height)
        {
            var items = OrderedCards();
            if (busy || index < 0 || index >= items.Count) return;
            var item = items[index];
            var size = new DashboardCardSize { Width = Math.Clamp(width, item.Binding == null ? 280 : 160, 2400), Height = Math.Clamp(height, item.Binding == null ? 210 : 150, 1600) };
            var previous = CardSize(item);
            if (previous != null && previous.Width == size.Width && previous.Height == size.Height) return;
            if (item.Binding == null) item.Widget.Size = size; else item.Binding.Size = size;
            MarkChanged(); LayoutWorkspace();
        }
        private void AttachResizeGrip(Panel card, CardItem item)
        {
            var grip = new Panel { Size = new Size(Scale(18), Scale(18)), Cursor = Cursors.SizeNWSE, AccessibleName = "Resize card", BackColor = Color.White };
            card.Controls.Add(grip);
            void Position() { grip.Location = new Point(card.Width - grip.Width - 2, card.Height - grip.Height - 2); grip.BringToFront(); }
            card.SizeChanged += (_, _) => Position(); Position();
            grip.Paint += (_, e) => { using var pen = new Pen(SystemColors.GrayText); for (int n = 4; n <= 12; n += 4) e.Graphics.DrawLine(pen, grip.Width - Scale(n), grip.Height - 3, grip.Width - 3, grip.Height - Scale(n)); };
            Point start = Point.Empty; Size original = Size.Empty; bool dragging = false;
            grip.MouseDown += (_, e) => { if (e.Button != MouseButtons.Left || busy) return; monitor.Checked = false; start = grip.PointToScreen(e.Location); original = card.Size; dragging = true; cardResizeInProgress = true; grip.Capture = true; };
            grip.MouseMove += (_, e) =>
            {
                if (!dragging || busy || e.Button != MouseButtons.Left) return;
                var now = grip.PointToScreen(e.Location);
                int maxWidth = Math.Max(160, widgets.ClientSize.Width - Scale(20) - SystemInformation.VerticalScrollBarWidth);
                ResizeCard(OrderedCards().IndexOf(item), (int)Math.Round(Math.Min(maxWidth, original.Width + now.X - start.X) * 96.0 / DeviceDpi), (int)Math.Round((original.Height + now.Y - start.Y) * 96.0 / DeviceDpi));
            };
            void FinishResize()
            {
                if (!dragging) return;
                dragging = false; cardResizeInProgress = false; grip.Capture = false;
                FlushAutoSave();
            }
            grip.MouseUp += (_, _) => FinishResize();
            grip.MouseCaptureChanged += (_, _) => { if (!grip.Capture) FinishResize(); };
        }
        private void InitializeDragLayout()
        {
            widgets.AllowDrop = true;
            widgets.DragOver += DragCardOver; widgets.DragDrop += DropCard;
            widgets.DragLeave += (_, _) => ClearDropTarget();
        }
        private void ClearDropTarget() { var old = dropTarget; dropTarget = null; old?.Invalidate(); }
        private void DragCardOver(object sender, DragEventArgs e)
        {
            if (busy || e.Data?.GetData(typeof(CardDrag)) is not CardDrag drag || drag.Owner != this) { e.Effect = DragDropEffects.None; ClearDropTarget(); return; }
            e.Effect = DragDropEffects.Move;
            var point = widgets.PointToClient(new Point(e.X, e.Y));
            if (DateTime.UtcNow - lastDragScroll > TimeSpan.FromMilliseconds(100))
            {
                int step = point.Y < Scale(30) ? -Scale(25) : point.Y > widgets.ClientSize.Height - Scale(30) ? Scale(25) : 0;
                if (step != 0) { widgets.AutoScrollPosition = new Point(0, Math.Max(0, -widgets.AutoScrollPosition.Y + step)); lastDragScroll = DateTime.UtcNow; }
            }
            var target = layoutCards.OrderBy(c => Math.Max(c.Left - point.X, Math.Max(0, point.X - c.Right)) + Math.Max(c.Top - point.Y, Math.Max(0, point.Y - c.Bottom))).FirstOrDefault();
            bool after = target != null && (point.Y > target.Bottom || (point.Y >= target.Top && ((string)target.Tag == "Value" ? point.X >= target.Left + target.Width / 2 : point.Y >= target.Top + target.Height / 2)));
            if (dropTarget != target || dropAfter != after) { ClearDropTarget(); dropTarget = target; dropAfter = after; target?.Invalidate(); }
        }
        private void DropCard(object sender, DragEventArgs e)
        {
            if (e.Data?.GetData(typeof(CardDrag)) is not CardDrag drag || drag.Owner != this || dropTarget == null) { ClearDropTarget(); return; }
            var items = OrderedCards(); var target = cardItems[dropTarget]; bool after = dropAfter;
            ClearDropTarget(); MoveCard(items.IndexOf(drag.Item), items.IndexOf(target), after);
        }
        private void AttachCard(Panel card, CardItem item)
        {
            cardItems[card] = item;
            card.TabStop = editLayout; card.AccessibleName = item.Binding?.Label ?? item.Widget.Title;
            card.KeyDown += (_, e) =>
            {
                if (!editLayout || busy || !e.Control) return;
                int index = OrderedCards().IndexOf(item); int direction = e.KeyCode == Keys.Left || e.KeyCode == Keys.Up ? -1 : e.KeyCode == Keys.Right || e.KeyCode == Keys.Down ? 1 : 0;
                if (direction == 0) return; e.Handled = e.SuppressKeyPress = true;
                if (e.Shift) ResizeCard(index, (int)Math.Round(card.Width * 96.0 / DeviceDpi) + (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right ? direction * 20 : 0), (int)Math.Round(card.Height * 96.0 / DeviceDpi) + (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down ? direction * 20 : 0));
                else if (index + direction >= 0 && index + direction < layoutCards.Count && MoveCard(index, index + direction, direction > 0)) layoutCards[index + direction].Focus();
            };
            var delete = new CardDeleteButton { Size = new Size(Scale(24), Scale(24)), Visible = editLayout, AccessibleName = "Delete " + (item.Binding?.Label ?? item.Widget.Title), TabStop = true };
            card.Controls.Add(delete); delete.BringToFront();
            card.SizeChanged += (_, _) => { delete.Location = new Point(card.Width - delete.Width - Scale(6), Scale(6)); delete.BringToFront(); };
            delete.Click += (_, _) => DeleteCard(OrderedCards().IndexOf(item));
            void Hover() { if (!card.IsDisposed) delete.Visible = editLayout || card.ContainsFocus || card.ClientRectangle.Contains(card.PointToClient(Control.MousePosition)); }
            void Leave() { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)Hover); }
            void Hook(Control c)
            {
                c.AllowDrop = true; c.DragOver += DragCardOver; c.DragDrop += DropCard;
                c.MouseEnter += (_, _) => Hover(); c.MouseLeave += (_, _) => Leave(); c.Enter += (_, _) => Hover(); c.Leave += (_, _) => Leave();
                if (c is Label || c == card || c is FlowLayoutPanel)
                {
                    Point? start = null; c.Cursor = Cursors.SizeAll;
                    c.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left && !busy) start = c.PointToScreen(e.Location); };
                    c.MouseMove += (_, e) =>
                    {
                        if (!start.HasValue || e.Button != MouseButtons.Left || busy) return;
                        var now = c.PointToScreen(e.Location); var size = SystemInformation.DragSize;
                        if (Math.Abs(now.X - start.Value.X) < size.Width / 2 && Math.Abs(now.Y - start.Value.Y) < size.Height / 2) return;
                        start = null; monitor.Checked = false;
                        try { c.DoDragDrop(new CardDrag(this, item), DragDropEffects.Move); } finally { ClearDropTarget(); }
                    };
                    c.MouseUp += (_, _) => start = null;
                }
                foreach (Control child in c.Controls) if (child != delete) Hook(child);
            }
            dashboardTips.SetToolTip(delete, "Delete this card");
            Hook(card); AttachResizeGrip(card, item); delete.MouseEnter += (_, _) => Hover(); delete.MouseLeave += (_, _) => Leave();
            delete.Enter += (_, _) => Hover(); delete.Leave += (_, _) => Leave();
            card.Paint += (_, e) =>
            {
                if (dropTarget != card) return;
                using var pen = new Pen(Color.FromArgb(0, 120, 215), Scale(3));
                if ((string)card.Tag == "Value") { int x = dropAfter ? card.Width - 2 : 1; e.Graphics.DrawLine(pen, x, 1, x, card.Height - 2); }
                else { int y = dropAfter ? card.Height - 2 : 1; e.Graphics.DrawLine(pen, 1, y, card.Width - 2, y); }
            };
        }
        private sealed class CardDeleteButton : Button
        {
            internal CardDeleteButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent?.BackColor ?? Color.White);
                using var pen = new Pen(Enabled ? Color.FromArgb(145, 40, 40) : SystemColors.GrayText, Math.Max(1, DeviceDpi / 64f));
                int inset = Width / 3; e.Graphics.DrawLine(pen, inset, inset, Width - inset, Height - inset); e.Graphics.DrawLine(pen, Width - inset, inset, inset, Height - inset);
                if (Focused) { using var focus = new Pen(SystemColors.Highlight, 2); e.Graphics.DrawRectangle(focus, Rectangle.Inflate(ClientRectangle, -2, -2)); }
            }
        }
    }
}

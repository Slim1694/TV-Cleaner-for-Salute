using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace TVCleaner.UI
{
    /// <summary>"Select all" check box drawn in the first column header of a ListView with CheckBoxes.</summary>
    public static class HeaderCheckBox
    {
        private const int LvmGetHeader = 0x101F;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);

        /// <param name="selectable">Which rows the header box may toggle (locked rows are skipped).</param>
        /// <param name="setAll">Called with the new state when the header box is clicked.</param>
        public static void Attach(ListView list, Func<ListViewItem, bool> selectable, Action<bool> setAll)
        {
            list.OwnerDraw = true;
            list.DrawItem += (s, e) => e.DrawDefault = true;
            list.DrawSubItem += (s, e) => e.DrawDefault = true;
            list.DrawColumnHeader += (s, e) =>
            {
                if (e.ColumnIndex != 0)
                {
                    e.DrawDefault = true;
                    return;
                }
                e.DrawBackground();
                var rows = list.Items.Cast<ListViewItem>().Where(selectable).ToList();
                int done = rows.Count(i => i.Checked);
                CheckBoxState state = rows.Count > 0 && done == rows.Count ? CheckBoxState.CheckedNormal
                    : done > 0 ? CheckBoxState.MixedNormal : CheckBoxState.UncheckedNormal;
                Size box = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
                var at = new Point(e.Bounds.Left + 4, e.Bounds.Top + (e.Bounds.Height - box.Height) / 2);
                CheckBoxRenderer.DrawCheckBox(e.Graphics, at, state);
                var text = new Rectangle(at.X + box.Width + 6, e.Bounds.Top, e.Bounds.Width - box.Width - 10, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, list.Font, text, SystemColors.ControlText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            list.ColumnClick += (s, e) =>
            {
                if (e.Column != 0) return;
                var rows = list.Items.Cast<ListViewItem>().Where(selectable).ToList();
                setAll(rows.Count == 0 || rows.Any(i => !i.Checked));
                Refresh(list);
            };
            list.ItemChecked += (s, e) => Refresh(list);
        }

        /// <summary>Repaints the header so the box follows the row check boxes.</summary>
        public static void Refresh(ListView list)
        {
            if (!list.IsHandleCreated) return;
            IntPtr header = SendMessage(list.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
            if (header != IntPtr.Zero) InvalidateRect(header, IntPtr.Zero, true);
        }
    }
}

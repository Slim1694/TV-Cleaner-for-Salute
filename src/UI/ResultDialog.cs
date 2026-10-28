using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TVCleaner.Core;

namespace TVCleaner.UI
{
    /// <summary>Scrollable summary of restore/rollback results with app names; failures first.</summary>
    public sealed class ResultDialog : Form
    {
        private ResultDialog(string title, string header, List<RestoreResult> results, Func<string, string> nameOf, string footer)
        {
            Text = title;
            ClientSize = new Size(600, 440);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            int ok = results.Count(r => r.Ok), failed = results.Count - ok;
            var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            list.Columns.Add("Приложение", 380);
            list.Columns.Add("Результат", 180);
            foreach (RestoreResult r in results.OrderBy(r => r.Ok).ThenBy(r => nameOf(r.Package)))
            {
                var item = new ListViewItem(new[] { nameOf(r.Package), r.Message }) { ToolTipText = r.Package };
                if (!r.Ok) item.ForeColor = Color.Red;
                list.Items.Add(item);
            }
            list.ShowItemToolTips = true;

            var top = new Label
            {
                Dock = DockStyle.Top, Height = 56, Padding = new Padding(10, 8, 10, 0),
                Text = header + "\r\nУспешно: " + ok + (failed > 0 ? ", не удалось: " + failed : "")
            };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56 };
            var note = new Label { Left = 10, Top = 8, Width = 440, Height = 40, Text = footer ?? "" };
            var okButton = new Button { Text = "OK", Width = 110, Height = 32, Top = 12, DialogResult = DialogResult.OK };
            bottom.Controls.AddRange(new Control[] { note, okButton });
            UiLayout.KeepRight(bottom, okButton);
            AcceptButton = okButton;
            CancelButton = okButton; // Esc closes too

            Controls.Add(list);   // added first so it fills the space left by the docked header and footer
            Controls.Add(top);
            Controls.Add(bottom);
        }

        public static void ShowResults(IWin32Window owner, string title, string header, List<RestoreResult> results,
            Func<string, string> nameOf, string footer)
        {
            using (var d = new ResultDialog(title, header, results, nameOf, footer)) d.ShowDialog(owner);
        }
    }
}

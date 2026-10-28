using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using TVCleaner.Adb;
using TVCleaner.Core;

namespace TVCleaner.UI
{
    public sealed class DonePage : UserControl
    {
        public DonePage(MainForm form, AppSession session, string summary)
        {
            Size = form.ClientSize;
            var text = new Label
            {
                Left = 20, Top = 20, Width = 840, Height = 200,
                Text = summary + "\r\n\r\nПерезагрузите телевизор и проверьте пульт, звук, HDMI-входы и Wi-Fi.\r\n" +
                       "Если что-то работает не так — нажмите «Вернуть всё как было»."
            };
            var rollback = new Button { Left = 20, Top = 240, Width = 260, Height = 36, Text = "Вернуть всё как было" };
            rollback.Click += async (s, e) => await RollbackUi.Run(this, session);
            var report = new Button { Left = 300, Top = 240, Width = 260, Height = 36, Text = "Сохранить отчёт о ТВ" };
            report.Click += (s, e) =>
            {
                using (var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = "tvcleaner-report.json" })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        File.WriteAllText(dlg.FileName, ReportWriter.Build(session.Info, session.Packages));
                }
            };
            var again = new Button { Left = 580, Top = 240, Width = 260, Height = 36, Text = "К списку приложений" };
            again.Click += async (s, e) =>
            {
                try
                {
                    await Task.Run(() => session.Refresh());
                    form.ShowSelect();
                }
                catch (AdbException ex)
                {
                    MessageBox.Show(this, ex.Message, "Нет связи");
                    form.ShowConnect();
                }
            };
            Controls.AddRange(new Control[] { text, rollback, report, again });
        }
    }
}

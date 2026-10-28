using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TVCleaner.Adb;
using TVCleaner.Core;

namespace TVCleaner.UI
{
    public sealed class RunPage : UserControl
    {
        private readonly MainForm form;
        private readonly AppSession session;
        private readonly List<PlanItem> plan;
        private readonly bool dryRun;
        private readonly ListView list = new ListView();
        private readonly ProgressBar bar = new ProgressBar();
        private readonly TextBox logBox = new TextBox();
        private readonly Button stopButton = new Button();
        private readonly Button nextButton = new Button();
        private volatile bool stopRequested;
        private bool connectionLost;
        private string summary;

        public RunPage(MainForm form, AppSession session, List<PlanItem> plan, bool dryRun)
        {
            this.form = form;
            this.session = session;
            this.plan = plan;
            this.dryRun = dryRun;
            Size = form.ClientSize;
            Padding = new Padding(12);

            // Dock layout: list on top, progress bar, log fills the middle, buttons bar at the bottom.
            list.Dock = DockStyle.Top;
            list.Height = 300;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.Columns.Add("Приложение", 300);
            list.Columns.Add("Действие", 120);
            list.Columns.Add("Результат", 400);
            foreach (PlanItem i in plan)
                list.Items.Add(new ListViewItem(new[] { i.Package.DisplayName, i.Action == ChangeAction.Uninstall ? "удалить" : "отключить", "ожидает" }));

            bar.Dock = DockStyle.Top;
            bar.Height = 22;
            bar.Maximum = plan.Count;
            var gap = new Panel { Dock = DockStyle.Top, Height = 8 };
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56 };
            stopButton.SetBounds(0, 12, 160, 36);
            stopButton.Text = "Остановить";
            stopButton.Click += (s, e) => { stopRequested = true; stopButton.Enabled = false; };
            nextButton.SetBounds(0, 12, 160, 36);
            nextButton.Text = "Далее →";
            nextButton.Enabled = false;
            nextButton.Click += (s, e) => { if (connectionLost) form.ShowConnect(); else form.ShowDone(summary); };
            bottom.Controls.AddRange(new Control[] { stopButton, nextButton });
            UiLayout.KeepRight(bottom, nextButton, 0);

            // Fill first, then docked bars (the last added docks first, nearest the edge).
            Controls.Add(logBox);
            Controls.Add(gap);
            Controls.Add(bar);
            Controls.Add(list);
            Controls.Add(bottom);
            Load += async (s, e) => await RunAll();
        }

        private async Task RunAll()
        {
            int ok = 0, failed = 0;
            string stopReason = null;
            ListViewItem current = null;
            try
            {
                ChangeRunner runner = await Task.Run(() => session.CreateRunner(dryRun, Log));
                for (int i = 0; i < plan.Count && !stopRequested; i++)
                {
                    PlanItem item = plan[i];
                    ListViewItem row = list.Items[i];
                    current = row;
                    row.SubItems[2].Text = "выполняется…";
                    StepResult r = await Task.Run(() => runner.Apply(item));
                    current = null;
                    row.SubItems[2].Text = r.Message;
                    bar.Value = i + 1;
                    if (r.Outcome == StepOutcome.Ok)
                    {
                        ok++;
                        continue;
                    }
                    if (r.Outcome == StepOutcome.Failed)
                    {
                        failed++;
                        row.ForeColor = Color.DarkOrange;
                        continue;
                    }
                    row.ForeColor = Color.Red;
                    DialogResult d = MessageBox.Show(this,
                        "После «" + item.Package.DisplayName + "» обнаружена проблема:\r\n" + r.Message +
                        "\r\n\r\nДа — откатить этот шаг и остановиться\r\nНет — продолжить\r\nОтмена — остановиться без отката",
                        "Проблема", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (d == DialogResult.Yes)
                    {
                        Log(await Task.Run(() => runner.RollbackLast()));
                        stopReason = "Остановлено после проблемы, последний шаг откатан.";
                        break;
                    }
                    if (d == DialogResult.Cancel)
                    {
                        stopReason = "Остановлено после проблемы.";
                        break;
                    }
                    ok++;
                }
                if (stopRequested && stopReason == null) stopReason = "Остановлено пользователем.";
                await Task.Run(() => session.Refresh());
            }
            catch (AdbException ex)
            {
                connectionLost = true;
                if (current != null)
                {
                    current.SubItems[2].Text = "связь потеряна";
                    current.ForeColor = Color.Red;
                }
                stopReason = ex.Message + " Журнал сохранён: подключитесь снова, чтобы продолжить или откатить.";
            }
            summary = string.Format("{0}Выполнено: {1} из {2}, не удалось: {3}.", dryRun ? "Пробный запуск. " : "", ok, plan.Count, failed);
            if (stopReason != null) summary += "\r\n" + stopReason;
            Log(summary);
            stopButton.Enabled = false;
            nextButton.Enabled = true;
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }
            logBox.AppendText(DateTime.Now.ToString("HH:mm:ss ") + message + "\r\n");
        }
    }
}

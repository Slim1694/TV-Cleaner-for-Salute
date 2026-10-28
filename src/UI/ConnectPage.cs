using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TVCleaner.Adb;
using TVCleaner.Core;

namespace TVCleaner.UI
{
    /// <summary>Compact "card": IP field with found TVs, connect, one-line status with a colored dot, TV summary.</summary>
    public sealed class ConnectPage : UserControl
    {
        private enum State { Idle, Waiting, Connected, Error }

        private const int Left0 = 24;

        private readonly MainForm form;
        private readonly AppSession session;
        private readonly ComboBox ipBox = new ComboBox();
        private readonly Button findButton = new Button();
        private readonly Button connectButton = new Button();
        private readonly Button nextButton = new Button();
        private readonly Button rollbackButton = new Button();
        private readonly Panel dot = new Panel();
        private readonly Label status = new Label();
        private readonly Label tvLabel = new Label();
        private State state = State.Idle;

        public ConnectPage(MainForm form, AppSession session)
        {
            this.form = form;
            this.session = session;
            Size = form.ClientSize;
            BackColor = Color.White;

            var title = new Label { Text = "Подключение к телевизору", Font = Theme.Title, AutoSize = true, Location = new Point(Left0, 16) };

            var ipLabel = new Label { Text = "IP телевизора", AutoSize = true, Location = new Point(Left0, 67) };
            ipBox.SetBounds(128, 63, 252, 26);
            ipBox.DropDownStyle = ComboBoxStyle.DropDown;
            if (session.Host != null) ipBox.Text = session.Host;
            findButton.SetBounds(388, 62, 104, 28);
            findButton.Text = "Найти";
            Theme.Secondary(findButton);
            findButton.Click += OnFind;
            connectButton.SetBounds(128, 98, 252, 32);
            connectButton.Text = "Подключить";
            Theme.Primary(connectButton);
            connectButton.Click += OnConnect;

            var line = new Label { BackColor = Theme.Border, Bounds = new Rectangle(Left0, 146, 468, 1) };
            dot.SetBounds(Left0, 162, 10, 10);
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(DotColor())) e.Graphics.FillEllipse(b, 0, 0, 9, 9);
            };
            status.SetBounds(Left0 + 16, 157, 452, 22);
            status.ForeColor = Theme.Muted;
            status.Text = "Не подключено";
            tvLabel.SetBounds(Left0 + 16, 180, 452, 64);

            var help = new LinkLabel { Text = "Как включить отладку по сети?", AutoSize = true, Location = new Point(Left0, 252) };
            help.LinkColor = Theme.Accent;
            help.LinkClicked += (s, e) => MessageBox.Show(this,
                "1. Скопируйте devmenu.apk из архива программы на флешку и установите его на телевизоре.\r\n" +
                "2. Откройте на ТВ приложение «Меню разработчика».\r\n" +
                "3. Включите «Отладка по сети» (на некоторых прошивках — «ADB по сети» или «Отладка ADB»).\r\n" +
                "4. Нажмите здесь «Найти» или введите IP телевизора (Настройки → Сеть).\r\n\r\n" +
                "Важно: включение отладки и изменение системных приложений может повлиять на гарантию. " +
                "Программа неофициальная и не связана со Сбером; используйте её на свой страх и риск. " +
                "Когда программа не нужна, выключайте отладку по сети.\r\n\r\n" +
                "Подробнее — в README.", "Отладка по сети");

            rollbackButton.SetBounds(Left0, 284, 200, 32);
            rollbackButton.Text = "Вернуть всё как было";
            Theme.Secondary(rollbackButton);
            rollbackButton.Visible = false;
            rollbackButton.Click += async (s, e) => { if (await RollbackUi.Run(this, session)) ShowTv(); };
            nextButton.SetBounds(388, 284, 104, 32);
            nextButton.Text = "Далее →";
            Theme.Primary(nextButton);
            nextButton.Visible = false;
            nextButton.Click += (s, e) => form.ShowSelect();

            Controls.AddRange(new Control[] { title, ipLabel, ipBox, findButton, connectButton, line, dot, status, tvLabel, help, rollbackButton, nextButton });
            BindEnter(connectButton);
            if (session.Info != null && session.Connection != null) ShowTv();
        }

        private void BindEnter(Button b)
        {
            ipBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; b.PerformClick(); } };
        }

        private Color DotColor()
        {
            return state == State.Connected ? Theme.Good : state == State.Waiting ? Theme.Warn : state == State.Error ? Theme.Bad : Theme.Idle;
        }

        private void SetStatus(State s, string text)
        {
            state = s;
            status.Text = text;
            status.ForeColor = s == State.Error ? Theme.Bad : s == State.Connected ? Color.Black : Theme.Muted;
            dot.Invalidate();
        }

        private async void OnFind(object sender, EventArgs e)
        {
            findButton.Enabled = false;
            SetStatus(State.Waiting, "Ищу телевизоры в сети…");
            try
            {
                List<string> found = await NetworkScanner.FindAsync(AdbConnection.DefaultPort, 800);
                ipBox.Items.Clear();
                foreach (string ip in found) ipBox.Items.Add(ip);
                if (found.Count > 0)
                {
                    ipBox.Text = found[0];
                    if (found.Count > 1) ipBox.DroppedDown = true;
                    SetStatus(State.Idle, found.Count == 1 ? "Найден телевизор " + found[0] : "Найдено телевизоров: " + found.Count + " — выберите в списке");
                }
                else
                {
                    SetStatus(State.Error, "Не найден телевизор с включённой отладкой по сети");
                }
            }
            finally
            {
                findButton.Enabled = true;
            }
        }

        private async void OnConnect(object sender, EventArgs e)
        {
            string host = ipBox.Text.Trim();
            if (host.Length == 0)
            {
                SetStatus(State.Error, "Введите IP-адрес телевизора");
                return;
            }
            connectButton.Enabled = false;
            findButton.Enabled = false;
            nextButton.Visible = false;
            rollbackButton.Visible = false;
            tvLabel.Text = "";
            SetStatus(State.Waiting, "Подключаюсь к " + host + "…");
            try
            {
                if (session.Connection != null) session.Connection.Dispose();
                AdbKeyStore keys = AdbKeyStore.LoadOrCreate(AppInfo.KeyFile);
                session.Connection = await Task.Run(() => AdbConnection.Connect(host, AdbConnection.DefaultPort, keys,
                    () => BeginInvoke(new Action(() => SetStatus(State.Waiting,
                        "Подтвердите на ТВ «Разрешить отладку» (отметьте «Всегда»)")))));
                session.Host = host;
                SetStatus(State.Waiting, "Читаю список приложений… (первый раз до полуминуты)");
                await Task.Run(() =>
                {
                    session.Info = new TvInspector(session.Connection, session.Db).ReadInfo();
                    session.Refresh();
                });
                ShowTv();
            }
            catch (AdbException ex)
            {
                session.Connection = null;
                SetStatus(State.Error, ex.Message);
            }
            finally
            {
                connectButton.Enabled = true;
                findButton.Enabled = true;
            }
        }

        private void ShowTv()
        {
            TvInfo i = session.Info;
            SetStatus(State.Connected, "Подключено: " + ModelName.Pretty(i.BuildInfo, i.Model));
            tvLabel.Text = i.Firmware + " · Android " + i.AndroidVersion + "\r\n" +
                           "Приложений " + session.Packages.Count + ", отключено " + session.Packages.Count(p => p.IsDisabled);
            if (!ModelName.IsTested(session.Db.TestedModels, i.BuildInfo))
                tvLabel.Text += "\r\n⚠ Модель ещё не проверялась: начните с «Рекомендуемой» и «Пробного запуска».";
            nextButton.Visible = true;
            rollbackButton.Visible = session.History != null && session.History.HasActiveChanges;
            form.SetTvTitle();
        }
    }
}

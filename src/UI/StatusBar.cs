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
    /// <summary>TV load (CPU, RAM, storage) refreshed every 5 s, plus "clear cache" and "install APK" buttons.</summary>
    public sealed class StatusBar : UserControl
    {
        private readonly AppSession session;
        private readonly Action packagesChanged;
        private readonly Meter cpu = new Meter("Процессор");
        private readonly Meter ram = new Meter("ОЗУ");
        private readonly Meter disk = new Meter("Память");
        private readonly Button cacheButton = new Button();
        private readonly Button installButton = new Button();
        private readonly ToolTip tips = new ToolTip();
        private readonly Timer timer = new Timer { Interval = 5000 };
        private TvSnapshot last;
        private bool polling;

        /// <param name="packagesChanged">Called on the UI thread after installs, so the page can rebuild its lists.</param>
        public StatusBar(AppSession session, Action packagesChanged)
        {
            this.session = session;
            this.packagesChanged = packagesChanged;
            Dock = DockStyle.Top;
            Height = 56;
            BackColor = Theme.Window;

            var meters = new Panel { Dock = DockStyle.Fill };
            meters.Controls.AddRange(new Control[] { cpu, ram, disk });
            // Meters share the free width (max 170 px each) so they never run into the buttons.
            meters.Resize += (s, e) =>
            {
                // 8 px left margin, two 16 px gaps, 8 px before the buttons
                int w = Math.Max(90, Math.Min(170, (meters.ClientSize.Width - 8 - 16 * 2 - 8) / 3));
                int x = 8;
                foreach (Meter m in new[] { cpu, ram, disk })
                {
                    m.SetBounds(x, 10, w, 36);
                    x += w + 16;
                }
            };

            var actions = new Panel { Dock = DockStyle.Right, Width = 352 };
            cacheButton.SetBounds(0, 12, 190, 32);
            cacheButton.Text = "Очистить кэш";
            Theme.Secondary(cacheButton);
            cacheButton.Click += OnClearCache;
            installButton.SetBounds(198, 12, 146, 32);
            installButton.Text = "Установить APK…";
            Theme.Secondary(installButton);
            installButton.Click += OnInstall;
            actions.Controls.AddRange(new Control[] { cacheButton, installButton });
            tips.SetToolTip(cacheButton, "Размер — оценка Android, система пересчитывает её примерно раз в сутки.");
            tips.SetToolTip(installButton, "Выбрать на компьютере один или несколько файлов .apk и установить их на телевизор.");

            Controls.Add(meters);
            Controls.Add(actions);
            Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });

            timer.Tick += async (s, e) => await Poll();
            Load += async (s, e) =>
            {
                timer.Start();
                await Poll();
                await LoadCacheSize();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                timer.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }

        private async Task Poll()
        {
            if (polling || session.Connection == null) return;
            polling = true;
            try
            {
                TvSnapshot now = await Task.Run(() => TvStats.Read(session.Connection));
                if (IsDisposed) return;
                int load = TvStats.CpuPercent(last, now);
                last = now;
                cpu.SetValue(load < 0 ? -1 : load / 100f, load < 0 ? "…" : load + "%");
                if (now.MemTotalKb > 0)
                {
                    long used = now.MemTotalKb - now.MemAvailableKb;
                    ram.SetValue((float)used / now.MemTotalKb, used / 1024 + " / " + now.MemTotalKb / 1024 + " МБ");
                }
                if (now.DataTotalKb > 0)
                {
                    disk.SetValue(1f - (float)now.DataFreeKb / now.DataTotalKb, "свободно " + Theme.Size(now.DataFreeKb * 1024));
                    tips.SetToolTip(disk, "Свободно " + Theme.Size(now.DataFreeKb * 1024) + " из " + Theme.Size(now.DataTotalKb * 1024));
                }
            }
            catch (AdbException)
            {
                if (IsDisposed) return;
                foreach (Meter m in new[] { cpu, ram, disk }) m.SetValue(-1, "нет связи");
            }
            finally
            {
                polling = false;
            }
        }

        private async Task LoadCacheSize()
        {
            try
            {
                long bytes = await Task.Run(() => CacheInfo.ReadCacheBytes(session.Connection));
                if (!IsDisposed && bytes >= 0) cacheButton.Text = "Очистить кэш (~" + Theme.Size(bytes) + ")";
            }
            catch (AdbException)
            {
                // the meters already show "нет связи"
            }
        }

        private async void OnClearCache(object sender, EventArgs e)
        {
            cacheButton.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                long freed = await Task.Run(() => CacheInfo.Clear(session.Connection));
                cacheButton.Text = "Очистить кэш";
                MessageBox.Show(this, freed > 0 ? "Освобождено ~" + Theme.Size(freed) + "." : "Кэш уже почти пуст.", "Очистка кэша");
                await Poll();
            }
            catch (AdbException ex)
            {
                MessageBox.Show(this, ex.Message, "Нет связи", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                cacheButton.Enabled = true;
            }
        }

        private async void OnInstall(object sender, EventArgs e)
        {
            string[] files;
            using (var dlg = new OpenFileDialog { Filter = "Приложения Android (*.apk)|*.apk", Multiselect = true, Title = "Выберите APK для установки" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                files = dlg.FileNames;
            }
            installButton.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                List<RestoreResult> results = await Task.Run(() =>
                {
                    List<RestoreResult> r = files.Select(f => ApkInstaller.Install(session.Connection, f)).ToList();
                    session.Refresh();
                    return r;
                });
                packagesChanged();
                ResultDialog.ShowResults(this, "Установка", "Установка приложений завершена.", results, n => n, null);
                await Poll();
            }
            catch (AdbException ex)
            {
                MessageBox.Show(this, ex.Message, "Нет связи", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                installButton.Enabled = true;
            }
        }
    }
}

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
    /// <summary>Tab "Очистка" (enabled apps to disable/remove) and tab "Отключённые и удалённые" (selective restore).</summary>
    /// <remarks>Layout uses only Dock (top bar / bottom bar / list fills the rest): anchors break when a tab page
    /// is briefly tiny during construction.</remarks>
    public sealed class SelectPage : UserControl
    {
        private readonly MainForm form;
        private readonly AppSession session;
        private readonly RadioButton recommended = new RadioButton();
        private readonly RadioButton maximum = new RadioButton();
        private readonly RadioButton manual = new RadioButton();
        private readonly ListView cleanList = new ListView();
        private readonly CheckBox uninstallBox = new CheckBox();
        private readonly CheckBox dryRunBox = new CheckBox();
        private readonly ListView restoreList = new ListView();
        private readonly Label restoreCount = new Label();
        private readonly Button restoreButton = new Button();
        private bool updating;
        private bool userToggling; // set only by a real mouse click / Space in the clean list

        public SelectPage(MainForm form, AppSession session, int tab)
        {
            this.form = form;
            this.session = session;
            Size = form.ClientSize;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var cleanTab = new TabPage("Очистка");
            var restoreTab = new TabPage("Отключённые и удалённые");
            BuildCleanTab(cleanTab);
            BuildRestoreTab(restoreTab);
            tabs.TabPages.Add(cleanTab);
            tabs.TabPages.Add(restoreTab);
            Controls.Add(tabs);
            Controls.Add(new StatusBar(session, ReloadLists)); // docked above the tabs
            tabs.SelectedIndex = tab;
            form.SetTvTitle();

            ReloadLists();
            recommended.Checked = true;
        }

        /// <summary>Rebuilds both lists from the session (call after the session was refreshed).</summary>
        private void ReloadLists()
        {
            FillCleanList();
            FillRestoreList();
            if (!manual.Checked) ApplyMode();
        }

        // ---------- Tab 1: cleaning ----------

        private void BuildCleanTab(TabPage page)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            recommended.SetBounds(8, 8, 230, 26);
            recommended.Text = "Рекомендуемая очистка";
            maximum.SetBounds(244, 8, 220, 26);
            maximum.Text = "Максимальная очистка";
            manual.SetBounds(470, 8, 170, 26);
            manual.Text = "Выбрать вручную";
            foreach (RadioButton r in new[] { recommended, maximum, manual }) r.CheckedChanged += OnModeChanged;
            top.Controls.AddRange(new Control[] { recommended, maximum, manual });

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 64 };
            uninstallBox.SetBounds(8, 6, 560, 26);
            uninstallBox.Text = "Удалять, где возможно (копия APK сохраняется на компьютере)";
            dryRunBox.SetBounds(8, 34, 560, 26);
            dryRunBox.Text = "Пробный запуск (показать действия, ничего не менять)";
            var next = new Button { Text = "Далее →", Width = 140, Height = 32, Top = 16 };
            Theme.Primary(next);
            next.Click += OnNext;
            bottom.Controls.AddRange(new Control[] { uninstallBox, dryRunBox, next });
            UiLayout.KeepRight(bottom, next);

            SetupList(cleanList);
            cleanList.Columns.Add("Приложение", 230);
            cleanList.Columns.Add("Пакет", 230);
            cleanList.Columns.Add("Риск", 70);
            cleanList.Columns.Add("Тип", 100);
            cleanList.Columns.Add("Что может сломаться", 300);
            cleanList.ItemCheck += OnItemCheck;
            // Windows replays check states when the list handle is created; only real input switches to manual mode.
            cleanList.MouseDown += (s, e) => userToggling = true;
            cleanList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Space) userToggling = true; };
            cleanList.MouseUp += (s, e) => userToggling = false;
            cleanList.KeyUp += (s, e) => userToggling = false;
            HeaderCheckBox.Attach(cleanList, i => !((TvPackage)i.Tag).Locked, check => SetAll(cleanList, check, true));

            page.Controls.Add(cleanList); // fill first, then the docked bars around it
            page.Controls.Add(top);
            page.Controls.Add(bottom);
        }

        private void FillCleanList()
        {
            cleanList.BeginUpdate();
            updating = true;
            cleanList.Items.Clear();
            cleanList.Groups.Clear();
            var groups = new Dictionary<string, ListViewGroup>
            {
                { "ads", new ListViewGroup("Реклама и телеметрия") },
                { "junk", new ListViewGroup("Ненужное") },
                { "optional", new ListViewGroup("Сервисы Сбера, которыми можно пользоваться") },
                { "user", new ListViewGroup("Установленные пользователем (только вручную)") },
                { "locked", new ListViewGroup("Системные и неизвестные — не трогать") }
            };
            foreach (ListViewGroup g in groups.Values) cleanList.Groups.Add(g);

            foreach (TvPackage p in session.Packages.Where(x => !x.IsDisabled).OrderBy(x => x.Locked).ThenBy(x => x.DisplayName))
            {
                string key = p.Locked ? "locked" : p.IsUserApp ? "user" : p.Info.Category.ToString().ToLowerInvariant();
                string risk = p.Info == null ? "—" : p.Info.Risk == RiskLevel.Low ? "низкий" : p.Info.Risk == RiskLevel.Medium ? "средний" : "высокий";
                string kind = p.IsSystem ? "встроенный" : "обычный";
                string note = p.Locked ? p.LockReason
                    : p.IsUserApp ? "ваше приложение"
                    : (string.IsNullOrEmpty(p.Info.Breaks) ? p.Info.Description : p.Info.Breaks);
                var item = new ListViewItem(new[] { p.DisplayName, p.Name, risk, kind, note })
                {
                    Tag = p,
                    Group = groups[key],
                    ToolTipText = p.Info != null ? p.Info.Description : note
                };
                if (p.Locked) item.ForeColor = Color.Gray;
                else if (p.Info != null && p.Info.Risk != RiskLevel.Low) item.ForeColor = Color.DarkOrange;
                cleanList.Items.Add(item);
            }
            updating = false;
            cleanList.EndUpdate();
            HeaderCheckBox.Refresh(cleanList);
        }

        private CleanMode CurrentMode()
        {
            if (recommended.Checked) return CleanMode.Recommended;
            if (maximum.Checked) return CleanMode.Maximum;
            return CleanMode.Manual;
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            var r = (RadioButton)sender;
            if (r.Checked && r != manual) ApplyMode();
        }

        private void ApplyMode()
        {
            updating = true;
            foreach (ListViewItem item in cleanList.Items)
                item.Checked = Planner.IsInMode((TvPackage)item.Tag, CurrentMode());
            updating = false;
        }

        private void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            var p = (TvPackage)cleanList.Items[e.Index].Tag;
            if (p.Locked)
            {
                e.NewValue = e.CurrentValue;
                return;
            }
            if (!updating && userToggling) manual.Checked = true;
        }

        private void OnNext(object sender, EventArgs e)
        {
            var chosen = new HashSet<string>(cleanList.CheckedItems.Cast<ListViewItem>().Select(i => ((TvPackage)i.Tag).Name));
            List<PlanItem> plan = Planner.Build(session.Packages, CleanMode.Manual, chosen, uninstallBox.Checked);
            if (plan.Count == 0)
            {
                MessageBox.Show(this, "Ничего не выбрано.", "TV Cleaner for Salute");
                return;
            }
            string lines = string.Join("\r\n", plan.Select(i =>
                (i.Action == ChangeAction.Uninstall ? "удалить: " : "отключить: ") + i.Package.DisplayName));
            string text = "Будет выполнено (" + plan.Count + "):\r\n\r\n" + lines +
                          (dryRunBox.Checked ? "\r\n\r\nПробный запуск: на телевизоре ничего не изменится." : "");
            if (MessageBox.Show(this, text, "Подтверждение", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                form.ShowRun(plan, dryRunBox.Checked);
        }

        // ---------- Tab 2: disabled and removed ----------

        private void BuildRestoreTab(TabPage page)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            var info = new Label
            {
                Left = 8, Top = 10, Width = 600, Height = 24,
                Text = "Отметьте приложения, которые нужно снова включить или вернуть из копии."
            };
            top.Controls.Add(info);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 64 };
            restoreCount.SetBounds(8, 20, 400, 26);
            restoreButton.Text = "Включить / восстановить выбранные";
            restoreButton.Width = 290;
            restoreButton.Height = 32;
            restoreButton.Top = 16;
            Theme.Primary(restoreButton);
            restoreButton.Click += OnRestore;
            bottom.Controls.AddRange(new Control[] { restoreCount, restoreButton });
            UiLayout.KeepRight(bottom, restoreButton);

            SetupList(restoreList);
            restoreList.Columns.Add("Приложение", 260);
            restoreList.Columns.Add("Пакет", 260);
            restoreList.Columns.Add("Состояние", 170);
            restoreList.Columns.Add("Кто", 120);
            HeaderCheckBox.Attach(restoreList, i => true, check => SetAll(restoreList, check, false));

            page.Controls.Add(restoreList);
            page.Controls.Add(top);
            page.Controls.Add(bottom);
        }

        private void FillRestoreList()
        {
            restoreList.BeginUpdate();
            restoreList.Items.Clear();
            List<RestoreItem> items = RestoreList.Build(session.Packages, session.History);
            foreach (RestoreItem i in items.OrderBy(x => !x.ByProgram).ThenBy(x => session.NameOf(x.Package)))
            {
                restoreList.Items.Add(new ListViewItem(new[]
                {
                    session.NameOf(i.Package), i.Package,
                    i.Kind == RestoreKind.Disabled ? "отключён" : "удалён, есть копия",
                    i.ByProgram ? "программа" : "до программы"
                }) { Tag = i });
            }
            restoreList.EndUpdate();
            restoreCount.Text = items.Count == 0 ? "Отключённых и удалённых приложений нет." : "Всего: " + items.Count;
            restoreButton.Enabled = items.Count > 0;
            HeaderCheckBox.Refresh(restoreList);
        }

        private async void OnRestore(object sender, EventArgs e)
        {
            List<RestoreItem> chosen = restoreList.CheckedItems.Cast<ListViewItem>().Select(i => (RestoreItem)i.Tag).ToList();
            if (chosen.Count == 0)
            {
                MessageBox.Show(this, "Ничего не выбрано.", "TV Cleaner for Salute");
                return;
            }
            string lines = string.Join("\r\n", chosen.Select(i =>
                (i.Kind == RestoreKind.Disabled ? "включить: " : "восстановить: ") + session.NameOf(i.Package)));
            if (MessageBox.Show(this, "Будет выполнено (" + chosen.Count + "):\r\n\r\n" + lines, "Подтверждение",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            restoreButton.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                List<RestoreResult> results = await Task.Run(() =>
                {
                    List<RestoreResult> r = session.CreateRunner(false, null).Restore(chosen);
                    session.Refresh();
                    return r;
                });
                ReloadLists();
                ResultDialog.ShowResults(this, "Готово", "Выбранные приложения возвращены.", results, session.NameOf, null);
            }
            catch (AdbException ex)
            {
                MessageBox.Show(this, ex.Message, "Нет связи", MessageBoxButtons.OK, MessageBoxIcon.Error);
                form.ShowConnect();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ---------- helpers ----------

        /// <summary>Header check box: (un)checks every selectable row; on the clean tab this means manual mode.</summary>
        private void SetAll(ListView list, bool check, bool cleanTab)
        {
            if (cleanTab)
            {
                updating = true;
                manual.Checked = true;
            }
            foreach (ListViewItem item in list.Items)
            {
                var p = item.Tag as TvPackage;
                if (p != null && p.Locked) continue;
                item.Checked = check;
            }
            updating = false;
        }

        private static void SetupList(ListView list)
        {
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.CheckBoxes = true;
            list.FullRowSelect = true;
            list.ShowItemToolTips = true;
        }
    }
}

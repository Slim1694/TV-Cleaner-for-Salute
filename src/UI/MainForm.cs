using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TVCleaner.Core;

namespace TVCleaner.UI
{
    public sealed class MainForm : Form
    {
        private static readonly Size CompactSize = new Size(516, 332);
        private static readonly Size WorkSize = new Size(900, 600);
        private static readonly Size WorkMinimum = new Size(760, 500);

        private readonly AppSession session;
        private Control current;

        public MainForm(PackageDatabase db)
        {
            Text = "TV Cleaner for Salute " + AppInfo.Version;
            Font = Theme.Base;
            StartPosition = FormStartPosition.CenterScreen;
            session = new AppSession { Db = db, Store = new HistoryStore(AppInfo.Root) };
            ShowConnect();
        }

        public void ShowConnect()
        {
            ApplySize(CompactSize, CompactSize);
            ShowPage(new ConnectPage(this, session));
        }

        public void ShowSelect(int tab = 0)
        {
            ApplySize(WorkSize, WorkMinimum);
            ShowPage(new SelectPage(this, session, tab));
        }

        public void ShowRun(List<PlanItem> plan, bool dryRun)
        {
            ApplySize(WorkSize, WorkMinimum);
            ShowPage(new RunPage(this, session, plan, dryRun));
        }

        public void ShowDone(string summary)
        {
            ApplySize(WorkSize, WorkMinimum);
            ShowPage(new DonePage(this, session, summary));
        }

        /// <summary>"TV Cleaner for Salute — SBER 55 CV9632-N55 (192.168.1.50)" once a TV is connected.</summary>
        public void SetTvTitle()
        {
            Text = session.Info == null ? "TV Cleaner for Salute " + AppInfo.Version
                : "TV Cleaner for Salute " + AppInfo.Version + " — " + ModelName.Pretty(session.Info.BuildInfo, session.Info.Model) + " (" + session.Host + ")";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (session.Connection != null) session.Connection.Dispose();
            base.OnFormClosed(e);
        }

        /// <summary>Compact window for connecting, larger one for work; never shrinks a window the user enlarged.</summary>
        private void ApplySize(Size wanted, Size minimum)
        {
            Size client = ClientSize;
            MinimumSize = Size.Empty;
            bool compact = wanted == CompactSize;
            ClientSize = compact ? wanted : new Size(System.Math.Max(client.Width, wanted.Width), System.Math.Max(client.Height, wanted.Height));
            MinimumSize = SizeFromClientSize(minimum);
            if (compact) CenterToScreen();
        }

        private void ShowPage(Control page)
        {
            if (current != null)
            {
                Controls.Remove(current);
                current.Dispose();
            }
            page.Dock = DockStyle.Fill;
            Controls.Add(page);
            current = page;
        }
    }
}

using System;
using System.IO;
using System.Windows.Forms;
using TVCleaner.Core;
using TVCleaner.UI;

namespace TVCleaner
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            PackageDatabase db;
            try
            {
                db = PackageDatabase.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packages.json"));
            }
            catch (PackageDatabaseException e)
            {
                MessageBox.Show(e.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Application.Run(new MainForm(db));
        }
    }
}

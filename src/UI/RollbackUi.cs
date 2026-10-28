using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using TVCleaner.Adb;
using TVCleaner.Core;

namespace TVCleaner.UI
{
    public static class RollbackUi
    {
        /// <summary>Asks, rolls back every change on this TV, refreshes the session. True if rollback ran.</summary>
        public static async Task<bool> Run(IWin32Window owner, AppSession session)
        {
            if (MessageBox.Show(owner, "Вернуть все изменения, сделанные программой на этом телевизоре?",
                    "Откат", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
            try
            {
                List<RestoreResult> results = await Task.Run(() =>
                {
                    List<RestoreResult> r = session.CreateRunner(false, null).RollbackAll();
                    session.Refresh();
                    return r;
                });
                if (results.Count == 0)
                {
                    MessageBox.Show(owner, "Программа ещё ничего не меняла на этом телевизоре.", "Откат");
                    return false;
                }
                ResultDialog.ShowResults(owner, "Откат завершён", "Изменения программы отменены.", results,
                    session.NameOf, "Перезагрузите телевизор.");
                return true;
            }
            catch (AdbException e)
            {
                MessageBox.Show(owner, e.Message, "Откат прерван", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}

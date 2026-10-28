using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TVCleaner.Adb;

namespace TVCleaner.Core
{
    public enum StepOutcome { Ok, Failed, Unhealthy }

    public sealed class StepResult
    {
        public PlanItem Item { get; set; }
        public StepOutcome Outcome { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Applies plan items one by one with a journal entry and a health check after each, and rolls them back.</summary>
    public sealed class ChangeRunner
    {
        private readonly IAdbShell shell;
        private readonly HistoryStore store;
        private readonly TvHistory history;
        private readonly HealthCheck health;
        private readonly bool dryRun;
        private readonly Action<string> log;

        public ChangeRunner(IAdbShell shell, HistoryStore store, TvHistory history, HealthCheck health,
            bool dryRun, Action<string> log)
        {
            this.shell = shell;
            this.store = store;
            this.history = history;
            this.health = health;
            this.dryRun = dryRun;
            this.log = log != null ? log : new Action<string>(s => { });
        }

        public StepResult Apply(PlanItem item)
        {
            if (item.Package.Locked || Blocklist.IsBlocked(item.Package.Name))
                return Result(item, StepOutcome.Failed, "защищённый пакет, пропущен");
            return item.Action == ChangeAction.Uninstall ? Uninstall(item) : Disable(item);
        }

        public string RollbackLast()
        {
            for (int i = history.Entries.Count - 1; i >= 0; i--)
            {
                if (history.Entries[i].Result != HistoryEntry.Ok) continue;
                RestoreResult r = RollbackEntry(history.Entries[i]);
                return r.Package + ": " + r.Message;
            }
            return "Нечего откатывать";
        }

        /// <summary>Returns the TV to the snapshot state; one result per package (repeated entries collapse).</summary>
        public List<RestoreResult> RollbackAll()
        {
            var results = new List<RestoreResult>();
            for (int i = history.Entries.Count - 1; i >= 0; i--)
                if (history.Entries[i].Result == HistoryEntry.Ok) results.Add(RollbackEntry(history.Entries[i]));
            return results;
        }

        /// <summary>Enables disabled packages and reinstalls uninstalled ones chosen by the user.</summary>
        public List<RestoreResult> Restore(IEnumerable<RestoreItem> items)
        {
            var results = new List<RestoreResult>();
            foreach (RestoreItem item in items)
            {
                if (dryRun)
                {
                    log("[пробный запуск] восстановление " + item.Package);
                    results.Add(Restored(item.Package, true, "пробный запуск"));
                    continue;
                }
                bool ok = item.Kind == RestoreKind.Disabled ? Enable(item.Package) : Reinstall(item.Backups);
                if (ok)
                {
                    MarkRolledBack(item.Package, item.Kind == RestoreKind.Disabled ? HistoryEntry.ActionDisable : HistoryEntry.ActionUninstall);
                    log((item.Kind == RestoreKind.Disabled ? "Включён " : "Восстановлен ") + item.Package);
                }
                results.Add(Restored(item.Package, ok, ok
                    ? (item.Kind == RestoreKind.Disabled ? "включён" : "восстановлен из копии")
                    : "не удалось"));
            }
            return results;
        }

        private StepResult Disable(PlanItem item)
        {
            string name = item.Package.Name;
            string command = "pm disable-user --user 0 " + name;
            if (dryRun)
            {
                log("[пробный запуск] " + command);
                return Result(item, StepOutcome.Ok, "пробный запуск");
            }
            HistoryEntry e = AddEntry(name, HistoryEntry.ActionDisable, null);
            string output = shell.Exec(command);
            if (output.IndexOf("disabled-user", StringComparison.Ordinal) < 0)
            {
                Finish(e, HistoryEntry.Failed);
                return Result(item, StepOutcome.Failed, output.Trim());
            }
            Finish(e, HistoryEntry.Ok);
            log("Отключён " + name);
            return AfterChange(item, "отключён");
        }

        private StepResult Uninstall(PlanItem item)
        {
            string name = item.Package.Name;
            List<string> paths = TvInspector.ParsePackageLines(shell.Exec("pm path " + name));
            if (dryRun)
            {
                log("[пробный запуск] копия " + paths.Count + " файл(ов) APK, затем pm uninstall " + name);
                return Result(item, StepOutcome.Ok, "пробный запуск");
            }
            List<string> backups = Backup(name, paths);
            if (backups == null)
            {
                log("Не удалось сохранить копию " + name + ", пакет будет только отключён");
                return Disable(item);
            }
            HistoryEntry e = AddEntry(name, HistoryEntry.ActionUninstall, backups);
            string output = shell.Exec("pm uninstall " + name);
            if (output.IndexOf("Success", StringComparison.Ordinal) < 0)
            {
                Finish(e, HistoryEntry.Failed);
                return Result(item, StepOutcome.Failed, output.Trim());
            }
            Finish(e, HistoryEntry.Ok);
            log("Удалён " + name + ", копия: " + backups.Count + " файл(ов)");
            return AfterChange(item, "удалён, копия сохранена");
        }

        /// <summary>Copies every APK part to the PC and checks its size; null if anything is off.</summary>
        private List<string> Backup(string name, List<string> paths)
        {
            if (paths.Count == 0) return null;
            string dir = store.BackupDir(history.Serial, name);
            Directory.CreateDirectory(dir);
            var saved = new List<string>();
            foreach (string path in paths)
            {
                byte[] data = shell.ReadBytes("cat '" + path + "'");
                long size;
                if (data.Length == 0 || !long.TryParse(shell.Exec("stat -c %s '" + path + "'").Trim(), out size) || size != data.Length)
                    return null;
                string local = Path.Combine(dir, Path.GetFileName(path));
                File.WriteAllBytes(local, data);
                saved.Add(local);
            }
            return saved;
        }

        private RestoreResult RollbackEntry(HistoryEntry e)
        {
            if (dryRun) return Restored(e.Package, true, "пробный запуск");
            bool ok;
            if (e.Action == HistoryEntry.ActionDisable)
            {
                ok = Enable(e.Package);
            }
            else
            {
                ok = Reinstall(e.Backups);
                if (ok && history.OriginallyDisabled.Contains(e.Package))
                    shell.Exec("pm disable-user --user 0 " + e.Package);
            }
            if (!ok) return Restored(e.Package, false, "откат не удался");
            MarkRolledBack(e.Package, e.Action); // also closes earlier entries of the same action
            log("Возвращён " + e.Package);
            return Restored(e.Package, true, "возвращён");
        }

        private bool Enable(string package)
        {
            return shell.Exec("pm enable " + package).IndexOf("enabled", StringComparison.Ordinal) >= 0;
        }

        private void MarkRolledBack(string package, string action)
        {
            foreach (HistoryEntry e in history.Entries)
                if (e.Package == package && e.Action == action && e.Result == HistoryEntry.Ok) e.Result = HistoryEntry.RolledBack;
            store.Save(history);
        }

        private static RestoreResult Restored(string package, bool ok, string message)
        {
            return new RestoreResult { Package = package, Ok = ok, Message = message };
        }

        private bool Reinstall(List<string> files)
        {
            if (files == null || files.Count == 0 || files.Any(f => !File.Exists(f))) return false;
            if (files.Count == 1)
            {
                byte[] apk = File.ReadAllBytes(files[0]);
                return shell.ExecWithInput("cmd package install -S " + apk.Length, apk).IndexOf("Success", StringComparison.Ordinal) >= 0;
            }
            long total = files.Sum(f => new FileInfo(f).Length);
            Match m = Regex.Match(shell.Exec("pm install-create -S " + total), @"\[(\d+)\]");
            if (!m.Success) return false;
            string id = m.Groups[1].Value;
            for (int i = 0; i < files.Count; i++)
            {
                byte[] part = File.ReadAllBytes(files[i]);
                string r = shell.ExecWithInput("pm install-write -S " + part.Length + " " + id + " " + i + "_" + Path.GetFileName(files[i]) + " -", part);
                if (r.IndexOf("Success", StringComparison.Ordinal) < 0)
                {
                    shell.Exec("pm install-abandon " + id);
                    return false;
                }
            }
            return shell.Exec("pm install-commit " + id).IndexOf("Success", StringComparison.Ordinal) >= 0;
        }

        private StepResult AfterChange(PlanItem item, string done)
        {
            string problem = health.Check();
            if (problem == null) return Result(item, StepOutcome.Ok, done);
            log("Проблема после " + item.Package.Name + ": " + problem);
            return Result(item, StepOutcome.Unhealthy, problem);
        }

        private HistoryEntry AddEntry(string package, string action, List<string> backups)
        {
            var e = new HistoryEntry
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Package = package,
                Action = action,
                Result = HistoryEntry.Pending,
                Backups = backups ?? new List<string>()
            };
            history.Entries.Add(e);
            store.Save(history);
            return e;
        }

        private void Finish(HistoryEntry e, string result)
        {
            e.Result = result;
            store.Save(history);
        }

        private static StepResult Result(PlanItem item, StepOutcome outcome, string message)
        {
            return new StepResult { Item = item, Outcome = outcome, Message = message };
        }
    }
}

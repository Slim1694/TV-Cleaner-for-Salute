namespace TVCleaner.Adb
{
    /// <summary>Runs commands on the TV. Implemented by AdbConnection; replaced by FakeShell in tests.</summary>
    public interface IAdbShell
    {
        /// <summary>Runs a shell command, returns stdout and stderr as text.</summary>
        string Exec(string command);

        /// <summary>Runs a command and returns raw stdout bytes (used to copy APK files).</summary>
        byte[] ReadBytes(string command);

        /// <summary>Runs a command, sends input to its stdin, returns stdout and stderr as text.</summary>
        string ExecWithInput(string command, byte[] input);
    }
}

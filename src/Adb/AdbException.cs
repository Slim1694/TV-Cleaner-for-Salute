using System;

namespace TVCleaner.Adb
{
    public enum AdbError { NotFound, Refused, AuthRejected, AuthTimeout, Disconnected, Timeout, Protocol }

    /// <summary>ADB failure with a message that can be shown to a non-technical user.</summary>
    public sealed class AdbException : Exception
    {
        public AdbError Error { get; private set; }

        public AdbException(AdbError error, string message) : base(message)
        {
            Error = error;
        }
    }
}

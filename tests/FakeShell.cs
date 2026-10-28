using System;
using System.Collections.Generic;
using TVCleaner.Adb;

namespace TVCleaner.Tests
{
    /// <summary>Scriptable IAdbShell: exact responses, or handlers that simulate a TV.</summary>
    public sealed class FakeShell : IAdbShell
    {
        public readonly List<string> Commands = new List<string>();
        public readonly Dictionary<string, string> Responses = new Dictionary<string, string>();
        public Func<string, string> Handler;
        public Func<string, byte[]> BytesHandler;
        public Func<string, byte[], string> InputHandler;

        public string Exec(string command)
        {
            Commands.Add(command);
            return Respond(command);
        }

        public byte[] ReadBytes(string command)
        {
            Commands.Add(command);
            byte[] b = BytesHandler != null ? BytesHandler(command) : null;
            return b ?? new byte[0];
        }

        public string ExecWithInput(string command, byte[] input)
        {
            Commands.Add(command);
            string r = InputHandler != null ? InputHandler(command, input) : null;
            return r ?? Respond(command);
        }

        private string Respond(string command)
        {
            if (Handler != null)
            {
                string r = Handler(command);
                if (r != null) return r;
            }
            string v;
            return Responses.TryGetValue(command, out v) ? v : "";
        }
    }
}

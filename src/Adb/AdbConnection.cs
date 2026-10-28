using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace TVCleaner.Adb
{
    /// <summary>ADB over TCP (TV "network debugging", port 5555): handshake, RSA auth, exec streams.</summary>
    public sealed class AdbConnection : IAdbShell, IDisposable
    {
        public const int DefaultPort = 5555;
        private const uint ProtocolVersion = 0x01000000;
        private const uint MaxData = 256 * 1024;
        private const int ConnectTimeoutMs = 5000;
        private const int CommandTimeoutMs = 30000;
        private const int UserConfirmTimeoutMs = 60000;

        private readonly object sync = new object();
        private TcpClient client;
        private NetworkStream stream;
        private uint nextLocalId = 1;
        private uint remoteMaxData = 4096;

        public string DeviceBanner { get; private set; }

        private AdbConnection() { }

        /// <summary>Connects and authorizes. waitingForUser is called when the TV shows the "Allow debugging?" prompt.</summary>
        public static AdbConnection Connect(string host, int port, AdbKeyStore keys, Action waitingForUser)
        {
            var c = new AdbConnection();
            try
            {
                c.Open(host, port);
                c.Handshake(keys, waitingForUser);
                return c;
            }
            catch
            {
                c.Dispose();
                throw;
            }
        }

        public string Exec(string command)
        {
            return Encoding.UTF8.GetString(Run(command + " 2>&1", null));
        }

        public byte[] ReadBytes(string command)
        {
            return Run(command, null);
        }

        public string ExecWithInput(string command, byte[] input)
        {
            return Encoding.UTF8.GetString(Run(command + " 2>&1", input));
        }

        public void Dispose()
        {
            try { if (stream != null) stream.Dispose(); } catch (ObjectDisposedException) { }
            if (client != null) client.Close();
        }

        private void Open(string host, int port)
        {
            client = new TcpClient();
            IAsyncResult ar = client.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
                throw new AdbException(AdbError.NotFound,
                    "Телевизор " + host + " не отвечает. Проверьте IP-адрес и что ТВ включён.");
            try
            {
                client.EndConnect(ar);
            }
            catch (SocketException)
            {
                throw new AdbException(AdbError.Refused,
                    "Телевизор " + host + " отклонил подключение. Включите на ТВ «Отладку по сети». " +
                    "Если она включена — разрешите программе доступ в сеть в брандмауэре или антивирусе.");
            }
            client.NoDelay = true;
            stream = client.GetStream();
            stream.ReadTimeout = 10000;
        }

        private void Handshake(AdbKeyStore keys, Action waitingForUser)
        {
            Send(AdbMessage.WithString(AdbMessage.CNXN, ProtocolVersion, MaxData, "host::TVCleaner"));
            bool signatureSent = false, keySent = false;
            while (true)
            {
                AdbMessage m;
                try
                {
                    m = Receive();
                }
                catch (AdbException e)
                {
                    if (!keySent) throw;
                    if (e.Error == AdbError.Timeout)
                        throw new AdbException(AdbError.AuthTimeout,
                            "Запрос на экране ТВ не подтверждён за минуту. Подключитесь снова и нажмите «Разрешить».");
                    throw new AdbException(AdbError.AuthRejected,
                        "Телевизор отклонил подключение. Подключитесь снова и нажмите на ТВ «Разрешить».");
                }

                if (m.Command == AdbMessage.CNXN)
                {
                    remoteMaxData = Math.Min(m.Arg1, MaxData);
                    DeviceBanner = m.PayloadText;
                    stream.ReadTimeout = CommandTimeoutMs;
                    return;
                }
                if (m.Command != AdbMessage.AUTH || m.Arg0 != AdbMessage.AuthToken)
                    throw new AdbException(AdbError.Protocol, "Неожиданный ответ ТВ: " + AdbMessage.CommandName(m.Command));

                if (!signatureSent)
                {
                    Send(new AdbMessage(AdbMessage.AUTH, AdbMessage.AuthSignature, 0, keys.SignToken(m.Payload)));
                    signatureSent = true;
                }
                else if (!keySent)
                {
                    string key = keys.AndroidPublicKey("TVCleaner@" + Environment.MachineName);
                    Send(AdbMessage.WithString(AdbMessage.AUTH, AdbMessage.AuthRsaPublicKey, 0, key));
                    keySent = true;
                    stream.ReadTimeout = UserConfirmTimeoutMs;
                    if (waitingForUser != null) waitingForUser();
                }
                else
                {
                    throw new AdbException(AdbError.AuthRejected,
                        "Телевизор отклонил ключ. Подключитесь снова и нажмите на ТВ «Разрешить».");
                }
            }
        }

        /// <summary>Opens "exec:command", optionally streams input to stdin, collects stdout until the TV closes the stream.</summary>
        private byte[] Run(string command, byte[] input)
        {
            lock (sync)
            {
                uint local = nextLocalId++;
                Send(AdbMessage.WithString(AdbMessage.OPEN, local, 0, "exec:" + command));
                uint remote = 0;
                int sent = 0;
                var output = new MemoryStream();
                while (true)
                {
                    AdbMessage m = Receive();
                    if (m.Arg1 != local) continue; // message for another (stale) stream
                    if (m.Command == AdbMessage.OKAY)
                    {
                        remote = m.Arg0;
                        if (input != null && sent < input.Length)
                        {
                            int n = (int)Math.Min((long)remoteMaxData, input.Length - sent);
                            var chunk = new byte[n];
                            Buffer.BlockCopy(input, sent, chunk, 0, n);
                            Send(new AdbMessage(AdbMessage.WRTE, local, remote, chunk));
                            sent += n;
                        }
                    }
                    else if (m.Command == AdbMessage.WRTE)
                    {
                        remote = m.Arg0;
                        output.Write(m.Payload, 0, m.Payload.Length);
                        Send(new AdbMessage(AdbMessage.OKAY, local, remote, null));
                    }
                    else if (m.Command == AdbMessage.CLSE)
                    {
                        if (remote == 0)
                            throw new AdbException(AdbError.Protocol, "Телевизор отказался выполнить команду: " + command);
                        Send(new AdbMessage(AdbMessage.CLSE, local, remote, null));
                        return output.ToArray();
                    }
                }
            }
        }

        private AdbMessage Receive()
        {
            byte[] header = ReadExact(AdbMessage.HeaderSize);
            int length;
            AdbMessage m;
            try
            {
                m = AdbMessage.DecodeHeader(header, out length);
            }
            catch (InvalidDataException e)
            {
                throw new AdbException(AdbError.Protocol, e.Message);
            }
            m.Payload = length > 0 ? ReadExact(length) : new byte[0];
            return m;
        }

        private byte[] ReadExact(int count)
        {
            var buf = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read;
                try
                {
                    read = stream.Read(buf, offset, count - offset);
                }
                catch (IOException e)
                {
                    throw Translate(e);
                }
                catch (ObjectDisposedException)
                {
                    throw new AdbException(AdbError.Disconnected, "Связь с телевизором потеряна.");
                }
                if (read <= 0) throw new AdbException(AdbError.Disconnected, "Связь с телевизором потеряна.");
                offset += read;
            }
            return buf;
        }

        private void Send(AdbMessage m)
        {
            byte[] b = m.Encode();
            try
            {
                stream.Write(b, 0, b.Length);
            }
            catch (IOException e)
            {
                throw Translate(e);
            }
            catch (ObjectDisposedException)
            {
                throw new AdbException(AdbError.Disconnected, "Связь с телевизором потеряна.");
            }
        }

        private static AdbException Translate(IOException e)
        {
            var se = e.InnerException as SocketException;
            if (se != null && se.SocketErrorCode == SocketError.TimedOut)
                return new AdbException(AdbError.Timeout, "Телевизор не ответил вовремя.");
            return new AdbException(AdbError.Disconnected, "Связь с телевизором потеряна.");
        }
    }
}

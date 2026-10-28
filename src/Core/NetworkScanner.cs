using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TVCleaner.Core
{
    /// <summary>Finds hosts with an open ADB port in the /24 subnets of active network adapters.</summary>
    public static class NetworkScanner
    {
        public static async Task<List<string>> FindAsync(int port, int timeoutMs)
        {
            List<Task<string>> probes = LocalSubnetHosts().Select(ip => Probe(ip, port, timeoutMs)).ToList();
            string[] results = await Task.WhenAll(probes);
            return results.Where(r => r != null).Distinct().ToList();
        }

        public static List<IPAddress> LocalSubnetHosts()
        {
            var hosts = new List<IPAddress>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                IPInterfaceProperties props = ni.GetIPProperties();
                if (props.GatewayAddresses.Count == 0) continue;
                foreach (UnicastIPAddressInformation a in props.UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] b = a.Address.GetAddressBytes();
                    for (int i = 1; i < 255; i++)
                        if (i != b[3]) hosts.Add(new IPAddress(new byte[] { b[0], b[1], b[2], (byte)i }));
                }
            }
            return hosts;
        }

        private static async Task<string> Probe(IPAddress ip, int port, int timeoutMs)
        {
            var client = new TcpClient();
            try
            {
                Task connect = client.ConnectAsync(ip, port);
                if (await Task.WhenAny(connect, Task.Delay(timeoutMs)) != connect) return null;
                await connect;
                return ip.ToString();
            }
            catch (SocketException)
            {
                return null;
            }
            finally
            {
                client.Close();
            }
        }
    }
}

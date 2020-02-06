using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using NetCoreServer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using Caliburn.Micro;
using System.Net.Http;
using System.IO;
using System.Reflection;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    public class SRSClientSession : TcpSession
    {
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly ConcurrentDictionary<string, SRClient> _clients;
        private readonly HashSet<IPAddress> _bannedIps;
        private readonly IEventAggregator _eventAggregator;

        // Received data string.
        private readonly StringBuilder _receiveBuffer = new StringBuilder();

        public string SRSGuid { get; set; }

        public SRSClientSession(ServerSync server, ConcurrentDictionary<string, SRClient> client, HashSet<IPAddress> bannedIps, IEventAggregator eventAggregator) : base(server)
        {
            _clients = client;
            _bannedIps = bannedIps;
            _eventAggregator = eventAggregator;
        }

        protected override void OnConnected()
        {
            var clientIp = (IPEndPoint)Socket.RemoteEndPoint;

            if (_bannedIps.Contains(clientIp.Address))
            {
                Disconnect();

                Logger.Warn("Disconnecting Banned Client -  " + clientIp.Address + " " + clientIp.Port);
                return;
            }

            var whiteListFile = Path.Combine(GetCurrentDirectory(), @"client-whitelist.txt");
            var whiteList = File.ReadAllLines(whiteListFile);
            if (whiteList.Contains(clientIp.Address.ToString()))
                return;

            switch (CheckVpn(clientIp.Address))
            {
                case VpnBlockResult.Safe:
                    File.AppendAllText(whiteListFile, clientIp.Address.ToString() + Environment.NewLine);
                    break;
                case VpnBlockResult.Block:
                    var client = new SRClient { ClientSession = this };
                    _eventAggregator.PublishOnUIThread(new BanClientMessage(client));
                    break;
                case VpnBlockResult.Warning:
                    break;
            }
        }

        private VpnBlockResult CheckVpn(IPAddress ipAddress)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://v2.api.iphub.info/ip/" + ipAddress)
            {
                Headers = { { "X-Key", Environment.GetEnvironmentVariable("VPNCHECKKEY", EnvironmentVariableTarget.Machine) } }
            };

            using (var response = HttpClient.SendAsync(request).GetAwaiter().GetResult())
            {
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"Unable to get VPN info. Status: {response.StatusCode}, key length: {Environment.GetEnvironmentVariable("VPNCHECKKEY", EnvironmentVariableTarget.Machine).Length}");
                    return VpnBlockResult.Warning;
                }

                var vpnResult = JsonConvert.DeserializeObject<VpnResult>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                return (VpnBlockResult)vpnResult.block;
            }
        }

        protected override void OnDisconnected()
        {
            _receiveBuffer.Clear();
            ((ServerSync)Server).HandleDisconnect(this);
        }

        private List<NetworkMessage> GetNetworkMessage()
        {
            List<NetworkMessage> messages = new List<NetworkMessage>();
            //search for a \n, extract up to that \n and then remove from buffer
            var content = _receiveBuffer.ToString();
            while (content.Length > 2 && content.Contains("\n"))
            {
                //extract message
                var message = content.Substring(0, content.IndexOf("\n", StringComparison.Ordinal) + 1);

                //now clear from buffer
                _receiveBuffer.Remove(0, message.Length);

                try
                {

                    var networkMessage = (JsonConvert.DeserializeObject<NetworkMessage>(message.Trim()));
                    //trim the received part
                    messages.Add(networkMessage);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Unable to process JSON: \n {message}");
                }


                //load in next part
                content = _receiveBuffer.ToString();
            }

            return messages;
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _receiveBuffer.Append(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

            foreach (var s in GetNetworkMessage())
            {
                ((ServerSync)Server).HandleMessage(this, s);

            }
        }

        protected override void OnError(SocketError error)
        {
            Logger.Error($"Socket Error: {error}");
        }

        protected override void OnException(Exception error)
        {
            Logger.Error(error, $"Socket Exception: {error}");
            Disconnect();
        }

        private static string GetCurrentDirectory()
        {
            //To get the location the assembly normally resides on disk or the install directory
            var currentPath = Assembly.GetExecutingAssembly().CodeBase;

            //once you have the path you get the directory with:
            var currentDirectory = Path.GetDirectoryName(currentPath);

            if (currentDirectory.StartsWith("file:\\"))
            {
                currentDirectory = currentDirectory.Replace("file:\\", "");
            }

            return currentDirectory;
        }

    }

    public enum VpnBlockResult
    {
        Safe = 0,
        Block = 1,
        Warning = 2
    }

    public class VpnResult
    {
        public string ip { get; set; }
        public string countryCode { get; set; }
        public string countryName { get; set; }
        public int asn { get; set; }
        public string isp { get; set; }
        public int block { get; set; }
    }

}

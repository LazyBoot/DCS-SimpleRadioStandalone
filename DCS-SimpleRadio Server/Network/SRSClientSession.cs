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
using System.IO;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Settings;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    public class SRSClientSession : TcpSession
    {
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

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

            var whiteListFile = Path.Combine(HoggitVpnChecker.GetCurrentDirectory(), @"client-whitelist.txt");
            var whiteList = File.ReadAllLines(whiteListFile);
            if (whiteList.Contains(clientIp.Address.ToString()))
                return;

            switch (HoggitVpnChecker.CheckVpn(clientIp.Address))
            {
                case VpnBlockResult.Safe:
                    File.AppendAllText(whiteListFile, clientIp.Address.ToString() + Environment.NewLine);
                    break;
                case VpnBlockResult.Block:
                    var client = new SRClient { ClientSession = this };
                    _eventAggregator.PublishOnUIThread(new BanClientMessage(client));
                    Disconnect();
                    Logger.Warn("Disconnecting + banning VPN Client -  " + clientIp.Address + " " + clientIp.Port);
                    break;
                case VpnBlockResult.Warning:
                    if (!ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.BLOCK_WARN_IPS).BoolValue)
                        break;

                    Disconnect();
                    Logger.Warn("Disconnecting possible VPN Client -  " + clientIp.Address + " " + clientIp.Port);
                    break;
                case VpnBlockResult.Error:
                    break;
            }
        }

        protected override void OnSent(long sent, long pending)
        {
            // Disconnect slow client with 3MB send buffer
            if (pending > 3e+6)
                Disconnect();
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

    }
}

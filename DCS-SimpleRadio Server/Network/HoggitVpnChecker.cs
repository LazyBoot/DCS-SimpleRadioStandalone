using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    static class HoggitVpnChecker
    {
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly HttpClient HttpClient = new HttpClient();

        internal static VpnBlockResult CheckVpn(IPAddress ipAddress)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://v2.api.iphub.info/ip/" + ipAddress)
            {
                Headers = { { "X-Key", Environment.GetEnvironmentVariable("VPNCHECKKEY", EnvironmentVariableTarget.Machine) } }
            };

            try
            {
                using (var response = HttpClient.SendAsync(request).GetAwaiter().GetResult())
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Warn($"Unable to get VPN info. Status: {response.StatusCode}, key length: {Environment.GetEnvironmentVariable("VPNCHECKKEY", EnvironmentVariableTarget.Machine).Length}");
                        return VpnBlockResult.Error;
                    }

                    var vpnResult = JsonConvert.DeserializeObject<VpnResult>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                    return (VpnBlockResult)vpnResult.block;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unable to contact VPN checker API");
                return VpnBlockResult.Error;
            }
        }

        internal static string GetCurrentDirectory()
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
}

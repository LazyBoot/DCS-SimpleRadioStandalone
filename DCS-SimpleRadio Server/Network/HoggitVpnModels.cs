namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    public enum VpnBlockResult
    {
        Safe = 0,
        Block = 1,
        Warning = 2,
        Error = 3
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

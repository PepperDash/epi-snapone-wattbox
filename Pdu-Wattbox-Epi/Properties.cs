using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;

namespace Pdu_Wattbox_Epi {
    public class Properties {

        [JsonProperty("control")]
        public WattboxControlProperties Control { get; set; }
        [JsonProperty("authType")]
        public string AuthType { get; set; }
        [JsonProperty("useLegacyJoinMap")]
        public bool UseLegacyJoinMap { get; set; }

        [JsonProperty("outlets")]
        public List<Outlet> Outlets {get; set;}
    }

    public class Outlet {

        [JsonProperty("key")]
        public string Key { get; set; }
        [JsonProperty("outletNumber")]
        public int OutletNumber { get; set; }
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }

    }

    public class WattboxControlProperties
    {
        [JsonProperty("tcpSshControlProperties")]
        public TcpSshPropertiesConfig TcpSshProperties { get; set; }
    }
}
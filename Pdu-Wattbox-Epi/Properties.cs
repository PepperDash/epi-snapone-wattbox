using System;
using System.Linq;
using System.Collections.Generic;
using PepperDash.Core;
using Newtonsoft.Json;


namespace Pdu_Wattbox_Epi {
    public class Properties {
        [JsonProperty("control")]
        
        public WattboxControlProperties Control { get; set; }
        [JsonProperty("authType")]
        public string AuthType { get; set; }
    }

    public class Outlet {
        public string Key { get; set; }
        public int OutletNumber { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; }

        public Outlet(string key, OutletDict outlet)
        {
            Key = key;
            OutletNumber = outlet.OutletIndex;
            Enabled = !outlet.IsInvisible;
            Name = outlet.Name;
        }
    }

    public class WattboxControlProperties
    {
        [JsonProperty("tcpSshProperties")]
        public TcpSshPropertiesConfig TcpSshProperties { get; set; }
    }

    public class OutletDict
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("outletIndex")]
        public int OutletIndex { get; set; }
        [JsonProperty("isInvisible")]
        public bool IsInvisible { get; set; }
    }

}

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.Core;


namespace Pdu_Wattbox_Epi {
    public class Properties {
        [JsonProperty("control")]
        public EssentialsControlPropertiesConfig Control { get; set; }
        [JsonProperty("authType")]
        public string AuthType { get; set; }
        [JsonProperty("parseOutletNames")]
        public bool ParseOutletNames { get; set; }
        [JsonProperty("outlets")]
        public JObject Outlets { get; set; }

        public Properties()
        {
            //Outlets = new List<Outlet>();
            //OutletsDictionary = new Dictionary<string, OutletDict>();
        }
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

        public Outlet(string key, OutletDict outlet)
        {
            Key = key;
            OutletNumber = outlet.OutletIndex;
            Enabled = !outlet.IsInvisible;
            Name = outlet.Name;
        }
        public Outlet() {}
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

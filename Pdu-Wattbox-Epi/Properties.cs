using System.Collections.Generic;
using PepperDash.Core;

namespace Pdu_Wattbox_Epi {
    public class Properties {
        public WattboxControlProperties Control { get; set; }
        public string AuthType { get; set; }

        public List<Outlet> Outlets {get; set;}
    }

    public class Outlet {
        public string key { get; set; }
        public int outletNumber { get; set; }
        public bool enabled { get; set; }
        public string name { get; set; }

    }

    public class WattboxControlProperties
    {
        public TcpSshPropertiesConfig TcpSshProperties { get; set; }
    }
}
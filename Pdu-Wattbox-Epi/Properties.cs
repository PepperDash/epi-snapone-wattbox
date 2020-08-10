using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace Pdu_Wattbox_Epi {
    public class Properties {
        public ControlPropertiesConfig Control { get; set; }
        public string AuthType { get; set; }

        public List<Outlet> Outlets {get; set;}
    }

    public class Outlet {
        public string key { get; set; }
        public int outletNumber { get; set; }
        public bool enabled { get; set; }
        public string name { get; set; }

    }
}
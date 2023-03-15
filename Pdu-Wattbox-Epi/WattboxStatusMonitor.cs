using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace Pdu_Wattbox_Epi
{
    public class WattboxStatusMonitor : StatusMonitorBase
    {
        public WattboxStatusMonitor(IKeyed parent, long warning, long error) : base(parent, warning, error)
        {
        }

        public override void Start()
        {

        }

        public override void Stop()
        {
            
        }
    }
}
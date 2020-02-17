using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common;
using PepperDash.Essentials.Bridges;
using Newtonsoft.Json;
using Crestron.SimplSharp.Reflection;
using Pdu_Wattbox_Epi.Bridge.JoinMaps;

namespace Pdu_Wattbox_Epi.Bridge {
    public static class WattboxApiExtensions 
    {
        public static void LinkToApiExt(this Wattbox WattboxDevice, BasicTriList trilist, uint joinStart, string joinMapKey) {
            WattboxJoinMap joinMap = new WattboxJoinMap(joinStart);

            Debug.Console(1, WattboxDevice, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            

            Debug.Console(2, WattboxDevice, "There are {0} Outlets", WattboxDevice.props.Outlets.Count());

            WattboxDevice.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Online]);
            trilist.StringInput[joinMap.DeviceName].StringValue = WattboxDevice.Name;

            ushort x = 0;
            for (int i = 0; i < WattboxDevice.props.Outlets.Count(); i++) {
                var j = i;
                var o = WattboxDevice.props.Outlets[j];
                if (o.enabled) {
                    trilist.SetSigTrueAction((joinMap.PowerReboot + x), () => WattboxDevice.SetOutlet(j + 1 , 3));
                    trilist.SetSigTrueAction((joinMap.PowerOn + x), () => WattboxDevice.SetOutlet(j + 1, 1));
                    trilist.SetSigTrueAction((joinMap.PowerOff + x), () => WattboxDevice.SetOutlet(j + 1, 0));

                    trilist.StringInput[joinMap.OutletName + x].StringValue = o.name;

                    WattboxDevice.IsPowerOn[j].LinkInputSig(trilist.BooleanInput[joinMap.PowerOnFb + x]);
                    WattboxDevice.IsPowerOn[j].LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOffFb + x]);
                }
                x += 3;
            }

        }
    }
}
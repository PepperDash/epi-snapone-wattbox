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
            Debug.Console(2, WattboxDevice, "There are a total of {0} Power On Feedbacks", WattboxDevice.IsPowerOn.Count());
            foreach (var item in WattboxDevice.props.Outlets)
            {
                var o = item;
                var x = (item.outletNumber - 1) * 4;
                Debug.Console(2, WattboxDevice, "x = {0}", x);
                trilist.BooleanInput[joinMap.Enabled + (ushort)x].BoolValue = o.enabled;
                trilist.SetSigTrueAction((joinMap.PowerReboot + (ushort)x), () => WattboxDevice.SetOutlet(o.outletNumber, 3));

                trilist.SetSigTrueAction((joinMap.PowerOn + (ushort)x), () => WattboxDevice.SetOutlet(o.outletNumber, 1));

                trilist.SetSigTrueAction((joinMap.PowerOff + (ushort)x), () => WattboxDevice.SetOutlet(o.outletNumber, 0));

                trilist.StringInput[joinMap.OutletName + (ushort)x].StringValue = o.name;

                WattboxDevice.IsPowerOn[o.outletNumber].LinkInputSig(trilist.BooleanInput[joinMap.PowerOnFb + (ushort)x]);
                WattboxDevice.IsPowerOn[o.outletNumber].LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOffFb + (ushort)x]);
            }

            trilist.OnlineStatusChange += new Crestron.SimplSharpPro.OnlineStatusChangeEventHandler((d, args) =>
            {
                if (args.DeviceOnLine)
                {
                    trilist.StringInput[joinMap.DeviceName].StringValue = WattboxDevice.Name;
                    foreach (var item in WattboxDevice.props.Outlets)
                    {
                        var o = item;
                        var x = (item.outletNumber - 1) * 4;
                        Debug.Console(2, WattboxDevice, "x = {0}", x);
                        trilist.BooleanInput[joinMap.Enabled + (ushort)x].BoolValue = o.enabled;

                        trilist.StringInput[joinMap.OutletName + (ushort)x].StringValue = o.name;
                    }
                }
            }
            );

        }
    }
}
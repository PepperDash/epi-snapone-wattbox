using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Core;
using System.Collections.Generic;

namespace Pdu_Wattbox_Epi
{
    public class WattboxFactory : EssentialsPluginDeviceFactory<WattboxBase>
    {
        public WattboxFactory()
        {
            MinimumEssentialsFrameworkVersion = "1.6.1";

            TypeNames = new List<string> {"wattbox"};
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Biamp Tesira Device");

            var comm = CommFactory.CreateCommForDevice(dc);

            if (comm == null)
            {
                return new WattboxHttp(dc.Key, dc.Name, dc);
            }

            return new WattboxSocket(dc.Key, dc.Name, comm, dc);
        }
    }
}
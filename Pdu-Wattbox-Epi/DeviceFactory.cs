using System;
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
            Debug.Console(1, "Factory Attempting to create new Wattbox Device");

            var method = dc.Properties["control"].Value<string>("method");

            Debug.Console(1, "Wattbox control method: {0}", method.Equals("http",StringComparison.OrdinalIgnoreCase));

            if (String.IsNullOrEmpty(method))
            {
                Debug.Console(0, Debug.ErrorLogLevel.Warning, "No valid control method found");
                throw new NullReferenceException("No valid control method found");
            }

            if (method.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Console(1, "Creating Wattbox using HTTP Comms");
                return new WattboxHttp(dc.Key, dc.Name, dc);
            }


            Debug.Console(1, "Creating Wattbox using TCP/IP Comms");
            var comm = CommFactory.CreateCommForDevice(dc);
            return new WattboxSocket(dc.Key, dc.Name, comm, dc);
        }

    }
}
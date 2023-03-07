using System;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Core;
using System.Collections.Generic;
using Wattbox.Lib;

namespace Pdu_Wattbox_Epi
{
    public class WattboxFactory : EssentialsPluginDeviceFactory<WattboxController>
    {
        public WattboxFactory()
        {
            MinimumEssentialsFrameworkVersion = "1.12.2";

            TypeNames = new List<string> {"wattbox"};
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Wattbox Device");

            var controlProperties = CommFactory.GetControlPropertiesConfig(dc);

            //var method = dc.Properties["control"].Value<string>("method");
            //var tcpProperties = dc.Properties["control"].Value<TcpSshPropertiesConfig>("tcpSshProperties");

            Debug.Console(1, "Wattbox control method: {0}", controlProperties.Method);

            IWattboxCommunications comms;

            var method = controlProperties.Method;

            var methodString = method.ToString();

            var newKey = String.Format("{0}-{1}", dc.Key, methodString);
            var newName = String.Format("{0}-{1}", dc.Name, methodString);

            if (method == eControlMethod.Http || method == eControlMethod.Https)
            {
                Debug.Console(1, "Creating Wattbox using HTTP Comms");
                comms = new WattboxHttp(newKey, newName,
                    "Basic", controlProperties.TcpSshProperties);
                DeviceManager.AddDevice(comms);

            }
            else
            {
                Debug.Console(1, "Creating Wattbox using TCP/IP Comms");
                var comm = CommFactory.CreateCommForDevice(dc);
                comms = new WattboxSocket(newKey, newName, comm,
                    controlProperties.TcpSshProperties);
            }

            return new WattboxController(dc.Key, dc.Name, comms, dc);
        }

    }
}
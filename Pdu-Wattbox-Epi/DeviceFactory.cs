using System;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Core;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
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

            IWattboxCommunications internalComms;

            var method = controlProperties.Method;

            if (method == eControlMethod.Http || method == eControlMethod.Https)
            {
                Debug.Console(1, "Creating Wattbox using HTTP Comms");

                internalComms = new WattboxHttp(String.Format("{0}-http", dc.Key), String.Format("{0}-http", dc.Name),
                    "Basic", controlProperties.TcpSshProperties);
                DeviceManager.AddDevice(internalComms);

            }
            else
            {
                Debug.Console(1, "Creating Wattbox using TCP/IP Comms");
                var comm = CommFactory.CreateCommForDevice(dc);

                internalComms = new WattboxSocket(String.Format("{0}-tcp", dc.Key), String.Format("{0}-tcp", dc.Name), comm,

                    controlProperties.TcpSshProperties);
            }
            var control = CommFactory.GetControlPropertiesConfig(dc);

            var comms = new WattboxCommunicationMonitor(internalComms, 90000, 180000, internalComms, 0, control.Method);

            return new WattboxController(dc.Key, dc.Name, comms, dc);
        }

    }
}
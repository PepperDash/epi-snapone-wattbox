using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash_Essentials_Core.Devices;
using Wattbox.Lib;

namespace Pdu_Wattbox_Epi
{
    public class WattboxOutlet : IHasPowerCycle
    {
        public readonly int OutletNumber;
        private readonly IWattboxCommunications _comms;
        public bool IsPowerOn { get; private set; }

        public WattboxOutlet(string key, string name, int outletNumber, int powerCycleTimeMs, IWattboxCommunications comms)
        {
            Key = key;
            Name = name;
            OutletNumber = outletNumber;
            _comms = comms;
            PowerCycleTimeMs = powerCycleTimeMs;

            PowerIsOnFeedback = new BoolFeedback(() => IsPowerOn);
            DeviceManager.AddDevice(this);
        }

        #region IHasPowerCycle Members

        public void PowerCycle()
        {
            _comms.SetOutlet(OutletNumber, 2);
        }

        public int PowerCycleTimeMs { get; private set; }

        #endregion

        #region IKeyName Members

        public string Name { get; private set; }

        #endregion

        #region IKeyed Members

        public string Key { get; private set; }
        #endregion

        #region IHasPowerControlWithFeedback Members

        public BoolFeedback PowerIsOnFeedback { get; private set; }

        #endregion

        #region IHasPowerControl Members

        public void PowerOff()
        {
            _comms.SetOutlet(OutletNumber, 0);
        }

        public void PowerOn()
        {
            _comms.SetOutlet(OutletNumber, 1);
        }

        public void PowerToggle()
        {
            if (PowerIsOnFeedback.BoolValue)
            {
                PowerOff();
                return;
            }
            PowerOn();
        }

        public void SetPowerStatus(bool state)
        {
            IsPowerOn = state;
            PowerIsOnFeedback.FireUpdate();
        }

       #endregion
    }
}
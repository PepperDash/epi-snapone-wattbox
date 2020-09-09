using System;
using System.Collections.Generic;
using Crestron.SimplSharp; // For Basic SIMPL# Classes
using PepperDash.Core;
using Wattbox.Lib;
using STRING = System.String;
using SSTRING = Crestron.SimplSharp.SimplSharpString;
using INTEGER = System.UInt16;
using SIGNED_INTEGER = System.Int16;
using SIGNED_LONG_INTEGER = System.Int32;
using LONG_INTEGER = System.UInt32;

namespace Wattbox
{
    public delegate void OutletStatusUpdateSplus(INTEGER index, INTEGER status);

    public delegate void OutletNameUpdateSplus(INTEGER index, STRING name);

    public class WattboxController:Device
    {
        private CTimer _pollTimer;
        private const INTEGER TrueSplus = 1;
        private const INTEGER FalseSplus = 0;

        private const long PollTime = 45000;
        private const long DueTime = 5000;

        private IWattboxCommunications _comms;

        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public WattboxController() : this(String.Empty)
        {
        }

        public OutletStatusUpdateSplus UpdateOutletStatus { get; set; }

        public WattboxController(string key):base(key){}

        public void Initialize(string key, string method, TcpSshPropertiesConfig tcpProperties)
        {
            Key = key;

            if (method.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                _comms = new WattboxHttp(String.Format("{0}-http", key), "Wattbox-http", String.Empty, tcpProperties);
            }
            else
            {
                var comms = new GenericTcpIpClient(String.Format("{0}-tcp", key));
                _comms = new WattboxSocket(String.Format("{0}-socket", key), "Wattbox-http", comms, tcpProperties);
            }

            _comms.UpdateOutletStatus = UpdateOutlets;
        }

        public void PollEnable(INTEGER enable)
        {
            if (enable == TrueSplus)
            {
                StartTimer();
            }
            else
            {
                StopTimer();
            }
        }

        private void StartTimer()
        {
            if (_pollTimer != null)
            {
                StopTimer();
            }

            _pollTimer = new CTimer((o) => GetStatus(), DueTime, PollTime );
        }

        private void StopTimer()
        {
            if (_pollTimer == null) return;

            _pollTimer.Stop();
            _pollTimer.Dispose();
            _pollTimer = null;
        }

        public void SetOutlet(INTEGER index, INTEGER action)
        {
            _comms.SetOutlet(index, action);
        }

        public void GetStatus()
        {
            _comms.GetStatus();
        }

        private void UpdateOutlets(List<bool> outletStatus)
        {
            var handler = UpdateOutletStatus;

            if (handler == null) return;

            for(INTEGER i = 1; i <= outletStatus.Count; i++)
            {
                handler(i, outletStatus[i] ? TrueSplus : FalseSplus);
            }
        }
    }
}

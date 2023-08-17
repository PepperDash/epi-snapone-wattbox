using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Wattbox.Lib;

namespace Pdu_Wattbox_Epi
{
    public class WattboxCommunicationMonitor : StatusMonitorBase, IWattboxCommunications
    {
        private CTimer _pollTimer;

        private readonly long _pollTime;

        private readonly eControlMethod _controlMethod;
        public FirmwareVersionUpdate UpdateFirmwareVersion { get; set; }
        public SerialUpdate UpdateSerial { get; set; }
        public HostnameUpdate UpdateHostname { get; set; }

        public long PollTime
        {
            get
            {
                return _pollTime <= 5000 ? _pollTime : 30000;  
            }
        }

        public WattboxCommunicationMonitor(IKeyed parent, long warningTime, long errorTime, IWattboxCommunications comms, long pollTime, eControlMethod controlMethod)
            : base(parent, warningTime, errorTime)
        {
            Communications = comms;
            _pollTime = pollTime;
            _controlMethod = controlMethod;

            //DeviceManager.AddDevice(this);

            UpdateFirmwareVersion = Communications.UpdateFirmwareVersion;
            UpdateSerial = Communications.UpdateSerial;
            UpdateHostname = Communications.UpdateHostname;

            Communications.TextReceived += Communications_TextReceived;

            
        }

        void Communications_TextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            Status = MonitorStatus.IsOk;
            OnStatusChange(Status);
            ResetErrorTimers();
            var handler = TextReceived;
            if (handler == null) return;
            if (e == null) return;
            handler(this, e);
        }



        public IWattboxCommunications Communications;

        public override void Start()
        {
            if (_controlMethod != eControlMethod.Http && _controlMethod != eControlMethod.Https && _controlMethod != eControlMethod.Ssh) return;
            _pollTimer = new CTimer(o => GetStatus(), null, 0, PollTime);
        }

        public override void Stop()
        {
            if (_controlMethod != eControlMethod.Http && _controlMethod != eControlMethod.Https && _controlMethod != eControlMethod.Ssh) return;
            if (_pollTimer == null) return;
            _pollTimer.Stop();
            _pollTimer = null;
            StopErrorTimers();
        }

        public void Connect()
        {
            if (Communications == null) return;
            Communications.Connect();
        }

        public void GetStatus()
        {
            StartErrorTimers();
            if (Communications == null) return;
            Communications.GetStatus();
        }

        public bool IsOnlineWattbox
        {
            get { return Communications != null && Communications.IsOnlineWattbox; }
        }

        public bool IsLoggedIn
        {
            get { return Communications != null && Communications.IsLoggedIn; }
        }

        public void SetOutlet(int index, int action)
        {
            if (Communications == null) return;
            Communications.SetOutlet(index, action);
        }


        public OutletStatusUpdate UpdateOutletStatus
        {
            get
            {
                return Communications == null ? null : Communications.UpdateOutletStatus;
            }
            set
            {
                if (Communications == null) return;
                Communications.UpdateOutletStatus = value;
            }
        }

        public OutletNameUpdate UpdateOutletName
        {
            get
            {
                return Communications == null ? null : Communications.UpdateOutletName;
            }
            set
            {
                if (Communications == null) return;
                Communications.UpdateOutletName = value;
            }
        }

        public OnlineStatusUpdate UpdateOnlineStatus
        {
            get
            {
                return Communications == null ? null : Communications.UpdateOnlineStatus;
            }
            set
            {
                if (Communications == null) return;
                Communications.UpdateOnlineStatus = value;
            }
        }

        public LoggedInStatusUpdate UpdateLoggedInStatus
        {
            get
            {
                return Communications == null ? null : Communications.UpdateLoggedInStatus;
            }
            set
            {
                if (Communications == null) return;
                Communications.UpdateLoggedInStatus = value;
            }
        }


        #region IBasicCommunication Members

        public void SendBytes(byte[] bytes)
        {
            Communications.SendBytes(bytes);
        }

        public void SendText(string text)
        {
            Communications.SendText(text);
        }

        #endregion

        #region ICommunicationReceiver Members

        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;

        public void Disconnect()
        {
            Communications.Disconnect();
        }

        public bool IsConnected { get; private set; }

        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;

        #endregion
    }
}
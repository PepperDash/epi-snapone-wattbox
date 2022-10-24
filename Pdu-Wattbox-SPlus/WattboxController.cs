// For Basic SIMPL# Classes
using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Core.WebApi.Presets;
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

    public delegate void OnlineStatusUpdate(INTEGER online);

    public delegate void LoggedInStatusUpdateSplus(INTEGER loggedIn);

    public class WattboxController : Device
    {
        private const INTEGER TrueSplus = 1;
        private const INTEGER FalseSplus = 0;

        private const int BufferSize = 1024;

        private const long PollTime = 45000;
        private const long DueTime = 5000;

        private IWattboxCommunications _comms;
        private CTimer _pollTimer;

        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public WattboxController() : this(STRING.Empty)
        {
        }

        public WattboxController(string key) : base(key)
        {
        }

        public OutletStatusUpdateSplus UpdateOutletStatus { get; set; }
        public OnlineStatusUpdate UpdateOnlineStatus { get; set; }
        public LoggedInStatusUpdateSplus UpdateLoggedInStatus { get; set; }

        public void Initialize(STRING key, STRING method, STRING ipAddress, STRING userName, STRING password,
            INTEGER port)
        {
            Debug.Console(2, this, "Initializing: {0}:{1}:{2}:{3}:{4}:{5}", key, method, ipAddress, userName, password, port);
            Initialize(key, method,
                new TcpSshPropertiesConfig {Address = ipAddress, Port = port, Username = userName, Password = password});
        }

        public void Initialize(string key, string method, TcpSshPropertiesConfig tcpProperties)
        {
            Debug.Console(2, this, Debug.ErrorLogLevel.Notice, "Initializing: {0}:{1}:{2}:{3}:{4}:{5}", key, method,
                tcpProperties.Address,
                tcpProperties.Username, tcpProperties.Password, tcpProperties.Port);
            Key = key;

            if (method.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Creating HTTP Wattbox Client");
                _comms = new WattboxHttp(String.Format("{0}-http", key), "Wattbox-http", "Basic", tcpProperties);
            }
            else
            {
                Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Creating TCP/IP Wattbox Client");
                var comms = new GenericTcpIpClient(String.Format("{0}-tcp", key), tcpProperties.Address,
                    tcpProperties.Port, BufferSize);
                comms.AutoReconnect = true;
                comms.AutoReconnectIntervalMs = 10000;
                _comms = new WattboxSocket(String.Format("{0}-socket", key), "Wattbox-tcp", comms, tcpProperties);
            }

            _comms.UpdateOutletStatus = UpdateOutlets;
            _comms.UpdateOnlineStatus = UpdateOnline;
            _comms.UpdateLoggedInStatus = UpdateLoggedIn;

            _comms.Connect();
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

            Debug.Console(2, this, Debug.ErrorLogLevel.Notice, "Starting Poll timer. {0}:{1}", DueTime, PollTime);
            _pollTimer = new CTimer((o) => GetStatus(),null, DueTime, PollTime);
        }

        private void StopTimer()
        {
            if (_pollTimer == null)
            {
                return;
            }

            //Debug.Console(2, this, Debug.ErrorLogLevel.Notice, "Stopping Poll timer. {0}:{1}", DueTime, PollTime);

            _pollTimer.Stop();
            _pollTimer.Dispose();
            _pollTimer = null;
        }

        public void SetOutlet(INTEGER index, INTEGER action)
        {
            Debug.Console(2, this, Debug.ErrorLogLevel.Notice, "Setting outlet {0} to action {1}", index, action);
            _comms.SetOutlet(index, action);
        }

        public void GetStatus()
        {
            if (_comms == null) return;
            //Debug.Console(2, this, "Polling for status");
            _comms.GetStatus();
        }

        private void UpdateOutlets(List<bool> outletStatus)
        {
            var handler = UpdateOutletStatus;

            if (handler == null)
            {
                return;
            }

            Debug.Console(2, this, "OutletStatus Count: {0}",outletStatus.Count);

            for (INTEGER i = 1; i <= outletStatus.Count; i++)
            {
                Debug.Console(2, this, "Index: {0} outletStatus.Count: {1} outletStatus: {2}", i, outletStatus.Count, outletStatus[i - 1]);
                handler(i, outletStatus[i - 1] ? TrueSplus : FalseSplus);
            }
        }

        private void UpdateOnline(bool online)
        {
            var handler = UpdateOnlineStatus;

            if (handler == null) return;

            Debug.Console(2, this, "Online: {0}", online);

            handler(online ? TrueSplus : FalseSplus);
        }

        private void UpdateLoggedIn(bool status)
        {
            var handler = UpdateLoggedInStatus;

            if (handler == null) return;

            handler(status ? TrueSplus : FalseSplus);
        }
    }
}
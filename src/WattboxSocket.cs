using System;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using PepperDash.Core;
using Serilog.Events;


namespace Wattbox.Lib
{
    public class WattboxSocket : IWattboxCommunications
    {
        private const string DelimiterOut = "\x0A";
        private const string DelimiterIn = "\x0D\x0A";
        private const string DelimiterTcp = "\x0A";
        private const string DelimiterUsername = ": ";
        public readonly IBasicCommunication Communication;
        private readonly TcpSshPropertiesConfig _config;
        private CommunicationGather _portGather;
        private CTimer _deviceInfoTimer;

        public WattboxSocket(string key, string name, IBasicCommunication comm, TcpSshPropertiesConfig tcpProperties)
        {
            Key = key;
            Name = name;

            Debug.LogMessage(LogEventLevel.Debug, this, "Made it to constructor for Wattbox Socket");
            _config = tcpProperties;

            Communication = comm;
            var socket = comm as ISocketStatus;

            if (socket != null)
            {
                socket.ConnectionChange += socket_ConnectionChange;
            }

            if (Communication is GenericSshClient)
            {
                _portGather = new CommunicationGather(Communication, DelimiterIn);
            }
            else
            {
                _portGather = new CommunicationGather(Communication, DelimiterUsername);
            }
            
            _portGather.LineReceived += PortGather_LineReceived;

            Communication.BytesReceived += Communication_BytesReceived;
            Communication.TextReceived += Communication_TextReceived;
        }

        void Communication_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
            if (e == null) return;
            var handler = BytesReceived;
            if (handler == null) return;
            handler(this, e);
        }
        void Communication_TextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            if (e == null) return;
            var handler = TextReceived;
            if (handler == null) return;
            handler(this, e);
        }

        public string Name { get; set; }

        #region IKeyed Members

        public string Key { get; set; }

        #endregion

        #region IWattboxCommunications Members

        public bool IsLoggedIn { get; set; }
        public bool IsOnlineWattbox { get; set; }
        public OutletStatusUpdate UpdateOutletStatus { get; set; }
        public OutletNameUpdate UpdateOutletName { get; set; }
        public OnlineStatusUpdate UpdateOnlineStatus { get; set; }
        public LoggedInStatusUpdate UpdateLoggedInStatus { get; set; }
        public FirmwareVersionUpdate UpdateFirmwareVersion { get; set; }
        public SerialUpdate UpdateSerial { get; set; }
        public HostnameUpdate UpdateHostname { get; set; }


        public void SetOutlet(int outletNumber, int outletStatus)
        {
            var actionString = "";

            switch (outletStatus)
            {
                case (0):
                    actionString = "OFF";
                    break;
                case (1):
                    actionString = "ON";
                    break;
                case (3):
                    actionString = "RESET";
                    break;
            }
            SendLine(String.Format("!OutletSet={0},{1}", outletNumber, actionString));
        }

        public void Connect()
        {
            Debug.LogMessage(LogEventLevel.Verbose, this, "Attempting to connect...");
            Communication.Connect();
        }

        public void GetStatus()
        {
            SendLine("?OutletStatus");
            SendLine("?OutletName");
        }


        #endregion

        private void PortGather_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            if (args.Text != "OK")
            {
                ParseResponse(args.Text);
            }
        }

        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            IsOnlineWattbox = args.Client.IsConnected;

            var handler = UpdateOnlineStatus;

            if (handler == null) return;

            handler(IsOnlineWattbox);

            if (args.Client.IsConnected)
            {
                SendLine("?Hostname");
                SendLine("?OutletName");
            }
        }

        public void ParseResponse(string data)
        {
            Debug.LogMessage(LogEventLevel.Verbose, this, "ParseResponse: {0}", data);

            if (data.Contains("#Error"))
            {
                Debug.LogMessage(LogEventLevel.Warning, this, "Error Parsing Respone - Verify device config: {0}", data);
                return;
            }

            if (data.Contains("?OutletStatus="))
            {
                var outletStatString = data.Substring(14);
                Debug.LogMessage(LogEventLevel.Verbose, this, "state substring: {0}", outletStatString);
                var outletStatusList = outletStatString.Split(',').Select(s => s == "1").ToList();

                var handler = UpdateOutletStatus;
                if (handler != null) handler(outletStatusList);

                return;
            }

            if (data.Contains("?OutletName="))
            {
                var outletNameString = data.Substring(12);
                Debug.LogMessage(LogEventLevel.Verbose, this, "name substring: {0}", outletNameString);

                // Names are wrapped in braces: {Name1},{Name2},...  A name may itself contain a
                // comma (e.g. "{RDL Devices - PS4 - UA1, BA1}"), so extract by braces rather than
                // splitting on ',' - a naive split miscounts outlets (e.g. 13 instead of 12) and
                // shifts every name after the one that contains the comma.
                var outletNameList = Regex.Matches(outletNameString, @"\{([^}]*)\}")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .ToList();

                // Fallback for any firmware that doesn't wrap names in braces.
                if (outletNameList.Count == 0)
                    outletNameList = outletNameString.Split(',').Select(s => s.Trim('{', '}')).ToList();

                var handler = UpdateOutletName;
                if (handler != null) handler(outletNameList);

                return;
            }

            if (data.Contains("?Hostname="))
            {
                var hostnameString = data.Substring(10);
                Debug.LogMessage(LogEventLevel.Verbose, this, "Hostname : {0}", hostnameString);
                var handler = UpdateHostname;
                if (handler != null) handler(hostnameString);

                _deviceInfoTimer = new CTimer(SendLine, "?ServiceTag", 250);
            }

            if (data.Contains("?ServiceTag="))
            {
                var serialString = data.Substring(12);
                Debug.LogMessage(LogEventLevel.Verbose, this, "Serial : {0}", serialString);
                var handler = UpdateSerial;
                if (handler != null) handler(serialString);

                _deviceInfoTimer = new CTimer(SendLine, "?Firmware", 250);


            }

            if (data.Contains("?Firmware="))
            {
                var firmwareString = data.Substring(10);
                Debug.LogMessage(LogEventLevel.Verbose, this, "Firmware : {0}", firmwareString);
                var handler = UpdateFirmwareVersion;
                if (handler != null) handler(firmwareString);

                _deviceInfoTimer.Dispose();

            }

            if (data.Contains("Successfully Logged In"))
            {
                IsLoggedIn = true;
                var handler = UpdateLoggedInStatus;

                if (handler == null) return;

                handler(IsLoggedIn);

                return;
            }

            if (data.Contains("Username"))
            {
                Debug.LogMessage(LogEventLevel.Verbose, this, "sending username {0}", _config.Username);
                SendLine(_config.Username);
                return;
            }

            if (!data.Contains("Password"))
            {
                return;
            }

            _portGather.LineReceived -= PortGather_LineReceived;

            //logging in changes the delmiter we're looking for...
            if (Communication is GenericSshClient)
            {
                _portGather = new CommunicationGather(Communication, DelimiterIn);
            }
            else { 
                _portGather = new CommunicationGather(Communication, DelimiterTcp); 
            }
            _portGather.LineReceived += PortGather_LineReceived;

            Debug.LogMessage(LogEventLevel.Verbose, this, "sending password {0}", _config.Password);
            SendLine(_config.Password);

        }




        public void SendLine(string data)
        {
            if (String.IsNullOrEmpty(data))
            {
                return;
            }

            Communication.SendText(String.Format("{0}{1}", data, DelimiterOut));

            if (data.Contains("!OutletSet"))
            {
                GetStatus();
            }
        }

        public void SendLine(object data)
        {
            var cmd = data as String;
            if (cmd == null || String.IsNullOrEmpty(cmd)) return;

            Communication.SendText(String.Format("{0}{1}", data, DelimiterOut));

            if (cmd.Contains("!OutletSet"))
            {
                GetStatus();
            }

        }

        #region IBasicCommunication Members

        public void SendBytes(byte[] bytes)
        {
            Communication.SendBytes(bytes);
        }

        public void SendText(string text)
        {
            Communication.SendText(text);
        }

        #endregion

        #region ICommunicationReceiver Members

        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;

        public void Disconnect()
        {
            Communication.Disconnect();
        }

        public bool IsConnected
        {
            get { return Communication.IsConnected; }
        }

        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;

        #endregion
    }
}
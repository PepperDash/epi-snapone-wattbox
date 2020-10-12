using System;
using System.Linq;
using PepperDash.Core;

namespace Wattbox.Lib
{
    public class WattboxSocket : IWattboxCommunications, IKeyed
    {
        private const string DelimiterOut = "\x0A";
        private const string DelimiterIn = "\x0A";
        private const string DelimiterUsername = ": ";
        private readonly IBasicCommunication _communication;
        private readonly TcpSshPropertiesConfig _config;
        private CommunicationGather _portGather;

        public WattboxSocket(string key, string name, IBasicCommunication comm, TcpSshPropertiesConfig tcpProperties)
        {
            Key = key;
            Name = name;

            Debug.Console(1, this, "Made it to constructor for Wattbox Socket");
            _config = tcpProperties;

            _communication = comm;
            var socket = comm as ISocketStatus;

            if (socket != null)
            {
                socket.ConnectionChange += socket_ConnectionChange;
            }

            _portGather = new CommunicationGather(_communication, DelimiterUsername);
            _portGather.LineReceived += PortGather_LineReceived;

            //AddPostActivationAction(Communication.Connect);
        }

        public string Name { get; set; }

        #region IKeyed Members

        public string Key { get; set; }

        #endregion

        #region IWattboxCommunications Members

        public bool IsOnline { get; set; }
        public OutletStatusUpdate UpdateOutletStatus { get; set; }
        public OnlineStatusUpdate UpdateOnlineStatus { get; set; }

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
            Debug.Console(2, this, "Attempting to connect...");
            _communication.Connect();
        }

        public void GetStatus()
        {
            SendLine("?OutletStatus");
        }

        #endregion

        private void PortGather_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            if (Debug.Level == 2)
            {
                Debug.Console(2, this, "RX: '{0}'", args.Text);
            }
            if (args.Text != "OK")
            {
                ParseResponse(args.Text);
            }
        }

        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            IsOnline = args.Client.IsConnected;

            var handler = UpdateOnlineStatus;

            if (handler == null) return;

            handler(IsOnline);
        }

        public void ParseResponse(string data)
        {
            Debug.Console(2, this, "ParseResponse: {0}", data);

            if (data.Contains("?OutletStatus="))
            {
                var outletStatString = data.Substring(14);
                Debug.Console(2, this, "state substring: {0}", outletStatString);
                var outletStatusArray = outletStatString.Split(',').Select(s => s == "1").ToList();

                var handler = UpdateOutletStatus;

                if (handler == null)
                {
                    return;
                }

                handler(outletStatusArray);
            } /*
            else if (data.Contains("?OutletName="))
            {
                var outletNameString = data.Substring(12);
                var outletNameArray = outletNameString.Split(',');

                foreach (var item in from item in Props.Outlets let outlet = item select item)
                {
                    OutletName[item.outletNumber] = outletNameArray[item.outletNumber - 1];
                    OutletNameFeedbacks[item.outletNumber].FireUpdate();
                }
            }*/

            else if (data.Contains("Username"))
            {
                SendLine(_config.Username);
            }
            else if (data.Contains("Password"))
            {
                _portGather.LineReceived -= PortGather_LineReceived;

                //logging in changes the delmiter we're looking for...
                _portGather = new CommunicationGather(_communication, DelimiterIn);
                _portGather.LineReceived += PortGather_LineReceived;

                SendLine(_config.Password);
            }
        }

        public void SendLine(string data)
        {
            if (String.IsNullOrEmpty(data))
            {
                return;
            }

            _communication.SendText(data + DelimiterOut);

            if (data.Contains("!OutletSet"))
            {
                GetStatus();
            }
        }
    }
}
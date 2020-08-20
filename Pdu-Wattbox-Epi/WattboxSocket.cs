using System;
using System.Linq;
using System.Text;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;


namespace Pdu_Wattbox_Epi
{
    public class WattboxSocket : WattboxBase
    {
        /// <summary>
        /// Communication Object for Device
        /// </summary>
        public readonly IBasicCommunication Communication;

        public CommunicationGather PortGather { get; private set; }


        private const string Delimiter = "\x0D\x0A";


        public WattboxSocket(string key, string name, IBasicCommunication comm, DeviceConfig dc)
            : base(key, name, dc)
        {
            //Props = JsonConvert.DeserializeObject<Properties>(dc.Properties.ToString());
            Debug.Console(1, this, "Made it to constructor for Wattbox");
            Debug.Console(2, this, "Wattbox Properties : {0}", dc.Properties.ToString());

            Communication = comm;
            var socket = comm as ISocketStatus;

            if (socket != null)
            {
                socket.ConnectionChange += socket_ConnectionChange;
            }

            PortGather = new CommunicationGather(Communication, Delimiter);
            PortGather.LineReceived += PortGather_LineReceived;
        }

        void PortGather_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            if (Debug.Level == 2)
                Debug.Console(2, this, "RX: '{0}'", args.Text);
            if (args.Text != "OK")
            {
                ParseResponse(args.Text);
            }

        }

        void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            IsOnline = args.Client.IsConnected;
            IsOnlineFeedback.FireUpdate();
        }

        public override void ParseResponse(string data)
        {
            if (data.Contains("?OutletStatus="))
            {
                var outletStatString = data.Substring(15);
                var outletStatusArray = outletStatString.Split(',');

                foreach (var item in from item in Props.Outlets let outlet = item select item)
                {
                    IsPowerOn[item.outletNumber] = outletStatusArray[item.outletNumber - 1] == "1";
                    IsPowerOnFeedback[item.outletNumber].FireUpdate();
                }
            }
            else if (data.Contains("?OutletName="))
            {
                var outletNameString = data.Substring(12);
                var outletNameArray = outletNameString.Split(',');

                foreach (var item in from item in Props.Outlets let outlet = item select item)
                {
                    OutletName[item.outletNumber] = outletNameArray[item.outletNumber - 1];
                    OutletNameFeedbacks[item.outletNumber].FireUpdate();
                }
            }
        }

        public override void SetOutlet(int outletNumber, int outletStatus)
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

        public void SendLine(string data)
        {
            if (String.IsNullOrEmpty(data)) return;

            Debug.Console(1, this, "TX: '{0}'", data);
            Communication.SendText(data + Delimiter);

            if (data.Contains("!OutletSet"))
            {
                GetStatus();
            }

        }

        public override void GetStatus()
        {
            SendLine("!GetStatus");
        }


    }
}
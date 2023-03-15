using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash_Essentials_Core.Devices;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Pdu_Wattbox_Epi
{
    public class WattboxController : EssentialsBridgeableDevice, IHasControlledPowerOutlets, IDeviceInfoProvider
    {
        private const long PollTime = 45000;
        //private readonly IWattboxCommunications _comms;
        private readonly Properties _props;
        public FeedbackCollection<Feedback> Feedbacks;
        public ReadOnlyDictionary<int, IHasPowerCycle> PduOutlets { get; set; }

        private  Dictionary<int, IHasPowerCycle> TempDict { get; set; }
        public readonly WattboxCommunicationMonitor Comms;

        public DeviceInfo DeviceInfo { get; private set; }




        public readonly int OutletCount;

        public BoolFeedback IsOnlineFeedback;
        public IntFeedback OutletCountFeedback;
        public StringFeedback NameFeedback;

        private CTimer _pollTimer;


        public WattboxController(string key, string name, WattboxCommunicationMonitor comms, DeviceConfig dc)
            : base(key, name)
        {
            Comms = comms;

            Comms.UpdateOutletStatus = UpdateOutletStatus;
            Comms.UpdateOnlineStatus = UpdateOnlineStatus;
            Comms.UpdateLoggedInStatus = UpdateLoggedInStatus;
            Comms.UpdateFirmwareVersion = UpdateFirmwareVersion;
            Comms.UpdateSerial = UpdateSerial;
            Comms.UpdateHostname = UpdateHostname;

            DeviceInfo = new DeviceInfo();

            TempDict = new Dictionary<int, IHasPowerCycle>();

            if (dc.Properties != null) _props = dc.Properties.ToObject<Properties>();


            Debug.Console(2, this, "There are {0} outlets for {1}", _props.Outlets.Count(), Name);
            foreach (var item in _props.Outlets)
            {
                var i = item;
                var outlet = new WattboxOutlet(i.OutletNumber, i.Name, i.Enabled, this);
                TempDict.Add(i.OutletNumber, outlet);
                DeviceManager.AddDevice(outlet);
            }
            PduOutlets = new ReadOnlyDictionary<int, IHasPowerCycle>(TempDict);
            OutletCount = PduOutlets.Count;

            //var control = CommFactory.GetControlPropertiesConfig(dc);
            /*
            if (control.Method == eControlMethod.Http || control.Method == eControlMethod.Https)
            {
                _pollTimer = new CTimer(o => GetStatus(), null, 0, PollTime);
            }
            */



            CrestronEnvironment.ProgramStatusEventHandler += type =>
            {
                if (type != eProgramStatusEventType.Stopping) return;

                _pollTimer.Stop();
                _pollTimer.Dispose();
                _pollTimer = null;
            };
            NameFeedback = new StringFeedback(() => Name);
            IsOnlineFeedback = new BoolFeedback(() => Comms.IsOnlineWattbox);
            OutletCountFeedback = new IntFeedback(() => OutletCount);

            Feedbacks = new FeedbackCollection<Feedback>
            {
                NameFeedback,
                IsOnlineFeedback,
                OutletCountFeedback
            };
        }

        private void UpdateLoggedInStatus(bool status)
        {
            if (!status)
            {
                _pollTimer.Stop();
                _pollTimer.Dispose();
                _pollTimer = null;
                return;
            }

            if (_pollTimer == null)
            {
                _pollTimer = new CTimer(o => GetStatus(), null, 0, PollTime);
            }
        }

        private void UpdateOutletStatus(List<bool> outletStatus)
        {
            var actual = outletStatus.Count;
            var configured = _props.Outlets.Count;

            if (configured != actual)
                Debug.Console(0, this, "The number of configured outlets ({0}) does not match the number of outlets on the device ({1}).", configured, actual);

            for (var i = 0; i < actual; i++)
            {
                var outlet = PduOutlets[i + 1] as WattboxOutlet;
                if (outlet == null) continue;
                outlet.SetPowerStatus(outletStatus[i]);
                outlet.PowerIsOnFeedback.FireUpdate();
            }
        }

        private void UpdateOnlineStatus(bool online)
        {
            try
            {
                Comms.IsOnline = online;
                IsOnlineFeedback.FireUpdate();
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Exception updating online status: {0}", ex.Message);
                Debug.Console(1, this, "Exception updating online status: {1}", ex.StackTrace);
            }     
        }

        private void UpdateFirmwareVersion(string firmware)
        {
            if (DeviceInfo == null) return;
            DeviceInfo.FirmwareVersion = firmware;
            UpdateDeviceInfo();
        }
        private void UpdateHostname(string hostname)
        {
            if (DeviceInfo == null) return;
            DeviceInfo.HostName = hostname;
            DeviceInfo.IpAddress = CheckIp(hostname) ? hostname : GetIpAddress(hostname);
            DeviceInfo.MacAddress = CheckIp(hostname) ? GetMacAddress(hostname) : "00:00:00:00:00:00";
            UpdateDeviceInfo();
        }
        private void UpdateSerial(string serial)
        {
            if (DeviceInfo == null) return;
            DeviceInfo.SerialNumber = serial;
            UpdateDeviceInfo();
        }







        public void GetStatus()
        {
            Comms.GetStatus();   
        }

        public void SetOutlet(int index, int action)
        {
            Comms.SetOutlet(index, action);
        }

        public void SetOutlet(int index, EWattboxOutletSet action)
        {
            Comms.SetOutlet(index, (int)action);
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new PduJoinMapBase(joinStart);

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
                joinMap.SetCustomJoinData(customJoins);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            Debug.Console(2, this, "There are {0} Outlets", _props.Outlets.Count());

            IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Online.JoinNumber]);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Name.JoinNumber]);


            if ((int)joinMap.OutletName.JoinNumber - (int)joinStart > 0)
            {
                foreach (var o in PduOutlets.Select(outlet => outlet.Value).OfType<WattboxOutlet>())
                {
                    o.LinkOutlet(trilist, joinMap);
                }
            }

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine)
                {
                    return;
                }
                foreach (var item in Feedbacks)
                {
                    item.FireUpdate();
                }
            };
        }

        #region Overrides of Device

        public override bool CustomActivate()
        {
            Comms.Connect();
            return base.CustomActivate();
        }

        #endregion


        private string GetIpAddress(string hostname)
        {
            const string threeSeriesPattern = @"(?<=\[).+?(?=\])";
            const string fourSeriesPattern = @"(?<=\().+?(?=\))";
            var response = String.Empty;
            var cmd = String.Format("ping -n1 {0}", hostname);
            CrestronConsole.SendControlSystemCommand(cmd, ref response);


            var regex = new Regex(Global.ProcessorSeries == eCrestronSeries.Series3
                ? threeSeriesPattern
                : fourSeriesPattern);
            var match = regex.Match(response);
            return match != null ? match.ToString() : String.Empty;
        }

        private string GetMacAddress(string ipAddress)
        {
            const string macAddressPattern = @"([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})";
            var regex = new Regex(macAddressPattern);
            var response = String.Empty;
            CrestronConsole.SendControlSystemCommand("ShowArpTable", ref response);
            var addressToSearch = ipAddress;
            if (Global.ProcessorSeries == eCrestronSeries.Series3)
            {
                var octets = ipAddress.Split('.');
                var sb = new StringBuilder();
                foreach (var octet in octets)
                {
                    sb.Append(octet.PadLeft(3, ' ') + ".");
                }
                sb.Length--;
                addressToSearch = sb.ToString();
            }
            var substring = response.Substring(response.IndexOf(addressToSearch, System.StringComparison.Ordinal));
            var match = regex.Match(substring);
            return match != null ? match.ToString() : String.Empty;

        }



        #region ICommunicationMonitor Members


        #endregion


        #region IDeviceInfoProvider Members



        public event DeviceInfoChangeHandler DeviceInfoChanged;

        public void UpdateDeviceInfo()
        {
            var handler = DeviceInfoChanged;
            if (handler == null) return;
            handler(this, new DeviceInfoEventArgs(DeviceInfo));
        }

        public bool CheckIp(string data)
        {
            try
            {
                IPAddress.Parse(data);
                return true;

            }
            catch (Exception e)
            {
                var ex = e as FormatException;
                if (ex != null)
                {
                    Debug.Console(2, this, "{0} is not a valid IP Address", data);
                }
                return false;
            }
        }


        #endregion
    }

    public enum EWattboxOutletSet
    {
        PowerOff  = 0,
        PowerOn,
        PowerCycle
    }
}
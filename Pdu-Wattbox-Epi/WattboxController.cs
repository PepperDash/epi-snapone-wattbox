using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.Devices;
using PepperDash_Essentials_Core.Devices;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Pdu_Wattbox_Epi
{
    public class WattboxController : ReconfigurableDevice, IHasControlledPowerOutlets, IDeviceInfoProvider, ICommunicationMonitor
    {
        private const long PollTime = 45000;
        //private readonly IWattboxCommunications _comms;
        private readonly Properties _props;
        public FeedbackCollection<Feedback> Feedbacks;
        public ReadOnlyDictionary<int, IHasPowerCycle> PduOutlets { get; set; }

        private  Dictionary<int, IHasPowerCycle> TempDict { get; set; }
        public readonly WattboxCommunicationMonitor Comms;

        public DeviceInfo DeviceInfo { get; private set; }

        public readonly List<Outlet> Outlets;

        public StatusMonitorBase CommunicationMonitor { get; set; }

        private DeviceConfig _dc;

        public readonly int OutletCount;

        public BoolFeedback IsOnlineFeedback;
        public IntFeedback OutletCountFeedback;
        public StringFeedback NameFeedback;

        private CTimer _pollTimer;


        public WattboxController(string key, string name, WattboxCommunicationMonitor comms, DeviceConfig dc)
            : base(dc)
        {
            CommunicationMonitor = comms;

            Comms = comms;

            Comms.UpdateOutletStatus = UpdateOutletStatus;
            Comms.UpdateOnlineStatus = UpdateOnlineStatus;
            Comms.UpdateLoggedInStatus = UpdateLoggedInStatus;
            Comms.UpdateFirmwareVersion = UpdateFirmwareVersion;
            Comms.UpdateSerial = UpdateSerial;
            Comms.UpdateHostname = UpdateHostname;

            DeviceInfo = new DeviceInfo();

            TempDict = new Dictionary<int, IHasPowerCycle>();

            if (dc.Properties == null)
            {
                Debug.Console(0, this, "Malformed Json");
                return;
            }

            var outlets = new List<Outlet>();
            
            var outletsToken = dc.Properties.SelectToken("outlets");
            if (outletsToken == null)
            {
                Debug.Console(0, this, "OutletsToken is null");
                return;
            }
            if (outletsToken is JArray)
            {
                _props = dc.Properties.ToObject<Properties>();

                Debug.Console(0, this, "Found an Array");
                outlets = _props.Outlets;
                if (outlets == null)
                {
                    Debug.Console(0, this,"That Array is Null");
                    return;
                }
            }

            else if (outletsToken is JObject)
            {
                Debug.Console(0, this, "Found an Object");

                outlets = ListConvert(outletsToken.ToObject<Dictionary<string, OutletDict>>());
            }
            Outlets = outlets;

            Debug.Console(2, this, "There are {0} outlets for {1}", Outlets.Count(), Name);
            foreach (var item in Outlets)
            {
                var i = item;
                var outlet = new WattboxOutlet(i.OutletNumber, i.Name, i.Enabled, this);
                TempDict.Add(i.OutletNumber, outlet);
                DeviceManager.AddDevice(outlet);
            }
            PduOutlets = new ReadOnlyDictionary<int, IHasPowerCycle>(TempDict);
            OutletCount = PduOutlets.Count;

            CrestronEnvironment.ProgramStatusEventHandler += type =>
            {
                if (type != eProgramStatusEventType.Stopping) return;

                Comms.Stop();
                CommunicationMonitor.Stop();
                if (_pollTimer == null) return;
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



        private static List<Outlet> ListConvert(Dictionary<string, OutletDict> dict )
        {
            return (from outletDict in dict let key = outletDict.Key let value = outletDict.Value select new Outlet(key, value)).ToList();
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
            var configured = Outlets.Count;

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
            var joinMap = new WattboxJoinmapDynamic(joinStart, PduOutlets);

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
                joinMap.SetCustomJoinData(customJoins);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            JoinDataComplete joinData;
            if (joinMap.Joins.TryGetValue("SetIpAddress", out joinData))
            {
                trilist.SetStringSigAction(joinData.JoinNumber, (s) => { SetIpAddress(s); });
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            Debug.Console(2, this, "There are {0} Outlets", Outlets.Count());

            IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.BaseJoinMap.Online.JoinNumber]);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.BaseJoinMap.Name.JoinNumber]);


            if ((int)joinMap.BaseJoinMap.OutletName.JoinNumber - (int)joinStart > 0)
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

        protected override void CustomSetConfig(DeviceConfig config)
        {
            ConfigWriter.UpdateDeviceConfig(config);
        }

        private void SetIpAddress(string hostname)
        {
            try
            {
                if (hostname.Length > 2 &
                    _dc.Properties["control"]["tcpSshProperties"]["address"].ToString() != hostname)
                {
                    Debug.Console(2, this, "Changing IPAddress: {0}", hostname);

                    UpdateHostname(hostname);

                    _dc.Properties["control"]["tcpSshProperties"]["address"] = hostname;
                    CustomSetConfig(_dc);
                }
            }
            catch (Exception e)
            {
                if (Debug.Level == 2)
                    Debug.Console(2, this, "Error SetIpAddress: '{0}'", e);
            }
        }

        #region Overrides of Device

        public override bool CustomActivate()
        {
            Comms.Connect();
            Comms.Start();
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

        #region ICommunicationMonitor Members


        #endregion
    }

    public enum EWattboxOutletSet
    {
        PowerOff  = 0,
        PowerOn,
        PowerCycle
    }
}
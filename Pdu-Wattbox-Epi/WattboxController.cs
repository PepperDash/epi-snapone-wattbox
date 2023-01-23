using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash_Essentials_Core.Devices;
using Wattbox.Lib;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Pdu_Wattbox_Epi
{
    public class WattboxController : EssentialsBridgeableDevice, IHasControlledPowerOutlets
    {
        private const long PollTime = 45000;
        private readonly IWattboxCommunications _comms;
        private readonly Properties _props;
        public FeedbackCollection<Feedback> Feedbacks;
        public ReadOnlyDictionary<int, IHasPowerCycle> PduOutlets { get; set; }
        private  Dictionary<int, IHasPowerCycle> TempDict { get; set; }

        public readonly int OutletCount;

        public BoolFeedback IsOnlineFeedback;
        public IntFeedback OutletCountFeedback;
        public StringFeedback NameFeedback;

        private CTimer _pollTimer;


        public WattboxController(string key, string name, IWattboxCommunications comms, DeviceConfig dc)
            : base(key, name)
        {
            _comms = comms;


            _comms.UpdateOutletStatus = UpdateOutletStatus;
            _comms.UpdateOnlineStatus = UpdateOnlineStatus;
            _comms.UpdateLoggedInStatus = UpdateLoggedInStatus;
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

            var control = CommFactory.GetControlPropertiesConfig(dc);

            if (control.Method == eControlMethod.Http || control.Method == eControlMethod.Https)
            {
                _pollTimer = new CTimer((o) => GetStatus(), null, 0, PollTime);
            }

            CrestronEnvironment.ProgramStatusEventHandler += type =>
            {
                if (type != eProgramStatusEventType.Stopping) return;

                _pollTimer.Stop();
                _pollTimer.Dispose();
                _pollTimer = null;
            };
            NameFeedback = new StringFeedback(() => Name);
            IsOnlineFeedback = new BoolFeedback(() => _comms.IsOnline);
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
                _pollTimer = new CTimer((o) => GetStatus(), null, 0, PollTime);
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
                IsOnlineFeedback.FireUpdate();
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Exception updating online status: {0}", ex.Message);
                Debug.Console(1, this, "Exception updating online status: {1}", ex.StackTrace);
            }
            
        }

        public void GetStatus()
        {
            _comms.GetStatus();   
        }

        public void SetOutlet(int index, int action)
        {
            _comms.SetOutlet(index, action);
        }

        public void SetOutlet(int index, EWattboxOutletSet action)
        {
            _comms.SetOutlet(index, (int)action);
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new PduJoinMapBase(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            Debug.Console(2, this, "There are {0} Outlets", _props.Outlets.Count());

            IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Online.JoinNumber]);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Name.JoinNumber]);

            foreach (var outlet in PduOutlets)
            {
                var o = outlet.Value as WattboxOutlet;
                if (o == null) continue;
                o.LinkOutlet(trilist, joinMap);
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
            _comms.Connect();
            return base.CustomActivate();
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
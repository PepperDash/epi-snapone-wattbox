using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Pdu_Wattbox_Epi.Bridge.JoinMaps;
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
        private readonly Dictionary<int, bool> _isPowerOn;
        private readonly Dictionary<int, bool> _outletEnabled;
        private readonly Dictionary<int, string> _outletName;
        private readonly Properties _props;
        public FeedbackCollection<Feedback> Feedbacks;

        private bool _useLegacyJoinMap;

        public ReadOnlyDictionary<int, IHasPowerCycle> PduOutlets { get; private set; } 

        public BoolFeedback IsOnlineFeedback;

        public StringFeedback NameFeedback;

        private CTimer _pollTimer;


        public WattboxController(string key, string name, IWattboxCommunications comms, DeviceConfig dc)
            : base(key, name)
        {
            _comms = comms;

            IsOnlineFeedback = new BoolFeedback(() => _comms.IsOnline);

            _comms.UpdateOutletStatus = UpdateOutletStatus;
            _comms.UpdateOnlineStatus = UpdateOnlineStatus;
            _comms.UpdateLoggedInStatus = UpdateLoggedInStatus;

            //_IsPowerOn = new List<bool>();
            _isPowerOn = new Dictionary<int, bool>();
            IsPowerOnFeedback = new Dictionary<int, BoolFeedback>();

            OutletNameFeedbacks = new Dictionary<int, StringFeedback>();
            _outletName = new Dictionary<int, string>();

            OutletEnabledFeedbacks = new Dictionary<int, BoolFeedback>();
            _outletEnabled = new Dictionary<int, bool>();

            Feedbacks = new FeedbackCollection<Feedback>();

            _props = dc.Properties.ToObject<Properties>();

            _useLegacyJoinMap = _props.UseLegacyJoinMap;
            if (_useLegacyJoinMap)
            {
                Debug.Console(0, this, Debug.ErrorLogLevel.Notice, "This configuration uses the legacy joinmaps - these are obsolute - please consider utilizing the updated join maps");
            }

            NameFeedback = new StringFeedback(() => Name);

            Feedbacks.Add(NameFeedback);
            var outletDictionary = new Dictionary<int, IHasPowerCycle>();

            Debug.Console(2, this, "There are {0} outlets for {1}", _props.Outlets.Count(), Name);
            foreach (var item in _props.Outlets)
            {
                var i = item;

                var newOutlet = new WattboxOutlet(String.Format("{0}-{1}", this.Key, i.Name), i.Name, i.OutletNumber, 0,
                    _comms);

                Debug.Console(2, this, "The Outlet's name is {0} and it has an index of {1}", i.Name, i.OutletNumber);
                _outletEnabled.Add(newOutlet.OutletNumber, i.Enabled);
                var isPowerOnFeedback = newOutlet.PowerIsOnFeedback;
                var outletEnabledFeedback = new BoolFeedback(() => _outletEnabled[newOutlet.OutletNumber]);
                var outletNameFeedback = new StringFeedback(() => newOutlet.Name);


                IsPowerOnFeedback.Add(newOutlet.OutletNumber, isPowerOnFeedback);
                OutletEnabledFeedbacks.Add(newOutlet.OutletNumber, outletEnabledFeedback);
                OutletNameFeedbacks.Add(newOutlet.OutletNumber, outletNameFeedback);
                Feedbacks.Add(isPowerOnFeedback);
                Feedbacks.Add(outletEnabledFeedback);
                Feedbacks.Add(outletNameFeedback);
                outletDictionary.Add(newOutlet.OutletNumber, newOutlet);
                
            }

            PduOutlets = new ReadOnlyDictionary<int, IHasPowerCycle>(outletDictionary);


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
                _isPowerOn[i + 1] = outletStatus[i];
                IsPowerOnFeedback[i + 1].FireUpdate();
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

        //public List<BoolFeedback> IsPowerOnFeedback;

        public Dictionary<int, BoolFeedback> IsPowerOnFeedback { get; private set; }
        //public List<bool> _IsPowerOn;

        public Dictionary<int, StringFeedback> OutletNameFeedbacks { get; private set; }

        public Dictionary<int, BoolFeedback> OutletEnabledFeedbacks { get; private set; }


        public void GetStatus()
        {
            _comms.GetStatus();   
        }

        public void SetOutlet(int index, int action)
        {
            _comms.SetOutlet(index, action);
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = _useLegacyJoinMap ? new WattboxJoinMap(joinStart) : new PduJoinMapBase(joinStart);



            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            Debug.Console(2, this, "There are {0} Outlets", _props.Outlets.Count());

            IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Online.JoinNumber]);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Name.JoinNumber]);
            Debug.Console(2, this, "There are a total of {0} Power On Feedbacks", IsPowerOnFeedback.Count());
            foreach (var item in _props.Outlets)
            {
                var o = item;
                var x = (item.OutletNumber - 1)*4;
                Debug.Console(2, this, "x = {0}", x);

                OutletEnabledFeedbacks[o.OutletNumber].LinkInputSig(
                    trilist.BooleanInput[joinMap.OutletEnabled.JoinNumber + (ushort) x]);
                OutletNameFeedbacks[o.OutletNumber].LinkInputSig(
                    trilist.StringInput[joinMap.OutletName.JoinNumber + (ushort) x]);
                IsPowerOnFeedback[o.OutletNumber].LinkInputSig(
                    trilist.BooleanInput[joinMap.OutletPowerOn.JoinNumber + (ushort) x]);
                IsPowerOnFeedback[o.OutletNumber].LinkComplementInputSig(
                    trilist.BooleanInput[joinMap.OutletPowerOff.JoinNumber + (ushort) x]);

                trilist.SetSigTrueAction((joinMap.OutletPowerCycle.JoinNumber + (ushort) x),
                    () => SetOutlet(o.OutletNumber, 3));
                trilist.SetSigTrueAction((joinMap.OutletPowerOn.JoinNumber + (ushort)x), () => SetOutlet(o.OutletNumber, 1));
                trilist.SetSigTrueAction((joinMap.OutletPowerOff.JoinNumber + (ushort)x), () => SetOutlet(o.OutletNumber, 0));
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
}
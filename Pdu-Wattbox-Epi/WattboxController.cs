using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Pdu_Wattbox_Epi.Bridge.JoinMaps;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using Wattbox.Lib;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Pdu_Wattbox_Epi
{
    public class WattboxController : EssentialsBridgeableDevice
    {
        private readonly IWattboxCommunications _comms;
        private readonly Dictionary<int, bool> _isPowerOn;
        private readonly Dictionary<int, bool> _outletEnabled;
        private readonly Dictionary<int, string> _outletName;
        private readonly Properties _props;
        public FeedbackCollection<Feedback> Feedbacks;

        public BoolFeedback IsOnlineFeedback;

        public StringFeedback NameFeedback;
        public CTimer PollTimer;

        public WattboxController(string key, string name, IWattboxCommunications comms, DeviceConfig dc)
            : base(key, name)
        {
            _comms = comms;

            _comms.UpdateOutletStatus = UpdateOutletStatus;
            _comms.UpdateOnlineStatus = (b) => IsOnlineFeedback.FireUpdate();

            IsOnlineFeedback = new BoolFeedback(() => _comms.IsOnline);

            //_IsPowerOn = new List<bool>();
            _isPowerOn = new Dictionary<int, bool>();
            IsPowerOnFeedback = new Dictionary<int, BoolFeedback>();

            OutletNameFeedbacks = new Dictionary<int, StringFeedback>();
            _outletName = new Dictionary<int, string>();

            OutletEnabledFeedbacks = new Dictionary<int, BoolFeedback>();
            _outletEnabled = new Dictionary<int, bool>();

            Feedbacks = new FeedbackCollection<Feedback>();

            _props = dc.Properties.ToObject<Properties>();

            NameFeedback = new StringFeedback(() => Name);

            Feedbacks.Add(NameFeedback);

            Debug.Console(2, this, "There are {0} outlets for {1}", _props.Outlets.Count(), Name);
            foreach (var item in _props.Outlets)
            {
                var i = item;
                Debug.Console(2, this, "The Outlet's name is {0} and it has an index of {1}", i.name, i.outletNumber);
                _outletEnabled.Add(i.outletNumber, i.enabled);
                _isPowerOn.Add(i.outletNumber, false);
                _outletName.Add(i.outletNumber, i.name);
                var isPowerOnFeedback = new BoolFeedback(() => _isPowerOn[i.outletNumber]);
                var outletEnabledFeedback = new BoolFeedback(() => _outletEnabled[i.outletNumber]);
                var outletNameFeedback = new StringFeedback(() => _outletName[i.outletNumber]);


                IsPowerOnFeedback.Add(i.outletNumber, isPowerOnFeedback);
                OutletEnabledFeedbacks.Add(i.outletNumber, outletEnabledFeedback);
                OutletNameFeedbacks.Add(i.outletNumber, outletNameFeedback);
                Feedbacks.Add(isPowerOnFeedback);
                Feedbacks.Add(outletEnabledFeedback);
            }
        }

        private void UpdateOutletStatus(List<bool> outletStatus)
        {
            foreach (var outlet in _props.Outlets)
            {
                _isPowerOn[outlet.outletNumber] = outletStatus[outlet.outletNumber - 1];
                IsPowerOnFeedback[outlet.outletNumber].FireUpdate();
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
            var joinMap = new WattboxJoinMap(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            Debug.Console(2, this, "There are {0} Outlets", _props.Outlets.Count());

            IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Online.JoinNumber]);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceName.JoinNumber]);
            Debug.Console(2, this, "There are a total of {0} Power On Feedbacks", IsPowerOnFeedback.Count());
            foreach (var item in _props.Outlets)
            {
                var o = item;
                var x = (item.outletNumber - 1)*4;
                Debug.Console(2, this, "x = {0}", x);

                OutletEnabledFeedbacks[o.outletNumber].LinkInputSig(
                    trilist.BooleanInput[joinMap.Enabled.JoinNumber + (ushort) x]);
                OutletNameFeedbacks[o.outletNumber].LinkInputSig(
                    trilist.StringInput[joinMap.OutletName.JoinNumber + (ushort) x]);
                IsPowerOnFeedback[o.outletNumber].LinkInputSig(
                    trilist.BooleanInput[joinMap.PowerOn.JoinNumber + (ushort) x]);
                IsPowerOnFeedback[o.outletNumber].LinkComplementInputSig(
                    trilist.BooleanInput[joinMap.PowerOff.JoinNumber + (ushort) x]);

                trilist.SetSigTrueAction((joinMap.PowerReset.JoinNumber + (ushort) x),
                    () => SetOutlet(o.outletNumber, 3));
                trilist.SetSigTrueAction((joinMap.PowerOn.JoinNumber + (ushort) x), () => SetOutlet(o.outletNumber, 1));
                trilist.SetSigTrueAction((joinMap.PowerOff.JoinNumber + (ushort) x), () => SetOutlet(o.outletNumber, 0));
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
    }
}
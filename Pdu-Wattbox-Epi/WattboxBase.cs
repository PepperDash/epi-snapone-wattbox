using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Pdu_Wattbox_Epi.Bridge.JoinMaps;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Crestron.SimplSharpPro.DeviceSupport;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Pdu_Wattbox_Epi
{
    public abstract class WattboxBase : EssentialsBridgeableDevice
    {
        public Properties Props { get; set; }

        CTimer _pollTimer;

        public bool IsOnline;
        public BoolFeedback IsOnlineFeedback;

        public StringFeedback NameFeedback;
        //public List<BoolFeedback> IsPowerOnFeedback;

        public Dictionary<int, BoolFeedback> IsPowerOnFeedback;
        public Dictionary<int, bool> IsPowerOn;
        //public List<bool> _IsPowerOn;

        public Dictionary<int, StringFeedback> OutletNameFeedbacks;
        public Dictionary<int, string> OutletName;

        public Dictionary<int, BoolFeedback> OutletEnabledFeedbacks;
        public Dictionary<int, bool> OutletEnabled;

        public FeedbackCollection<Feedback> Feedbacks;

        protected WattboxBase(string key, string name, DeviceConfig dc)
            : base(key, name)
        {
            IsOnlineFeedback = new BoolFeedback(() => IsOnline);

            //_IsPowerOn = new List<bool>();
            IsPowerOn = new Dictionary<int, bool>();
            IsPowerOnFeedback = new Dictionary<int, BoolFeedback>();

            OutletNameFeedbacks = new Dictionary<int, StringFeedback>();
            OutletName = new Dictionary<int, string>();

            OutletEnabledFeedbacks = new Dictionary<int, BoolFeedback>();
            OutletEnabled = new Dictionary<int, bool>();

            Feedbacks = new FeedbackCollection<Feedback>();

            Props = dc.Properties.ToObject<Properties>();

            NameFeedback = new StringFeedback(() => Name);

            Feedbacks.Add(NameFeedback);

            Debug.Console(2, this, "There are {0} outlets for {1}", Props.Outlets.Count(), Name);
            foreach (var item in Props.Outlets)
            {
                var i = item;
                Debug.Console(2, this, "The Outlet's name is {0} and it has an index of {1}", i.name, i.outletNumber);
                OutletEnabled.Add(i.outletNumber, i.enabled);
                IsPowerOn.Add(i.outletNumber, false);
                OutletName.Add(i.outletNumber, i.name);
                var isPowerOnFeedback = new BoolFeedback(() => IsPowerOn[i.outletNumber]);
                var outletEnabledFeedback = new BoolFeedback(() => OutletEnabled[i.outletNumber]);
                var outletNameFeedback = new StringFeedback(() => OutletName[i.outletNumber]);


                IsPowerOnFeedback.Add(i.outletNumber, isPowerOnFeedback);
                OutletEnabledFeedbacks.Add(i.outletNumber, outletEnabledFeedback);
                OutletNameFeedbacks.Add(i.outletNumber, outletNameFeedback);
                Feedbacks.Add(isPowerOnFeedback);
                Feedbacks.Add(outletEnabledFeedback);
            }
        }

        public override bool CustomActivate()
        {
            _pollTimer = new CTimer(o => GetStatus(), null, 5000, 45000);
            return true;
        }

        public abstract void GetStatus();

        public abstract void SetOutlet(int index, int action);

        public abstract void ParseResponse(string data);

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new WattboxJoinMap(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            Debug.Console(2, this, "There are {0} Outlets", Props.Outlets.Count());

            IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Online.JoinNumber]);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceName.JoinNumber]);
            Debug.Console(2, this, "There are a total of {0} Power On Feedbacks", IsPowerOnFeedback.Count());
            foreach (var item in Props.Outlets)
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
                if (!args.DeviceOnLine) return;
                foreach (var item in Feedbacks)
                {
                    item.FireUpdate();
                }
            };
        }
    }
}
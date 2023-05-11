using System;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core;
using PepperDash_Essentials_Core.Devices;
using Feedback = PepperDash.Essentials.Core.Feedback;


namespace Pdu_Wattbox_Epi
{
    public class WattboxOutlet : IHasPowerCycle
    {
        public string Name { get; private set; }
        public string Key { get; private set; }
        public int PowerCycleTimeMs { get; private set; }
        public bool PowerStatus { get; private set; }
        public bool Enabled { get; private set; }



        public BoolFeedback PowerIsOnFeedback { get; private set; }
        public BoolFeedback EnabledFeedback { get; private set; }
        public StringFeedback NameFeedback { get; private set; }

        public FeedbackCollection<Feedback> Feedbacks;
        private readonly int _index;
        private readonly WattboxController _parent;



        public WattboxOutlet(int index, string name, bool enabled, WattboxController parent)
        {
            _parent = parent;
            _index = index;
            Enabled = enabled;
            Key = String.Format("{0}-{1}", _parent.Key, name);
            Name = name;
            //this doesn't matter!!!!
            PowerCycleTimeMs = 12345;
            PowerIsOnFeedback = new BoolFeedback(() => PowerStatus);
            EnabledFeedback = new BoolFeedback(() => Enabled);
            NameFeedback = new StringFeedback(() => Name);

            Feedbacks = new FeedbackCollection<Feedback>
            {
                PowerIsOnFeedback,
                EnabledFeedback,
                NameFeedback
            };
        }

        public void SetPowerStatus(bool status)
        {
            PowerStatus = status;
        }


        public void PowerCycle()
        {
            if (!Enabled) return;
            _parent.SetOutlet(_index, EWattboxOutletSet.PowerCycle);
        }

        public void PowerOff()
        {
            if (!Enabled) return;
            _parent.SetOutlet(_index, EWattboxOutletSet.PowerOff);
        }

        public void PowerOn()
        {
            if (!Enabled) return;
            _parent.SetOutlet(_index, EWattboxOutletSet.PowerOn);
        }

        public void PowerToggle()
        {
            if (!Enabled) return;
            _parent.SetOutlet(_index, PowerStatus ? EWattboxOutletSet.PowerOff : EWattboxOutletSet.PowerOn);
        }

        public void LinkOutlet(BasicTriList trilist, WattboxJoinmapDynamic joinMap)
        {
            EnabledFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Joins[string.Format("Outlet {0} Enabled", _index)].JoinNumber]);
            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Joins[string.Format("Outlet {0} Name", _index)].JoinNumber]);
            PowerIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Joins[string.Format("Outlet {0} Power On", _index)].JoinNumber]);
            PowerIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.Joins[string.Format("Outlet {0} Power Off", _index)].JoinNumber]);

            trilist.SetSigTrueAction(joinMap.Joins[string.Format("Outlet {0} Power On", _index)].JoinNumber, PowerOn);
            trilist.SetSigTrueAction(joinMap.Joins[string.Format("Outlet {0} Power Off", _index)].JoinNumber, PowerOff);
            trilist.SetSigTrueAction(joinMap.Joins[string.Format("Outlet {0} Power Cycle", _index)].JoinNumber, PowerCycle);

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
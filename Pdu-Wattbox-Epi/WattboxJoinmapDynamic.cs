using System.Collections.Generic;
using System.Linq;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash_Essentials_Core.Devices;

namespace Pdu_Wattbox_Epi
{
    public class WattboxJoinmapDynamic : JoinMapBaseAdvanced
    {
        public readonly PduJoinMapBase BaseJoinMap;

        private void SetIpChangeJoin(uint joinStart)
        {
            var joinData = new JoinData
            {

                JoinNumber = (uint)(2 + joinStart - 1),
                JoinSpan = 1
            };

            var joinMetaData = new JoinMetadata
            {
                Description = "Set device IP Address",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.DigitalSerial
            };

            var joinDataComplete = new JoinDataComplete(joinData, joinMetaData);

            Joins.Add("SetIpAddress", joinDataComplete);
        }

        public WattboxJoinmapDynamic(uint joinStart, IEnumerable<KeyValuePair<int, IHasPowerCycle>> pduOutlets)
            : base(joinStart, typeof (WattboxJoinmapDynamic))
        {
            BaseJoinMap = new PduJoinMapBase(joinStart);


            Joins.Add("Name", BaseJoinMap.Joins["Name"]);
            Joins.Add("Online", BaseJoinMap.Joins["Online"]);
            Joins.Add("OutletCount", BaseJoinMap.Joins["OutletCount"]);
            SetIpChangeJoin(joinStart);

            foreach (var index in pduOutlets.Select(outlet => outlet.Key))
            {
                SetOutletNameJoinData(index);
                SetOutletEnabledJoinData(index);
                SetOutletPowerCycleJoinData(index);
                SetOutletPowerOffJoinData(index);
                SetOutletPowerOnJoinData(index);
            }

        }
        

        private void SetOutletNameJoinData(int index)
        {

            var joinData = new JoinData
            {

                JoinNumber = (uint)((index - 1)*4 + BaseJoinMap.OutletName.JoinNumber),
                JoinSpan = 1
            };

            var joinMetaData = new JoinMetadata
            {
                Description = string.Format("Outlet {0} Name", index),
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            };

            var joinDataComplete = new JoinDataComplete(joinData, joinMetaData);

            Joins.Add(string.Format("Outlet {0} Name", index), joinDataComplete);

        }

        private void SetOutletEnabledJoinData(int index)
        {

            var joinData = new JoinData
            {

                JoinNumber = (uint)(((index - 1) * 4) + BaseJoinMap.OutletEnabled.JoinNumber),
                JoinSpan = 1
            };

            var joinMetaData = new JoinMetadata
            {
                Description = string.Format("Outlet {0} Enabled", index),
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            };

            var joinDataComplete = new JoinDataComplete(joinData, joinMetaData);

            Joins.Add(string.Format("Outlet {0} Enabled", index), joinDataComplete);

        }
        private void SetOutletPowerOffJoinData(int index)
        {

            var joinData = new JoinData
            {

                JoinNumber = (uint)(((index - 1) * 4) + BaseJoinMap.OutletPowerOff.JoinNumber),
                JoinSpan = 1
            };

            var joinMetaData = new JoinMetadata
            {
                Description = string.Format("Outlet {0} Power Off", index),
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            };

            var joinDataComplete = new JoinDataComplete(joinData, joinMetaData);

            Joins.Add(string.Format("Outlet {0} Power Off", index), joinDataComplete);

        }
        private void SetOutletPowerOnJoinData(int index)
        {

            var joinData = new JoinData
            {

                JoinNumber = (uint)(((index - 1) * 4) + BaseJoinMap.OutletPowerOn.JoinNumber),
                JoinSpan = 1
            };

            var joinMetaData = new JoinMetadata
            {
                Description = string.Format("Outlet {0} Power On", index),
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            };

            var joinDataComplete = new JoinDataComplete(joinData, joinMetaData);

            Joins.Add(string.Format("Outlet {0} Power On", index), joinDataComplete);

        }
        private void SetOutletPowerCycleJoinData(int index)
        {

            var joinData = new JoinData
            {

                JoinNumber = (uint)(((index - 1) * 4) + BaseJoinMap.OutletPowerCycle.JoinNumber),
                JoinSpan = 1
            };

            var joinMetaData = new JoinMetadata
            {
                Description = string.Format("Outlet {0} Power Cycle", index),
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            };

            var joinDataComplete = new JoinDataComplete(joinData, joinMetaData);

            Joins.Add(string.Format("Outlet {0} Power Cycle", index), joinDataComplete);

        }

    }
}
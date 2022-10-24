using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace Pdu_Wattbox_Epi.Bridge.JoinMaps
{
    public class WattboxJoinMap : PduJoinMapBase
    {

        [JoinName("Online")] public new JoinDataComplete Online =
            new JoinDataComplete(new JoinData() {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Device Online",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Enabled")] public new JoinDataComplete OutletEnabled =
            new JoinDataComplete(new JoinData() {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Outlet Enabled",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OutletPowerCycle")]
        public new JoinDataComplete OutletPowerCycle =
            new JoinDataComplete(new JoinData() {JoinNumber = 3, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Outlet Power Cycle",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OutletPowerOn")]
        public new JoinDataComplete OutletPowerOn =
            new JoinDataComplete(new JoinData() {JoinNumber = 4, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Power On Trigger and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OutletPowerOff")]
        public new JoinDataComplete OutletPowerOff =
            new JoinDataComplete(new JoinData() {JoinNumber = 5, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Outlet Power On",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Name")] public new JoinDataComplete Name =
            new JoinDataComplete(new JoinData() {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Device Online",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OutletName")] public new JoinDataComplete OutletName =
            new JoinDataComplete(new JoinData() {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Device Online",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public WattboxJoinMap(uint joinStart)
            : base(joinStart, typeof (WattboxJoinMap))
        {
        }

    }

}

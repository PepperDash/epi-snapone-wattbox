using PepperDash.Essentials.Core;

namespace Pdu_Wattbox_Epi.Bridge.JoinMaps
{
    public class WattboxJoinMap : JoinMapBaseAdvanced
    {

        [JoinName("Online")] public JoinDataComplete Online =
            new JoinDataComplete(new JoinData() {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Device Online",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Enabled")] public JoinDataComplete Enabled =
            new JoinDataComplete(new JoinData() {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Power Reset",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PowerReset")] public JoinDataComplete PowerReset =
            new JoinDataComplete(new JoinData() {JoinNumber = 3, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Power Reset",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PowerOn")] public JoinDataComplete PowerOn =
            new JoinDataComplete(new JoinData() {JoinNumber = 4, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Power On Trigger and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PowerOff")] public JoinDataComplete PowerOff =
            new JoinDataComplete(new JoinData() {JoinNumber = 5, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Power Off Trigger and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("DeviceName")] public JoinDataComplete DeviceName =
            new JoinDataComplete(new JoinData() {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Device Online",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OutletName")] public JoinDataComplete OutletName =
            new JoinDataComplete(new JoinData() {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata()
                {
                    Description = "Device Online",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        public WattboxJoinMap(uint joinStart)
            : base(joinStart, typeof (WattboxJoinMap))
        {
        }

    }

}

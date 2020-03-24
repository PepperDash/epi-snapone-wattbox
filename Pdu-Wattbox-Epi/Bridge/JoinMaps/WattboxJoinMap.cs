using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;

namespace Pdu_Wattbox_Epi.Bridge.JoinMaps {
    public class WattboxJoinMap : JoinMapBase {
        public uint Online { get; set; }
        public uint PowerOn { get; set; }
        public uint PowerOnFb { get; set; }
        public uint PowerOff { get; set; }
        public uint PowerOffFb { get; set; }
        public uint PowerReboot { get; set; }
        public uint DeviceName { get; set; }
        public uint OutletName { get; set; }
        public uint Enabled { get; set; }

        public WattboxJoinMap(uint JoinStart) {

            //Digital
            Online = 1;
            Enabled = 2;
            PowerReboot = 3;
            PowerOn = 4;
            PowerOnFb = 4;
            PowerOff = 5;
            PowerOffFb = 5;


            //Analog

            //String
            DeviceName = 1;
            OutletName = 2;

            OffsetJoinNumbers(JoinStart);

        }

        public override void OffsetJoinNumbers(uint joinStart) {
            var joinOffset = joinStart - 1;
            var properties = this.GetType().GetCType().GetProperties().Where(o => o.PropertyType == typeof(uint)).ToList();
            foreach (var property in properties) {
                property.SetValue(this, (uint)property.GetValue(this, null) + joinOffset, null);
            }
        }
    }
}
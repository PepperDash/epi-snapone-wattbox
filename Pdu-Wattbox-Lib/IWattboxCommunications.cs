using System.Collections.Generic;
using PepperDash.Core;

namespace Wattbox.Lib
{
    public delegate void OutletStatusUpdate(List<bool> outletStatus);

    public delegate void OnlineStatusUpdate(bool online);

    public delegate void LoggedInStatusUpdate(bool status);

    public delegate void FirmwareVersionUpdate(string firmware);

    public delegate void HostnameUpdate(string hostname);

    public delegate void SerialUpdate(string serial);

    public interface IWattboxCommunications : IKeyed
    {
        bool IsLoggedIn { get; }
        bool IsOnlineWattbox { get;  }
        OutletStatusUpdate UpdateOutletStatus { get; set; }
        OnlineStatusUpdate UpdateOnlineStatus { get; set; }
        LoggedInStatusUpdate UpdateLoggedInStatus { get; set; }
        FirmwareVersionUpdate UpdateFirmwareVersion { get; set; }
        HostnameUpdate UpdateHostname { get; set; }
        SerialUpdate UpdateSerial { get; set; }

        void GetStatus();
        void SetOutlet(int index, int action);

        void Connect();
    }
}
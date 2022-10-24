using System.Collections.Generic;

namespace Wattbox.Lib
{
    public delegate void OutletStatusUpdate(List<bool> outletStatus);

    public delegate void OnlineStatusUpdate(bool online);

    public delegate void LoggedInStatusUpdate(bool status);


    public interface IWattboxCommunications
    {
        bool IsLoggedIn { get; set; }
        bool IsOnline { get; set; }
        OutletStatusUpdate UpdateOutletStatus { get; set; }
        OnlineStatusUpdate UpdateOnlineStatus { get; set; }
        LoggedInStatusUpdate UpdateLoggedInStatus { get; set; }

        void GetStatus();
        void SetOutlet(int index, int action);

        void Connect();
    }
}
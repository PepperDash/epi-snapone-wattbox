using System.Collections.Generic;

namespace Wattbox.Lib
{
    public delegate void OutletStatusUpdate(List<bool> outletStatus);

    public delegate void OnlineStatusUpdate(bool online);

    public interface IWattboxCommunications
    {
        bool IsOnline { get; set; }
        OutletStatusUpdate UpdateOutletStatus { get; set; }
        OnlineStatusUpdate UpdateOnlineStatus { get; set; }

        void GetStatus();
        void SetOutlet(int index, int action);

        void Connect();
    }
}
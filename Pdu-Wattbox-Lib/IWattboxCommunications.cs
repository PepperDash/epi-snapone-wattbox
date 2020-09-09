using System.Collections.Generic;

namespace Wattbox.Lib
{
    public delegate void OutletStatusUpdate(List<bool> outletStatus);

    public interface IWattboxCommunications
    {
        bool IsOnline { get; set; }
        OutletStatusUpdate UpdateOutletStatus { get; set; }

        void GetStatus();
        void SetOutlet(int index, int action);

        void Connect();
    }
}
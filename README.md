# Essentials Snapone Wattbox Plugin

## License

Provided under MIT license

## Overview

This plugin controls Snapone Wattbox devices

## Dependencies

The [Essentials](https://github.com/PepperDash/Essentials) libraries are required. They referenced via nuget. You must have nuget.exe installed and in the `PATH` environment variable to use the following command. Nuget.exe is available at [nuget.org](https://dist.nuget.org/win-x86-commandline/latest/nuget.exe).

### Installing Dependencies

Dependencies will be installed automatically by Visual Studio on opening. Use the Nuget Package Manager in
Visual Studio to manage nuget package dependencies. All files will be output to the `output` directory at the root of
repository.

### Installing Different versions of PepperDash Core

If a different version of PepperDash Core is needed, use the Visual Studio Nuget Package Manager to install the desired
version.

# Usage

## Join Maps

Uses `PduJoinMapBase` from PepperDash.Essentials.Core.Devices library.

## Example Config

#### Device Types

Valid device types are
* `wattbox`

#### Control Methods
* `http`
* `https`
* `tcpIp`
* `ssh`


#### Example Configuration Object

```json
{
  "key": "pdu-1",
  "name": "PDU 1",
  "group": "pdu",
  "type": "wattbox",
  "properties": {
    "control": {
        // Begin TCP/SSH example
        "method": "tcpIp", 
        "tcpSshProperties": {
            "address": "192.168.0.231",
            "port": "23",
            "autoReconnect": true,
            "autoReconnectIntervalMs": 5000
        },
        // End TCP example
        // Begin HTTP/HTTPS example
        "method": "http/https" 
        "tcpSshProperties": {
            "address": "192.168.0.231",
            "port": "23",
            "autoReconnect": true,
            "autoReconnectIntervalMs": 5000,
            "username":"admin",
            "passord":"wattbox"
        }
    },
    // For HTTP/HTTPS example only
    "authorization": "basic",

    // Define the outlets
    "outlets": {
      "Outlet-01": {
          "name": "Outlet-01",
          "outletIndex": 1,
          "isInvisible": false
      },
      "Outlet-02": {
          "name": "Outlet-02",
          "outletIndex": 2,
          "isInvisible": false
      },
      "Outlet-03": {
          "name": "Outlet-03",
          "outletIndex": 3,
          "isInvisible": false
      },
      "Outlet-04": {
          "name": "Outlet-04",
          "outletIndex": 4,
          "isInvisible": false
      },
      "Outlet-05": {
          "name": "Outlet-05",
          "outletIndex": 5,
          "isInvisible": false
      },
      "Outlet-06": {
          "name": "Outlet-06",
          "outletIndex": 6,
          "isInvisible": false
      },
      "Outlet-07": {
          "name": "Outlet-07",
          "outletIndex": 7,
          "isInvisible": false
      },
      "Outlet-08": {
          "name": "Outlet-08",
          "outletIndex": 8,
          "isInvisible": false
      },
      "Outlet-09": {
          "name": "Outlet-09",
          "outletIndex": 9,
          "isInvisible": false
      },
      "Outlet-10": {
          "name": "Outlet-10",
          "outletIndex": 10,
          "isInvisible": false
      },
      "Outlet-11": {
          "name": "Outlet-11",
          "outletIndex": 11,
          "isInvisible": false
      },
      "Outlet-12": {
          "name": "Outlet-12",
          "outletIndex": 12,
          "isInvisible": false
      },
      "Outlet-13": {
          "name": "Outlet-13",
          "outletIndex": 13,
          "isInvisible": false
      },
      "Outlet-14": {
          "name": "Outlet-14",
          "outletIndex": 14,
          "isInvisible": false
      },
      "Outlet-15": {
          "name": "Outlet-15",
          "outletIndex": 15,
          "isInvisible": false
      },
      "Outlet-16": {
          "name": "Outlet-16",
          "outletIndex": 16,
          "isInvisible": false
      },
      "Outlet-17": {
          "name": "Outlet-17",
          "outletIndex": 17,
          "isInvisible": false
      },
      "Outlet-18": {
          "name": "Outlet-18",
          "outletIndex": 18,
          "isInvisible": false
      }
    }
  }
}
```
<!-- START Minimum Essentials Framework Versions -->
### Minimum Essentials Framework Versions

- 1.12.2
<!-- END Minimum Essentials Framework Versions -->
<!-- START Supported Types -->

<!-- END Supported Types -->
<!-- START Join Maps -->

<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- IDeviceInfoProvider
- ICommunicationMonitor
- IBridgeAdvanced
- IWattboxCommunications
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- Device
- ReconfigurableDevice
- PepperDash.Essentials.Core.Devices.IHasControlledPowerOutlets
- PepperDash.Essentials.Core.Devices.IHasPowerCycle
- StatusMonitorBase
- JoinMapBaseAdvanced
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void Initialize(STRING key, STRING method, STRING ipAddress, STRING userName, STRING password,
            INTEGER port)
- public void Initialize(string key, string method, TcpSshPropertiesConfig tcpProperties)
- public void PollEnable(INTEGER enable)
- public void SetOutlet(INTEGER index, INTEGER action)
- public void GetStatus()
- public void GetStatus()
- public void SetOutlet(int index, int action)
- public void SetOutlet(int index, EWattboxOutletSet action)
- public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
- public void OnDeviceInfoChanged()
- public void UpdateDeviceInfo()
- public bool CheckIp(string data)
- public void SetOutlet(int outletNumber, int outletStatus)
- public void Connect()
- public void GetStatus()
- public void ParseResponse(string data)
- public void SendLine(string data)
- public void SendLine(object data)
- public void SendBytes(byte[] bytes)
- public void SendText(string text)
- public void Disconnect()
- public void SetPowerStatus(bool status)
- public void SetName(string name)
- public void PowerCycle()
- public void PowerOff()
- public void PowerOn()
- public void PowerToggle()
- public void LinkOutlet(BasicTriList trilist, WattboxJoinmapDynamic joinMap)
- public void LinkOutlet(BasicTriList trilist, PduJoinMapBase joinMap, int offset)
- public void Connect()
- public void GetStatus()
- public void SetOutlet(int index, int action)
- public void SendBytes(byte[] bytes)
- public void SendText(string text)
- public void Disconnect()
- public void GetStatus()
- public void SetOutlet(int index, int action)
- public void Connect()
- public void SubmitRequest(string url, RequestType requestType)
- public void ParseResponse(string data)
- public void SendBytes(byte[] bytes)
- public void SendText(string text)
- public void Disconnect()
- public void SetOutlet(int outletNumber, int outletStatus)
- public void Connect()
- public void GetStatus()
- public void ParseResponse(string data)
- public void SendLine(string data)
- public void SendLine(object data)
- public void SendBytes(byte[] bytes)
- public void SendText(string text)
- public void Disconnect()
- public void GetStatus()
- public void SetOutlet(int index, int action)
- public void Connect()
- public void SubmitRequest(string url, RequestType requestType)
- public void ParseResponse(string data)
- public void SendBytes(byte[] bytes)
- public void SendText(string text)
- public void Disconnect()
<!-- END Public Methods -->
<!-- START Bool Feedbacks -->
### Bool Feedbacks

- IsOnlineFeedback
- IpChangeFeedback
- PowerIsOnFeedback
- EnabledFeedback
<!-- END Bool Feedbacks -->
<!-- START Int Feedbacks -->
### Int Feedbacks

- OutletCountFeedback
<!-- END Int Feedbacks -->
<!-- START String Feedbacks -->
### String Feedbacks

- NameFeedback
- NameFeedback
<!-- END String Feedbacks -->

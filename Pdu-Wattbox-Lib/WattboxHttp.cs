using System;
using System.Linq;
using System.Text;
using Crestron.SimplSharp.CrestronXmlLinq;
using Crestron.SimplSharp.Net.Http;
using PepperDash.Core;

namespace Wattbox.Lib
{
    public class WattboxHttp : IWattboxCommunications
    {
        private readonly string _authorization;
        private readonly HttpClient _client = new HttpClient();
        private readonly string _password;
        private readonly int _port;
        private readonly HttpClientRequest _request = new HttpClientRequest();
        private readonly string _username;
        private int _failtracker;

        public WattboxHttp(string key, string name, string authType, TcpSshPropertiesConfig tcpProperties)
        {
            Key = key;
            Name = name;

            BaseUrl = tcpProperties.Address;
            _port = tcpProperties.Port;
            _username = tcpProperties.Username;
            _password = tcpProperties.Password;
            _authorization = authType;
        }

        public string Name { get; set; }

        private string BaseUrl { get; set; }

        #region IKeyed Members

        public string Key { get; set; }

        #endregion

        #region IWattboxCommunications Members

        public OutletStatusUpdate UpdateOutletStatus { get; set; }
        public OutletNameUpdate UpdateOutletName { get; set; }
        public OnlineStatusUpdate UpdateOnlineStatus { get; set; }
        public LoggedInStatusUpdate UpdateLoggedInStatus { get; set; }
        public FirmwareVersionUpdate UpdateFirmwareVersion { get; set; }
        public SerialUpdate UpdateSerial { get; set; }
        public HostnameUpdate UpdateHostname { get; set; }
        public bool IsLoggedIn { get; set; }
        public bool IsOnlineWattbox { get; set; }


        public void GetStatus()
        {
            var newUrl = String.Format("http://{0}/wattbox_info.xml", BaseUrl);
            var newDir = String.Format("/wattbox_info.xml");

            Debug.Console(1, this, "Sending status request to {0}", newUrl);
            SubmitRequest(newUrl, newDir, RequestType.Get);
        }

        public void SetOutlet(int index, int action)
        {
            var newUrl = String.Format("http://{0}/control.cgi?outlet={1}&command={2}", BaseUrl, index, action);
            var newDir = String.Format("/control.cgi?outlet={0}&command={1}", index, action);
            //Debug.Console(2, Debug.ErrorLogLevel.Notice, "Url: {0}", newUrl);
            SubmitRequest(newUrl, newDir, RequestType.Get);
        }

        public void Connect()
        {
            //Debug.Console(2, this, "No connection necessary");
        }

        #endregion

        public void SubmitRequest(string url, string dir, RequestType requestType)
        {
            try
            {
                if (_failtracker >= 3)
                {
                    Debug.Console(0, this, Debug.ErrorLogLevel.Warning, "Authentication failure - please check auth and restart essentials");
                    return;
                }

                var plainText = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", _username, _password));

                var encodedAuth = Convert.ToBase64String(plainText);

                _client.KeepAlive = false;

                _client.Port = _port > 0 || _port < 65535 ? _port : 80;

                var request = new HttpClientRequest();

                if (!string.IsNullOrEmpty(_authorization))
                {
                    request.Header.SetHeaderValue("Authorization",
                        String.Format("{0} {1}", _authorization, encodedAuth));
                }
                request.Header.SetHeaderValue("User-Agent", "APP");
                request.KeepAlive = true;
                request.Header.SetHeaderValue("Keep-Alive", "300");

                request.Url.Parse(url);
                request.RequestType = requestType;

                //Debug.Console(2, this, "Sending request to {0}", request.Url);
                var response = _client.Dispatch(request);

                if (response == null)
                {
                    IsOnlineWattbox = false;
                    return;
                }
                
                var responseCode = response.Code;

                Debug.Console(1, "{0}:{1}", url, responseCode);

                //Any 2XX or 3XX response code is a valid HTTP response code that indicates no error
                IsOnlineWattbox = (responseCode >= 200 && responseCode < 400);

                var handler = TextReceived;
                if (handler != null)
                {
                    handler(this, new GenericCommMethodReceiveTextArgs());
                }

                if(string.IsNullOrEmpty(response.ContentString))
                {
                    IsOnlineWattbox = false;
                    Debug.Console(1, this, "Response ContentString is null or empty");
                    return;
                }
                
                if (response.Header.ContentType.Contains("text/xml"))
                {
                    //Debug.Console(2, this, "Parsing");
                    ParseResponse(response.ContentString);
                    return;
                }
                SetOfflineFail();
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Exception in HTTP Request : {0}", e.Message);
                Debug.Console(2, this, "Stack Trace: {0}", e.StackTrace);
                if (e.Message.ToLower().Contains("unauthorized") || e.Message.ToLower().Contains("401"))
                    IsOnlineWattbox = false;
            }
            finally
            {
                var handler = UpdateOnlineStatus;

                if (handler != null)
                {
                    //Debug.Console(0, this, "UpdateOnlineStatus Handler is Not Null and IsOnline =  {0}", IsOnlineWattbox);
                    handler(IsOnlineWattbox);
                }
            }
        }

        private void SetOfflineFail()
        {
            IsOnlineWattbox = false;
            _failtracker++;
        }

        public void ParseResponse(string data)
        {
            Debug.Console(2, this, "Response content: {0}", data);
            if (data.Contains("host_name"))
            {
                var xml = XElement.Parse(data);

                var hostnameString = xml.Element("host_name").Value;
                var hostnameHandler = UpdateHostname;
                if (hostnameHandler != null) hostnameHandler(hostnameString);

                var deviceModel = xml.Element("hardware_version").Value;
                var deviceModelHandler = UpdateFirmwareVersion;
                if (UpdateFirmwareVersion != null) deviceModelHandler(deviceModel);


                var serial = xml.Element("serial_number").Value;
                var serialHandler = UpdateSerial;
                if (UpdateSerial != null) serialHandler(serial);

                var result = xml.Element("outlet_status").Value;

                var outletStatus = result.Split(',').Select(s => s == "1").ToList();

                var handler = UpdateOutletStatus;

                if (handler != null) handler(outletStatus);

                var outletNamesResult = xml.Element("outlet_name").Value;

                var outletNames = outletNamesResult.Split(',').ToList();

                var namesHandler = UpdateOutletName;

                if (namesHandler != null) namesHandler(outletNames);
                return;
            }

            GetStatus();
        }

        #region IBasicCommunication Members

        public void SendBytes(byte[] bytes)
        {
            Debug.Console(0, this, "Unsupported - Added to adhere to interface");
        }

        public void SendText(string text)
        {
            Debug.Console(0, this, "Unsupported - Added to adhere to interface");
        }

        #endregion

        #region ICommunicationReceiver Members

        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;

        public void Disconnect()
        {
            Debug.Console(0, this, "Unsupported - Added to adhere to interface");
        }

        public bool IsConnected
        {
            get { return IsOnlineWattbox; }
        }

        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;



        #endregion
    }
}
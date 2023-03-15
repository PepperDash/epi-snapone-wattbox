using System;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
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
            //Props = JsonConvert.DeserializeObject<Properties>(dc.Properties.ToString());
            Key = key;
            Name = name;

            //Debug.Console(1, this, "Made it to constructor for Wattbox HTTP");

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

            //Debug.Console(2, this, "Sending status request to {0}", newUrl);
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
                }

                var plainText = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", _username, _password));

                var encodedAuth = Convert.ToBase64String(plainText);

                _client.KeepAlive = false;
                if (_port == 0)
                {
                    _client.Port = 80;
                }
                else if (_port >= 1 || _port <= 65535)
                {
                    _client.Port = _port;
                }

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

                Debug.Console(2, this, "Sending request to {0}", request.Url);
                var response = _client.Dispatch(request);

                if (response != null)
                {
                    var responseCode = response.Code;

                    //Debug.Console(2, "{0}:{1}", url, responseCode);

                    IsOnlineWattbox = (responseCode == 200 && responseCode != 401);


                    if (!String.IsNullOrEmpty(response.ContentString))
                    {
                        if (response.Header.ContainsHeaderValue("text/xml"))
                        {
                            ParseResponse(response.ContentString);
                        }
                        else SetOfflineFail();

                    }
                    else IsOnlineWattbox = false;

                }
                else
                {
                    IsOnlineWattbox = false;
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Exception in HTTP Request : {0}", e.Message);
                if (e.Message.ToLower().Contains("unauthorized") || e.Message.ToLower().Contains("401"))

                    IsOnlineWattbox = false;

                Debug.Console(2, this, "Stack Trace: {0}", e.StackTrace);
            }
            finally
            {

                //Debug.Console(0, this, "Reached finally and IsOnline =  {0}", IsOnlineWattbox);

                var handler = UpdateOnlineStatus;

                if (handler != null)
                {

                    //Debug.Console(0, this, "UpdateOnlineStatus Handler is Not Null and IsOnline =  {0}", IsOnlineWattbox);
                    handler(IsOnlineWattbox);
                }
                    /*
                else
                {
                    Debug.Console(0, this, "UpdateOnlineStatus Handler is Null and IsOnline =  {0}", IsOnlineWattbox);
                }
                     */
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


                var hostnameString = xml.Element("host_name").Value;
                var hostnameHandler = UpdateHostname;
                if (hostnameHandler != null) hostnameHandler(hostnameString);

                var deviceModel = xml.Element("hardware_version").Value;
                var deviceModelHandler = UpdateFirmwareVersion;
                if (UpdateFirmwareVersion != null) deviceModelHandler(deviceModel);


                var serial = xml.Element("serial_number").Value;
                var serialHandler = UpdateSerial;
                if (UpdateSerial != null) serialHandler(serial);

                //var result2 = result.Element("outlet_status").Value;


                var outletStatus = result.Split(',').Select(s => s == "1").ToList();

                var handler = UpdateOutletStatus;

                if (handler == null)
                {
                    return;
                }

                handler(outletStatus);
            }
            else
            {
                GetStatus();
            }
        }
    }
}
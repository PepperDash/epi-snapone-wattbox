using System;
using System.Linq;
using System.Text;
using Crestron.SimplSharp.CrestronXmlLinq;
using Crestron.SimplSharp.Net.Http;
using PepperDash.Core;

namespace Wattbox.Lib
{
    public class WattboxHttp : IWattboxCommunications, IKeyed
    {
        private readonly string _authType;
        private readonly string _authorization;
        private readonly HttpClient _client = new HttpClient();
        private readonly string _password;
        private readonly int _port;
        private readonly HttpClientRequest _request = new HttpClientRequest();
        private readonly string _username;

        public WattboxHttp(string key, string name, string authType, TcpSshPropertiesConfig tcpProperties)
        {
            //Props = JsonConvert.DeserializeObject<Properties>(dc.Properties.ToString());
            Key = key;
            Name = name;

            Debug.Console(1, this, "Made it to constructor for Wattbox HTTP");

            _authType = authType;
            BaseUrl = tcpProperties.Address;
            _port = tcpProperties.Port;
            _username = tcpProperties.Username;
            _password = tcpProperties.Password;
            _authorization = "Basic";
        }

        public string Name { get; set; }

        private string BaseUrl { get; set; }

        #region IKeyed Members

        public string Key { get; set; }

        #endregion

        #region IWattboxCommunications Members

        public OutletStatusUpdate UpdateOutletStatus { get; set; }
        public OnlineStatusUpdate UpdateOnlineStatus { get; set; }
        public bool IsOnline { get; set; }


        public void GetStatus()
        {
            var newUrl = String.Format("http://{0}/wattbox_info.xml", BaseUrl);
            var newDir = String.Format("/wattbox_info.xml");

            Debug.Console(2, this, "Sending status request to {0}", newUrl);
            SubmitRequest(newUrl, newDir, RequestType.Get);
        }

        public void SetOutlet(int index, int action)
        {
            var newUrl = String.Format("http://{0}/control.cgi?outlet={1}&command={2}", BaseUrl, index, action);
            var newDir = String.Format("/control.cgi?outlet={0}&command={1}", index, action);

            SubmitRequest(newUrl, newDir, RequestType.Get);
        }

        public void Connect()
        {
            Debug.Console(2, this, "No connection necessary");
        }

        #endregion

        public void SubmitRequest(string url, string dir, RequestType requestType)
        {
            try
            {
                if (!_authType.Equals("basic", StringComparison.InvariantCultureIgnoreCase))
                {
                    return;
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

                var response = _client.Dispatch(request);

                if (response != null)
                {
                    var responseCode = response.Code;

                    Debug.Console(2, "{0}:{1}", url, responseCode);

                    IsOnline = responseCode == 200;

                    var handler = UpdateOnlineStatus;

                    if (handler != null)
                    {
                        handler(IsOnline);
                    }
                    

                    if (!String.IsNullOrEmpty(response.ContentString))
                    {
                        ParseResponse(response.ContentString);
                    }
                }
                else
                {
                    IsOnline = false;
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Exception in HTTP Request : {0}", e.Message);
            }
        }

        public void ParseResponse(string data)
        {
            Debug.Console(2, this, "Response content: {0}", data);
            if (data.Contains("host_name"))
            {
                var xml = XElement.Parse(data);

                var result = xml.Element("outlet_status").Value;

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
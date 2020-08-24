using System;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.CrestronXmlLinq;

namespace Pdu_Wattbox_Epi
{
    public class WattboxHttp : WattboxBase
    {
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ContentType { get; set; }
        public string Authorization { get; set; }
        public int ResponseCode { get; set; }
        private readonly string _authType;

        public string BaseUrl { get; set; }

        private readonly HttpClientRequest _request = new HttpClientRequest();
        private HttpClientResponse _response;
        private readonly HttpClient _client = new HttpClient();


        public WattboxHttp(string key, string name, DeviceConfig dc)
            : base(key, name, dc)
        {
            //Props = JsonConvert.DeserializeObject<Properties>(dc.Properties.ToString());
            Debug.Console(1, this, "Made it to constructor for Wattbox");
            Debug.Console(2, this, "Wattbox Properties : {0}", dc.Properties.ToString());

            _authType = Props.AuthType;
            BaseUrl = Props.Control.TcpSshProperties.Address;
            Port = Props.Control.TcpSshProperties.Port;
            Username = Props.Control.TcpSshProperties.Username;
            Password = Props.Control.TcpSshProperties.Password;
            Authorization = "Basic";
        }

        public override bool CustomActivate()
        {
            PollTimer = new CTimer(o => GetStatus(), null, 5000, 45000);
            return true;
        }

        public void SubmitRequest(string url, string dir, RequestType requestType)
        {
            try
            {

                if (_authType.Equals("basic", StringComparison.InvariantCultureIgnoreCase))
                {
                    var plainText = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", Username, Password));

                    var encodedAuth = Convert.ToBase64String(plainText);

                    _client.KeepAlive = false;
                    if (Port == 0)
                        _client.Port = 80;
                    else if (Port >= 1 || Port <= 65535)
                        _client.Port = Port;

                    if (!string.IsNullOrEmpty(ContentType))
                        _request.Header.ContentType = ContentType;
                    if (!string.IsNullOrEmpty(Authorization))
                        _request.Header.SetHeaderValue("Authorization",
                            String.Format("{0} {1}", Authorization, encodedAuth));
                    _request.Header.SetHeaderValue("User-Agent", "APP");
                    _request.KeepAlive = true;
                    _request.Header.SetHeaderValue("Keep-Alive", "300");

                    _request.Url.Parse(url);
                    _request.RequestType = requestType;

                    _response = _client.Dispatch(_request);

                    if (_response != null)
                    {
                        ResponseCode = _response.Code;

                        if (ResponseCode > 0)
                        {
                            IsOnline = ResponseCode/100 < 3;
                        }
                        else
                            IsOnline = false;


                        IsOnlineFeedback.FireUpdate();
                        if (!String.IsNullOrEmpty(_response.ContentString))
                        {
                            ParseResponse(_response.ContentString);
                        }
                    }
                    else
                    {
                        IsOnline = false;
                        IsOnlineFeedback.FireUpdate();
                    }
                }
                else if (_authType.Equals("digest", StringComparison.CurrentCultureIgnoreCase))
                {
                    var digest = new DigestAuthFixer(String.Format("http://{0}", BaseUrl), Username,
                        Password, requestType, this);
                    var strResponse = digest.GrabResponse(dir);
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Exception in HTTP Request : {0}", e.Message);
            }
        }

        public override void ParseResponse(string data)
        {
            if (data.Contains("host_name"))
            {
                var xml = XElement.Parse(data);

                var result = xml.Element("outlet_status").Value;

                //var result2 = result.Element("outlet_status").Value;

                var outletStatus = result.Split(',');

                for (var i = 0; i < outletStatus.Count(); i++)
                {
                    IsPowerOn[i + 1] = outletStatus[i] == "1" ? true : false;
                    IsPowerOnFeedback[i + 1].FireUpdate();
                }
            }
            else
                GetStatus();
        }


        public override void GetStatus()
        {
            var newUrl = String.Format("http://{0}/wattbox_info.xml", BaseUrl);
            var newDir = String.Format("/wattbox_info.xml");

            SubmitRequest(newUrl, newDir, RequestType.Get);
        }

        public override void SetOutlet(int index, int action)
        {
            var newUrl = String.Format("http://{0}/control.cgi?outlet={1}&command={2}", BaseUrl, index, action);
            var newDir = String.Format("/control.cgi?outlet={0}&command={1}", index, action);

            SubmitRequest(newUrl, newDir, RequestType.Get);
        }
    }
}


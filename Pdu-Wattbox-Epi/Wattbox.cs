using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.DSP;
using System.Text.RegularExpressions;
using Crestron.SimplSharp.Reflection;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Bridges;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.Diagnostics;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp.CrestronXmlLinq;
using Pdu_Wattbox_Epi.Bridge;

namespace Pdu_Wattbox_Epi {
    public class Wattbox : ReconfigurableDevice, IBridge {

        DeviceConfig _Dc;

        public Properties props { get; set; }

        public BoolFeedback IsOnlineFeedback;
        //public List<BoolFeedback> IsPowerOn;

        public Dictionary<int, BoolFeedback> IsPowerOn;
        public Dictionary<int, bool> _IsPowerOn;
        //public List<bool> _IsPowerOn;

        private bool IsOnline { get; set; }

        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ContentType { get; set; }
        public string Authorization { get; set; }
        public int ResponseCode { get; set; }

        public string BaseUrl { get; set; }

        HttpClientRequest request = new HttpClientRequest();
        HttpClientResponse response;
        HttpClient client = new HttpClient();

        CTimer PollTimer;


        public static void LoadPlugin() {
            DeviceFactory.AddFactoryForType("wattBox", Wattbox.BuildDevice);
        }

        public static Wattbox BuildDevice(DeviceConfig dc) {
            var newDev = new Wattbox(dc.Key, dc.Name, dc);
            return newDev;
        }

        public Wattbox(string key, string name, DeviceConfig dc)
            : base(dc) {

            _Dc = dc;
            IsOnlineFeedback = new BoolFeedback(() => IsOnline);

            //_IsPowerOn = new List<bool>();
            _IsPowerOn = new Dictionary<int, bool>();
            IsPowerOn = new Dictionary<int, BoolFeedback>();
            //IsPowerOn = new List<BoolFeedback>();

            Name = name;

            



            

            props = JsonConvert.DeserializeObject<Properties>(_Dc.Properties.ToString());
            Debug.Console(1, this, "Made it to constructor for Wattbox");
            Debug.Console(2, this, "Wattbox Properties : {0}", _Dc.Properties.ToString());

            BaseUrl = props.Control.TcpSshProperties.Address;
            Port = props.Control.TcpSshProperties.Port;
            Username = props.Control.TcpSshProperties.Username;
            Password = props.Control.TcpSshProperties.Password;
            Authorization = "Basic";


            Init();
        }

        public void Init()
        {

            Debug.Console(2, this, "There are {0} outlets for {1}", props.Outlets.Count(), this.Name);
            foreach (var item in props.Outlets)
            {
                var i = item;

                Debug.Console(2, this, "The Outlet's name is {0} and it has an index of {1}", i.name, i.outletNumber);
                _IsPowerOn.Add(i.outletNumber, false);
                IsPowerOn.Add(i.outletNumber, new BoolFeedback(() => _IsPowerOn[i.outletNumber]));
            }
        }

        public override bool CustomActivate() {
            PollTimer = new CTimer(o => GetStatus(), null, 5000, 45000);
            return true;
        }

        public void SubmitRequest(string url, RequestType requestType) {
            try {

                var plainText = Encoding.UTF8.GetBytes(String.Format("{0}:{1}", Username, Password));

                var encodedAuth = Convert.ToBase64String(plainText);

                client.KeepAlive = false;
                if (Port == 0)
                    client.Port = 80;
                else if (Port >= 1 || Port <= 65535)
                    client.Port = Port;

                if (!string.IsNullOrEmpty(ContentType))
                    request.Header.ContentType = ContentType;
                if (!string.IsNullOrEmpty(Authorization))
                    request.Header.SetHeaderValue("Authorization", String.Format("{0} {1}", Authorization, encodedAuth));
                request.Header.SetHeaderValue("User-Agent", "APP");
                request.KeepAlive = true;
                request.Header.SetHeaderValue("Keep-Alive", "300");

                request.Url.Parse(url);
                request.RequestType = (RequestType)requestType;

                response = client.Dispatch(request);

                if (response != null)
                {
                    ResponseCode = response.Code;

                    if (ResponseCode > 0)
                    {
                        IsOnline = ResponseCode / 100 < 3 ? true : false;
                    }
                    else
                        IsOnline = false;


                    IsOnlineFeedback.FireUpdate();
                    if (!String.IsNullOrEmpty(response.ContentString.ToString()))
                    {
                        ParseResponse(response.ContentString.ToString());
                    }
                }
                else
                {
                    IsOnline = false;
                    IsOnlineFeedback.FireUpdate();
                }


            }
            catch (Exception e) {
                Debug.Console(2, this, "Exception in HTTP Request : {0}", e.Message);
            }
        }

        public void ParseResponse(string data) {
            if (data.Contains("host_name")) {
                var xml = XElement.Parse(data);

                var result = xml.Element("outlet_status").Value;

                //var result2 = result.Element("outlet_status").Value;

                var OutletStatus = result.Split(',');

                for (int i = 0; i < OutletStatus.Count(); i++) {
                    _IsPowerOn[i + 1] = OutletStatus[i] == "1" ? true : false;
                    IsPowerOn[i + 1].FireUpdate();
                }
            }
            else
                GetStatus();
        }

        public void GetStatus() {
            var newUrl = String.Format("http://{0}/wattbox_info.xml", BaseUrl);

            SubmitRequest(newUrl, RequestType.Get);
        }

        public void SetOutlet(int index, int action) {
            var newUrl = String.Format("http://{0}/control.cgi?outlet={1}&command={2}", BaseUrl, index, action);

            SubmitRequest(newUrl, RequestType.Get);
        }

        #region IBridge Members

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey) {
            this.LinkToApiExt(trilist, joinStart, joinMapKey);
        }

        #endregion

    }
}


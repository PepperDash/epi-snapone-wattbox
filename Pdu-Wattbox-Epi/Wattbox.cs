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
using PepperDash.Essentials.Core;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Bridges;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.Diagnostics;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp.CrestronXmlLinq;
using Pdu_Wattbox_Epi.Bridge;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Pdu_Wattbox_Epi {
    public class Wattbox : ReconfigurableDevice, IBridge {
        public Properties Props { get; set; }

        public BoolFeedback IsOnlineFeedback;
        //public List<BoolFeedback> IsPowerOnFeedback;

        public Dictionary<int, BoolFeedback> IsPowerOnFeedback;
        public Dictionary<int, bool> IsPowerOn;
        //public List<bool> _IsPowerOn;

        public Dictionary<int, StringFeedback> OutletNameFeedbacks;
        public Dictionary<int, string> OutletName;

        public Dictionary<int, BoolFeedback> OutletEnabledFeedbacks;
        public Dictionary<int, bool> OutletEnabled; 

        public FeedbackCollection<Feedback> Feedbacks; 

        private bool IsOnline { get; set; }

        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ContentType { get; set; }
        public string Authorization { get; set; }
        public int ResponseCode { get; set; }

        public string BaseUrl { get; set; }

        readonly HttpClientRequest _request = new HttpClientRequest();
        HttpClientResponse _response;
        readonly HttpClient _client = new HttpClient();

        CTimer _pollTimer;

        public static void LoadPlugin() {
            DeviceFactory.AddFactoryForType("wattBox", Wattbox.BuildDevice);
        }

        public static Wattbox BuildDevice(DeviceConfig dc) {
            var newDev = new Wattbox(dc.Key, dc.Name, dc);
            return newDev;
        }

        public Wattbox(string key, string name, DeviceConfig dc)
            : base(dc) {
            var dc1 = dc;
            if (dc1 == null) throw new ArgumentNullException("dc1");
            IsOnlineFeedback = new BoolFeedback(() => IsOnline);

            //_IsPowerOn = new List<bool>();
            IsPowerOn = new Dictionary<int, bool>();
            IsPowerOnFeedback = new Dictionary<int, BoolFeedback>();

            OutletNameFeedbacks = new Dictionary<int, StringFeedback>();
            OutletName = new Dictionary<int, string>();

            OutletEnabledFeedbacks = new Dictionary<int, BoolFeedback>();
            OutletEnabled = new Dictionary<int, bool>();    

            Feedbacks = new FeedbackCollection<Feedback>();
            //IsPowerOnFeedback = new List<BoolFeedback>();

            Name = name;

        
            

            Props = JsonConvert.DeserializeObject<Properties>(dc1.Properties.ToString());
            Debug.Console(1, this, "Made it to constructor for Wattbox");
            Debug.Console(2, this, "Wattbox Properties : {0}", dc1.Properties.ToString());

            BaseUrl = Props.Control.TcpSshProperties.Address;
            Port = Props.Control.TcpSshProperties.Port;
            Username = Props.Control.TcpSshProperties.Username;
            Password = Props.Control.TcpSshProperties.Password;
            Authorization = "Basic";


            Init();
        }

        public void Init()
        {

            Debug.Console(2, this, "There are {0} outlets for {1}", Props.Outlets.Count(), this.Name);
            foreach (var item in Props.Outlets)
            {
                var i = item;
                Debug.Console(2, this, "The Outlet's name is {0} and it has an index of {1}", i.name, i.outletNumber);
                OutletEnabled.Add(i.outletNumber, i.enabled);
                IsPowerOn.Add(i.outletNumber, false);
                OutletName.Add(i.outletNumber, i.name);
                var isPowerOnFeedback = new BoolFeedback(() => IsPowerOn[i.outletNumber]);
                var outletEnabledFeedback = new BoolFeedback(() => OutletEnabled[i.outletNumber]);
                var outletNameFeedback = new StringFeedback(() => OutletName[i.outletNumber]);


                IsPowerOnFeedback.Add(i.outletNumber, isPowerOnFeedback);
                OutletEnabledFeedbacks.Add(i.outletNumber, outletEnabledFeedback);
                OutletNameFeedbacks.Add(i.outletNumber, outletNameFeedback);
                Feedbacks.Add(isPowerOnFeedback);
                Feedbacks.Add(outletEnabledFeedback);
                Feedbacks.Add(outletNameFeedback);
                Feedbacks.Add(new StringFeedback("name", () => Name));
            }
        }

        public override bool CustomActivate() {
            _pollTimer = new CTimer(o => GetStatus(), null, 5000, 45000);
            return true;
        }

        public void SubmitRequest(string url, RequestType requestType) {
            try {

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
                    _request.Header.SetHeaderValue("Authorization", String.Format("{0} {1}", Authorization, encodedAuth));
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
                        IsOnline = ResponseCode / 100 < 3 ? true : false;
                    }
                    else
                        IsOnline = false;


                    IsOnlineFeedback.FireUpdate();
                    if (!String.IsNullOrEmpty(_response.ContentString.ToString()))
                    {
                        ParseResponse(_response.ContentString.ToString());
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

                var outletStatus = result.Split(',');

                for (var i = 0; i < outletStatus.Count(); i++) {
                    IsPowerOn[i + 1] = outletStatus[i] == "1" ? true : false;
                    IsPowerOnFeedback[i + 1].FireUpdate();
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


using System;
using System.Linq;
using System.Text;
using Crestron.SimplSharp.CrestronXmlLinq;
using Crestron.SimplSharp.Net.Http;
using PepperDash.Core;
using Serilog.Events;

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
        private bool _authFailed;

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

            Debug.LogMessage(LogEventLevel.Debug, this, "Sending status request to {0}", newUrl);
            SubmitRequest(newUrl, RequestType.Get);
        }

        public void SetOutlet(int index, int action)
        {
            var newUrl = String.Format("http://{0}/control.cgi?outlet={1}&command={2}", BaseUrl, index, action);
            //Debug.Console(2, Debug.ErrorLogLevel.Notice, "Url: {0}", newUrl);
            SubmitRequest(newUrl, RequestType.Get);
        }

        public void Connect()
        {
            //Debug.Console(2, this, "No connection necessary");
        }

        #endregion

        public void SubmitRequest(string url, RequestType requestType)
        {
            try
            {
                _client.KeepAlive = false;
                _client.Port = _port > 0 && _port < 65535 ? _port : 80;

                // Uses the configured scheme (Basic by default).
                string error;
                var response = TryDispatch(url, requestType, out error);
                LogAuthDiagnostics("initial", response, error);

                // A 401 is a genuine auth failure. A null response without a 401 is an
                // offline/transport failure - do NOT call it an auth error.
                if (response == null)
                {
                    if (!String.IsNullOrEmpty(error) && error.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0)
                        ReportAuthFailure();
                    else
                        ReportOffline(error);
                    return;
                }

                var responseCode = response.Code;

                Debug.LogMessage(LogEventLevel.Debug, "{0}:{1}", url, responseCode);

                if (responseCode == 401)
                {
                    ReportAuthFailure();
                    return;
                }

                //Any 2XX or 3XX response code is a valid HTTP response code that indicates no error
                IsOnlineWattbox = (responseCode >= 200 && responseCode < 400);
                if (IsOnlineWattbox)
                {
                    // Recovered - clear the auth-failure latch so the warning can fire again later.
                    _authFailed = false;
                }

                var handler = TextReceived;
                if (handler != null)
                {
                    handler(this, new GenericCommMethodReceiveTextArgs());
                }

                if(string.IsNullOrEmpty(response.ContentString))
                {
                    return;
                }

                if (response.Header.ContentType.Contains("text/xml"))
                {
                    //Debug.Console(2, this, "Parsing");
                    IsOnlineWattbox = true;
                    ParseResponse(response.ContentString);
                    return;
                }

                if (!IsOnlineWattbox)
                    ReportOffline(null);
            }
            catch (Exception e)
            {
                Debug.LogMessage(LogEventLevel.Error, this, "Exception in HTTP Request : {0}", e.Message);
                Debug.LogMessage(LogEventLevel.Verbose, this, "Stack Trace: {0}", e.StackTrace);
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

        // Builds and dispatches a single HTTP request. Returns the response, or null (with the
        // exception message in 'error') if the dispatch throws - e.g. Crestron's HttpClient throws
        // on a 401, with the response status line + headers in the message.
        private HttpClientResponse TryDispatch(string url, RequestType requestType, out string error)
        {
            error = null;
            try
            {
                var request = new HttpClientRequest();

                if (!String.IsNullOrEmpty(_authorization))
                {
                    var encodedAuth = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(String.Format("{0}:{1}", _username, _password)));
                    request.Header.SetHeaderValue("Authorization",
                        String.Format("{0} {1}", _authorization, encodedAuth));
                }

                request.Header.SetHeaderValue("User-Agent", "APP");
                request.KeepAlive = true;
                request.Header.SetHeaderValue("Keep-Alive", "300");

                request.Url.Parse(url);
                request.RequestType = requestType;

                return _client.Dispatch(request);
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        private static string GetAuthenticateHeader(HttpClientResponse response)
        {
            if (response == null || response.Header == null) return null;
            try
            {
                return response.Header.GetHeaderValue("WWW-Authenticate");
            }
            catch
            {
                return null;
            }
        }

        // A real authentication failure: the device returned a 401. Latched so the warning logs
        // once per failure, not every poll.
        private void ReportAuthFailure()
        {
            IsOnlineWattbox = false;
            if (_authFailed)
                return;

            _authFailed = true;
            Debug.LogMessage(LogEventLevel.Warning, this,
                "Authentication failure (HTTP 401) - check username/password (this device uses HTTP Basic auth)");
        }

        // A transport/offline failure (timeout, refused, DNS, etc.) - NOT an auth problem. Keeps
        // polling so the device self-heals; logged quietly to avoid spam at the poll rate.
        private void ReportOffline(string error)
        {
            IsOnlineWattbox = false;
            if (!String.IsNullOrEmpty(error))
                Debug.LogMessage(LogEventLevel.Verbose, this, "HTTP request failed (offline): {0}", error);
        }

        // Surfaces exactly what each attempt returned so auth issues are diagnosable on hardware:
        // the response code + WWW-Authenticate header when a response came back, or the raw
        // exception message when Crestron's HttpClient threw (e.g. on a 401).
        private void LogAuthDiagnostics(string phase, HttpClientResponse response, string error)
        {
            if (response != null)
            {
                var www = GetAuthenticateHeader(response);
                Debug.LogMessage(LogEventLevel.Debug, this, "[{0}] code={1} WWW-Authenticate={2}",
                    phase, response.Code, String.IsNullOrEmpty(www) ? "(none)" : www);
            }
            else
            {
                Debug.LogMessage(LogEventLevel.Debug, this, "[{0}] no response object; error={1}",
                    phase, String.IsNullOrEmpty(error) ? "(none)" : error);
            }
        }

        public void ParseResponse(string data)
        {
            Debug.LogMessage(LogEventLevel.Verbose, this, "Response content: {0}", data);
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

                var outletNames = ParseOutletNamesByCommaSpace(outletNamesResult, outletStatus.Count);

                var namesHandler = UpdateOutletName;

                if (namesHandler != null) namesHandler(outletNames);
                return;
            }

            GetStatus();
        }

        private static System.Collections.Generic.List<string> ParseOutletNamesByCommaSpace(string outletNamesRaw, int expectedCount)
        {
            var names = outletNamesRaw.Split(',').ToList();

            if (expectedCount <= 0 || names.Count <= expectedCount)
                return names.Select(s => s.Trim()).ToList();

            // Merge only fragments that look like a continuation after ", ".
            for (var i = 1; i < names.Count && names.Count > expectedCount; i++)
            {
                if (!names[i].StartsWith(" "))
                    continue;

                names[i - 1] = string.Format("{0},{1}", names[i - 1], names[i]);
                names.RemoveAt(i);
                i--;
            }

            return names.Select(s => s.Trim()).ToList();
        }

        #region IBasicCommunication Members

        public void SendBytes(byte[] bytes)
        {
            Debug.LogMessage(LogEventLevel.Warning, this, "Unsupported - Added to adhere to interface");
        }

        public void SendText(string text)
        {
            Debug.LogMessage(LogEventLevel.Warning, this, "Unsupported - Added to adhere to interface");
        }

        #endregion

        #region ICommunicationReceiver Members

        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;

        public void Disconnect()
        {
            Debug.LogMessage(LogEventLevel.Warning, this, "Unsupported - Added to adhere to interface");
        }

        public bool IsConnected
        {
            get { return IsOnlineWattbox; }
        }

        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;



        #endregion
    }
}
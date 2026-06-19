using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

                _client.KeepAlive = false;
                _client.Port = _port > 0 && _port < 65535 ? _port : 80;

                // First attempt - uses the configured scheme (Basic by default).
                string error;
                var response = TryDispatch(url, requestType, null, out error);

                // WB-800-IPVM / OvrC firmware requires HTTP Digest auth: it rejects Basic with a
                // 401 + 'WWW-Authenticate: Digest ...' challenge. Crestron's HttpClient surfaces that
                // 401 as an exception whose message carries the response headers, so the challenge can
                // arrive via 'error' (thrown) or via a returned 401 response. Retry once with Digest.
                var challenge = ExtractDigestChallenge(response, error);
                if (challenge != null)
                {
                    var digestHeader = BuildDigestHeader(challenge, MethodString(requestType), dir);
                    if (!String.IsNullOrEmpty(digestHeader))
                    {
                        Debug.Console(1, this, "Retrying request with HTTP Digest auth");
                        response = TryDispatch(url, requestType, digestHeader, out error);
                    }
                }

                if (response == null)
                {
                    if (!String.IsNullOrEmpty(error))
                        Debug.Console(2, this, "HTTP Request failed: {0}", error);
                    SetOfflineFail();
                    return;
                }

                var responseCode = response.Code;

                Debug.Console(1, "{0}:{1}", url, responseCode);

                //Any 2XX or 3XX response code is a valid HTTP response code that indicates no error
                IsOnlineWattbox = (responseCode >= 200 && responseCode < 400);
                if (IsOnlineWattbox)
                    _failtracker = 0;

                var handler = TextReceived;
                if (handler != null)
                {
                    handler(this, new GenericCommMethodReceiveTextArgs());
                }

                if (string.IsNullOrEmpty(response.ContentString))
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

        // Builds and dispatches a single HTTP request. Returns the response, or null (with the
        // exception message in 'error') if the dispatch throws - e.g. Crestron's HttpClient throws
        // on a 401, with the response status line + headers in the message.
        private HttpClientResponse TryDispatch(string url, RequestType requestType, string authorizationHeader, out string error)
        {
            error = null;
            try
            {
                var request = new HttpClientRequest();

                if (!String.IsNullOrEmpty(authorizationHeader))
                {
                    // Pre-computed scheme + credentials (e.g. a Digest 'response' header).
                    request.Header.SetHeaderValue("Authorization", authorizationHeader);
                }
                else if (!String.IsNullOrEmpty(_authorization))
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

        // Returns the 'Digest ...' challenge string from either a returned 401 response or the
        // exception message of a thrown 401, or null when Digest is not being requested.
        private static string ExtractDigestChallenge(HttpClientResponse response, string error)
        {
            if (response != null && response.Code == 401)
            {
                var hdr = GetAuthenticateHeader(response);
                if (!String.IsNullOrEmpty(hdr) && hdr.IndexOf("Digest", StringComparison.OrdinalIgnoreCase) >= 0)
                    return hdr;
            }

            if (!String.IsNullOrEmpty(error) &&
                error.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 &&
                error.IndexOf("Digest", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Isolate the WWW-Authenticate line for cleaner parsing.
                var match = Regex.Match(error, "WWW-Authenticate:\\s*(.+)", RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value : error;
            }

            return null;
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

        // Builds an RFC 2617 'Authorization: Digest ...' header value from a server challenge.
        // Supports algorithm=MD5 and qop=auth (what OvrC / WattBox firmware uses).
        private string BuildDigestHeader(string challenge, string method, string uri)
        {
            var realm = ExtractDirective(challenge, "realm");
            var nonce = ExtractDirective(challenge, "nonce");
            var qop = ExtractDirective(challenge, "qop");
            var opaque = ExtractDirective(challenge, "opaque");
            var algorithm = ExtractDirective(challenge, "algorithm");

            if (String.IsNullOrEmpty(realm) || String.IsNullOrEmpty(nonce))
            {
                Debug.Console(1, this, "Digest challenge missing realm/nonce - cannot authenticate");
                return null;
            }

            var ha1 = Md5Hex(String.Format("{0}:{1}:{2}", _username, realm, _password));
            var ha2 = Md5Hex(String.Format("{0}:{1}", method, uri));

            const string nc = "00000001";
            var cnonce = Md5Hex(Guid.NewGuid().ToString()).Substring(0, 16);

            string response;
            if (!String.IsNullOrEmpty(qop))
                response = Md5Hex(String.Format("{0}:{1}:{2}:{3}:{4}:{5}", ha1, nonce, nc, cnonce, qop, ha2));
            else
                response = Md5Hex(String.Format("{0}:{1}:{2}", ha1, nonce, ha2));

            var sb = new StringBuilder();
            sb.AppendFormat("Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", response=\"{4}\"",
                _username, realm, nonce, uri, response);
            if (!String.IsNullOrEmpty(algorithm))
                sb.AppendFormat(", algorithm={0}", algorithm);
            if (!String.IsNullOrEmpty(qop))
                sb.AppendFormat(", qop={0}, nc={1}, cnonce=\"{2}\"", qop, nc, cnonce);
            if (!String.IsNullOrEmpty(opaque))
                sb.AppendFormat(", opaque=\"{0}\"", opaque);

            return sb.ToString();
        }

        // Pulls a directive value ('key="value"' or 'key=value') out of a Digest challenge.
        private static string ExtractDirective(string source, string key)
        {
            if (String.IsNullOrEmpty(source)) return null;
            var match = Regex.Match(source, key + "\\s*=\\s*\"?([^\",]+)\"?", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string MethodString(RequestType requestType)
        {
            return requestType.ToString().ToUpper();
        }

        private static string Md5Hex(string input)
        {
            using (var md5 = new Crestron.SimplSharp.Cryptography.MD5CryptoServiceProvider())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
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
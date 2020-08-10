using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Cryptography;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Net.Http;

namespace Pdu_Wattbox_Epi
{

    public class DigestAuthFixer
    {
        private static string _host;
        private static string _user;
        private static string _password;
        private static string _realm;
        private static string _nonce;
        private static string _qop;
        private static string _cnonce;
        private static DateTime _cnonceDate;
        private static int _nc;
        private static RequestType _requestType;
        private Wattbox _parent;

        readonly HttpClientRequest _request = new HttpClientRequest();
        HttpClientResponse _response;
        readonly HttpClient _client = new HttpClient();

        public DigestAuthFixer(string host, string user, string password, RequestType requestType, Wattbox parent)
        {
            // TODO: Complete member initialization
            _host = host;
            _user = user;
            _password = password;
            _requestType = requestType;
            _parent = parent;
        }

        private string CalculateMd5Hash(
            string input)
        {
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var hash = MD5.Create().ComputeHash(inputBytes);
            var sb = new StringBuilder();
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private string GrabHeaderVar(
            string varName,
            string header)
        {
            var regHeader = new Regex(string.Format(@"{0}=""([^""]*)""", varName));
            var matchHeader = regHeader.Match(header);
            if (matchHeader.Success)
                return matchHeader.Groups[1].Value;
            throw new ApplicationException(string.Format("Header {0} not found", varName));
        }

        private string GetDigestHeader(
            string dir)
        {
            _nc = _nc + 1;

            var ha1 = CalculateMd5Hash(string.Format("{0}:{1}:{2}", _user, _realm, _password));
            var ha2 = CalculateMd5Hash(string.Format("{0}:{1}", "GET", dir));
            var digestResponse =
                CalculateMd5Hash(string.Format("{0}:{1}:{2:00000000}:{3}:{4}:{5}", ha1, _nonce, _nc, _cnonce, _qop, ha2));

            return string.Format("Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", " +
                                 "algorithm=MD5, response=\"{4}\", qop={5}, nc={6:00000000}, cnonce=\"{7}\"",
                _user, _realm, _nonce, dir, digestResponse, _qop, _nc, _cnonce);
        }

        public string GrabResponse(
            string dir)
        {
            var url = _host + dir;
            var uri = new Uri(url);

            var myRequest = new HttpClientRequest();

            myRequest.Header.SetHeaderValue("User-Agent", "APP");
            myRequest.KeepAlive = true;
            myRequest.Header.SetHeaderValue("Keep-Alive", "300");

            myRequest.Url.Parse(url);
            myRequest.RequestType = _requestType;




            // If we've got a recent Auth header, re-use it!
            if (!string.IsNullOrEmpty(_cnonce) &&
                DateTime.Now.Subtract(_cnonceDate).TotalHours < 1.0)
            {
                myRequest.Header.SetHeaderValue("Authorization", GetDigestHeader(dir));
            }


            try
            {
                var myResponse = _client.Dispatch(myRequest);

                if (myResponse != null)
                {
                    _parent.ResponseCode = _response.Code;

                    if (_parent.ResponseCode > 0)
                    {
                        if (_parent.ResponseCode/100 < 3)
                            _parent.IsOnline = true;
                        if (_parent.ResponseCode/100 == 4)
                        {

                            var wwwAuthenticateHeader = myResponse.Header["WWW-Authenticate"];
                            _realm = GrabHeaderVar("realm", wwwAuthenticateHeader.Value);
                            _nonce = GrabHeaderVar("nonce", wwwAuthenticateHeader.Value);
                            _qop = GrabHeaderVar("qop", wwwAuthenticateHeader.Value);

                            _nc = 0;
                            _cnonce = new Random().Next(123400, 9999999).ToString();
                            _cnonceDate = DateTime.Now;

                            var myRequest2 = new HttpClientRequest();
                            HttpClientResponse myResponse2;

                            myRequest2.Header.SetHeaderValue("User-Agent", "APP");
                            myRequest2.KeepAlive = true;
                            myRequest2.Header.SetHeaderValue("Keep-Alive", "300");

                            myRequest2.Url.Parse(url);
                            myRequest2.RequestType = _requestType;

                            myRequest2.Header.SetHeaderValue("Authorization", GetDigestHeader(dir));


                            myResponse = _client.Dispatch(myRequest2);
                            return myResponse.ContentString;

                        }
                        
                    }
                    else
                        _parent.IsOnline = false;


                    _parent.IsOnlineFeedback.FireUpdate();
                    return myResponse.ContentString;
                }
                _parent.IsOnline = false;
                _parent.IsOnlineFeedback.FireUpdate();
                return null;
            }
            catch ( Exception ex)
            {
                return null;
            }
        }
    }
}
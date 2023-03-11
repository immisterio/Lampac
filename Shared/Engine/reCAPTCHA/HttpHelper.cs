using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace Shared.Engine.reCAPTCHA
{
    public class HttpHelper
    {
        public static dynamic Post(Uri url, string post, out string error)
        {
            error = null;
            dynamic result = null;
            var postBody = Encoding.UTF8.GetBytes(post);
            var request = (HttpWebRequest)WebRequest.Create(url);

            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = postBody.Length;
            request.Timeout = 30000;

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(postBody, 0, postBody.Length);
                    stream.Close();
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var strreader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                    result = JsonConvert.DeserializeObject(strreader.ReadToEnd());

                    response.Close();
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;

                return false;
            }

            return result;
        }
    }
}

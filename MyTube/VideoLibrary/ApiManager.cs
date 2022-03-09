using MyTube.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace MyTube.VideoLibrary
{
    public class ApiManager
    {
        private static async Task<string> TryPostJsonAsync(string url, string auth, Object content)
        {
            string httpResponseBody;
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", auth);

                Uri uri = new Uri(url);

                HttpStringContent httpContent = new HttpStringContent(JsonConvert.SerializeObject(content));

                HttpResponseMessage httpResponseMessage = httpClient.PostAsync(uri, httpContent).AsTask().GetAwaiter().GetResult();

                httpResponseMessage.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponseMessage.Content.ReadAsStringAsync();
                return httpResponseBody;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private class PostObject
        {
            public string instanceCode;
            public DatabaseCore data;

            public PostObject(string instanceCode, DatabaseCore data)
            {
                this.instanceCode = instanceCode;
                this.data = data;
            }
        }

        private class ReturnObject
        {
            public string instanceCode;
        }

        public static string UpdateDatabase(string password, string instanceCode, DatabaseCore data)
        {
            try
            {
                return JsonConvert.DeserializeObject<ReturnObject>(TryPostJsonAsync("https://rntjc8dcvh.execute-api.us-west-1.amazonaws.com/prod", password, new PostObject(instanceCode, data)).GetAwaiter().GetResult()).instanceCode;
            }
            catch { throw new AccessViolationException(); }
        }

        private static string SendGetRequest(string url, string password, string instanceCode)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", password);

            var headers = httpClient.DefaultRequestHeaders;

            string header = "ie";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            header = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            Uri requestUri = new Uri(url + "?instanceCode=" + instanceCode);

            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                httpResponse = httpClient.GetAsync(requestUri).AsTask().GetAwaiter().GetResult();
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = httpResponse.Content.ReadAsStringAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }

            return httpResponseBody;
        }

        private class GetObject
        {
            public DatabaseCore body;
        }

        public static DatabaseCore GetGloabalDatabase(string password, string instanceCode)
        {
            try
            {
                return JsonConvert.DeserializeObject<GetObject>(SendGetRequest("https://rntjc8dcvh.execute-api.us-west-1.amazonaws.com/prod", password, instanceCode)).body;
            }
            catch (JsonReaderException) { return null; }
        }

    }
}

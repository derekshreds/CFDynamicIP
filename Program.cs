using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace CFDynamicIP
{
    public class API
    {
        public HttpClient client;
        public Auth auth;

        public class Auth
        {
            public string api_key;
            public string email;
        }

        public class UpdateContent
        {
            public string type;
            public string name;
            public string content;
            public int ttl;
            public bool proxied;
        }

        public async Task<dynamic> SendRequest(HttpMethod method, string url, HttpContent content = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("X-Auth-Key", auth.api_key);
            request.Headers.Add("X-Auth-Email", auth.email);

            if (content != null)
            {
                request.Content = content;
            }

            var response = await client.SendAsync(request).ConfigureAwait(false);
            var returned = await response.Content.ReadAsStringAsync();
            
            dynamic result = JsonConvert.DeserializeObject(returned);
            return result.result;
        }
    }

    public class Settings
    {
        public string api_key = "";
        public string zone_id = "";
        public string email = "";
        public string record_name = "";
        public string path = Application.StartupPath + "/";

        public Settings()
        {
            try
            {
                if (File.Exists(path + "settings.ini"))
                {
                    var data = File.ReadAllLines(path + "settings.ini");
                    char[] split_char = { '=' };

                    for (int i = 0; i < data.Length; i++)
                    {
                        if (data[i].Contains("api_key"))
                        {
                            api_key = data[i].Split(split_char)[1].Trim();
                        }

                        if (data[i].Contains("zone_id"))
                        {
                            zone_id = data[i].Split(split_char)[1].Trim();
                        }

                        if (data[i].Contains("email"))
                        {
                            email = data[i].Split(split_char)[1].Trim();
                        }

                        if (data[i].Contains("record_name"))
                        {
                            record_name = data[i].Split(split_char)[1].Trim();
                        }
                    }
                }
                else
                {
                    string[] default_config =
                    {
                        "api_key = ",
                        "zone_id = ",
                        "email = ",
                        "record_name = "
                    };
                    File.WriteAllLines(path + "settings.ini", default_config);
                }
            }
            catch { }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            HttpClient client = new HttpClient();
            Settings settings = new Settings();

            if (settings.api_key == "")
            {
                Environment.Exit(0);
            }

            string[] urls =
            {
                "https://api.ipify.org",
                "https://ipv4bot.whatismyipaddress.com"
            };

            string api_url = "https://api.cloudflare.com/client/v4/zones/" + settings.zone_id + "/dns_records"; ;

            API api = new API()
            {
                client = client,
                auth = new API.Auth()
                {
                    api_key = settings.api_key,
                    email = settings.email
                }
            };

            while (true)
            {
                try
                {
                    // Note:
                    // The Cloudflare API sets a maximum of 1,200 requests in a five minute period.
                    var ip = client.GetStringAsync(urls[0]).Result;
                    var database = api.SendRequest(HttpMethod.Get, api_url + "?name=" + settings.record_name).Result;
                    var cf_id = database[0].id.ToString();
                    var cf_ip = database[0].content.ToString();

                    if (ip != cf_ip)
                    {
                        var update_data = new API.UpdateContent()
                        {
                            type = "A",
                            name = settings.record_name,
                            content = ip,
                            ttl = 1,
                            proxied = true
                        };
                        var content = new StringContent(JsonConvert.SerializeObject(update_data), Encoding.UTF8, "application/json");
                        var update = api.SendRequest(HttpMethod.Put, api_url + "/" + cf_id, content).Result;
                    }

                    Thread.Sleep(60 * 15 * 1000);
                }
                catch { }
            }
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TwoCaptcha.Captcha;
using Easy_Captcha.Enum;
using System.Net.Http;
using System.Text;
using System.Net;

namespace Easy_Captcha
{
    public class Factory
    {
        /// <summary>
        /// Service used to request captchas
        /// </summary>
        public CaptchaProvider CaptchaProvider { get; private set; }

        /// <summary>
        /// Generated token list
        /// </summary>
        public List<string> Tokens { get; private set; }

        /// <summary>
        /// Provider's client key
        /// </summary>
        public string ClientKey { get; private set; }

        /// <summary>
        /// HCaptcha's site key from the website you are generating tokens
        /// </summary>
        public string SiteKey { get; private set; }

        /// <summary>
        /// Website Url used to generate the captchas
        /// </summary>
        public string SiteUrl { get; private set; }

        /// <summary>
        /// Whether the factory is stopping or not
        /// </summary>
        public bool Stopping { get; private set; }

        /// <summary>
        /// Factory status
        /// </summary>
        public Status Status { get; private set; }

        /// <summary>
        /// Delay between the token requests
        /// </summary>
        public int Delay { get; private set; }

        public Factory(CaptchaProvider captchaProvider, string clientKey, string siteKey, string siteUrl, int delay)
        {
            CaptchaProvider = captchaProvider;
            Status = Status.Disabled;
            ClientKey = clientKey;
            SiteKey = siteKey;
            SiteUrl = siteUrl;
            Tokens = new();
            Delay = delay;
        }

        /// <summary>
        /// Start the token factory, generating captchas and adding them to Tokens list
        /// </summary>
        public async Task Start()
        {
            if (Status == Status.Enabled || Stopping) return;
            Status = Status.Enabled;

            while (Status == Status.Enabled)
            {
                try
                {
                    _ = Task.Run(GenerateToken);
                    await Task.Delay(Delay);
                }

                catch
                {

                }
            }

            Stopping = false;
        }

        /// <summary>
        /// Stop the token factory
        /// </summary>
        public void Stop()
        {
            if (Status == Status.Disabled) return;

            Stopping = true;
            Status = Status.Disabled;
        }

        /// <summary>
        /// Get a token from list and remove it from token list
        /// </summary>
        /// <param name="getMethod">List retrieve method</param>
        /// <param name="index">Only necessary if getMethod is Index</param>
        /// <returns>Returns a captcha token if there is one available matching the selected index</returns>
        public string RetrieveToken(GetMethod getMethod = GetMethod.First, int index = 0)
        {
            var result = "";

            if (Tokens.Count == 0) return result;
            else if (getMethod == GetMethod.First) index = 0;
            else if (getMethod == GetMethod.Last) index = Tokens.Count - 1;
            else if (getMethod == GetMethod.Index && Tokens.Count < index) return result;

            result = Tokens[index];
            Tokens.RemoveAt(index);

            return result;
        }

        /// <summary>
        /// Create a new HCaptcha token and add it to this Factory's Tokens list
        /// </summary>
        public async Task GenerateToken()
        {
            var captchaToken = await GetToken();
            if (captchaToken == "") return;
            Tokens.Add(captchaToken);
        }

        /// <summary>
        /// Create a new HCaptcha token, using this Factory's provider
        /// </summary>
        /// <returns>If anything goes wrong, it may return an empty string</returns>
        public async Task<string> GetToken()
        {
            if (CaptchaProvider == CaptchaProvider.TwoCaptcha)
            {
                var solver = new TwoCaptcha.TwoCaptcha(ClientKey);
                var leftTries = 3;

                while (leftTries > 0)
                {
                    leftTries--;
                    var captcha = new HCaptcha();
                    captcha.SetSiteKey(SiteKey);
                    captcha.SetUrl(SiteUrl);

                    try
                    {
                        await solver.Solve(captcha);
                        return captcha.Code;
                    }
                    catch { await Task.Delay(500); }
                }

                return "";
            }

            else if (CaptchaProvider == CaptchaProvider.XEvil)
            {
                var apiUrl = CaptchaProvider.XEvil.ApiUrl();
                using var httpClient = new HttpClient();
                using var webClient = new WebClient();

                webClient.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                var createTaskResult = webClient.UploadString($"{apiUrl}in.php", $"key={ClientKey}&method=hcaptcha&sitekey={SiteKey}&pageurl={SiteUrl}");

                if (!createTaskResult.Contains('|')) return "";
                var taskId = int.Parse(createTaskResult.Split('|')[1]);

                while (true)
                {
                    var response = await httpClient.PostAsync($"{apiUrl}res.php", new StringContent($"?key={ClientKey}&action=get&id={taskId}&json=1", Encoding.UTF8, "application/x-www-form-urlencoded"));
                    var requestNode = JObject.Parse(await response.Content.ReadAsStringAsync())["request"];

                    if (requestNode != null)
                    {
                        var captchaResponse = requestNode.ToString();

                        if (captchaResponse != "CAPCHA_NOT_READY")
                            return captchaResponse;
                    }

                    await Task.Delay(2500);
                }
            }

            else if (CaptchaProvider == CaptchaProvider.CapMonster)
            {
                var apiUrl = CaptchaProvider.CapMonster.ApiUrl();
                using var httpClient = new HttpClient();

                var createTaskResult = await httpClient.PostAsync($"{apiUrl}createTask", new StringContent(
                    $"{{\"clientKey\": \"{ClientKey}\", \"task\": {{ \"type\": \"HCaptchaTaskProxyless\", \"websiteURL\": \"{SiteUrl}\", \"websiteKey\": \"{SiteKey}\" }}}}", Encoding.UTF8, "application/json"));

                var taskJson = JObject.Parse(await createTaskResult.Content.ReadAsStringAsync());
                var errorIdToken = taskJson["errorId"];
                var taskIdToken = taskJson["taskId"];

                if (errorIdToken != null)
                {
                    var errorId = (int)errorIdToken;
                    if (errorId != 0) return "";
                }

                if (taskIdToken == null) return "";

                var taskId = (int)taskIdToken;

                while (true)
                {
                    var response = await httpClient.PostAsync($"{apiUrl}getTaskResult", new StringContent($"{{\"clientKey\": \"{ClientKey}\", \"taskId\": {taskId}}}", Encoding.UTF8, "application/json"));
                    var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());

                    errorIdToken = responseJson["errorId"];
                    var statusToken = responseJson["status"];

                    if (errorIdToken != null)
                    {
                        var errorId = (int)errorIdToken;
                        if (errorId != 0) return "";
                    }

                    if (statusToken != null)
                    {
                        var status = statusToken.ToString();
                        var solutionToken = responseJson["solution"];

                        if (status != "processing" && solutionToken != null)
                        {
                            var captchaToken = solutionToken["gRecaptchaResponse"];

                            if (captchaToken != null)
                                return captchaToken.ToString();
                        }
                    }

                    await Task.Delay(2500);
                }
            }

            else return "";
        }
    }
}
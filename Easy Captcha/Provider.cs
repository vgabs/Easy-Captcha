using Easy_Captcha.Enum;

namespace Easy_Captcha
{
    public static class Provider
    {
        public static string ApiUrl(this CaptchaProvider captchaProvider)
        {
            return captchaProvider switch
            {
                CaptchaProvider.CapMonster => "https://api.capmonster.cloud/",
                CaptchaProvider.XEvil => "http://127.0.0.1/",
                _ => ""
            };
        }
    }
}

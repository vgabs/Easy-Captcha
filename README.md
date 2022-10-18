# Easy-Captcha

A .NET 5.0 library created to generate **HCaptcha** tokens from some providers easily.
For now only XEvil, TwoCaptcha and CapMonster are supported.

## Sample
Auto generating tokens

    var captchaFactory = new Factory(Easy_Captcha.Enum.CaptchaProvider.TwoCaptcha, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", "https://www.google.com/", 10000);
    
    //Start generating captchas
    _ = Task.Run(captchaFactory.Start);
    
    //Do your stuff
    while (this.Enabled) 
    {
        var token = captchaFactory.RetrieveToken(Easy_Captcha.Enum.GetMethod.First);
    
        if (!string.IsNullOrEmpty(token))
	        await MethodThatUsesCaptcha(token);

        await Task.Delay(2500;
    }
    
    //Stop generating captchas
    captchaFactory.Stop();    
Generating tokens by demand

    var token = await captchaFactory.GetToken();
    await MethodThatUsesCaptcha(token);

using Microsoft.Maui.Handlers;

namespace Manta.Helpers;

public class CustomWebViewHandler : WebViewHandler
{
#if IOS
    protected override void ConnectHandler(WebKit.WKWebView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ScrollView.Bounces = false;
    }
#endif
}
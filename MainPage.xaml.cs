namespace Manta;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if ANDROID
		var platformView = (Android.Webkit.WebView?)blazorWebView.Handler?.PlatformView;
        if (platformView is not null)
		    platformView.OverScrollMode = Android.Views.OverScrollMode.Never;
#endif
    }
}

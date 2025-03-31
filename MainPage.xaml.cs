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
		var blazorView = this.blazorWebView;
		var platformView = (Android.Webkit.WebView)blazorView.Handler.PlatformView;
		platformView.OverScrollMode = Android.Views.OverScrollMode.Never;
#endif
    }
}

using Microsoft.AspNetCore.Components.WebView.Maui;

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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (stackLayout.Children.Count == 0)
        {
            blazorWebView = new()
            {
                HostPage = "wwwroot/index.html",
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand,
            };

            RootComponent rootComponent = new()
            {
                Selector = "#app",
                ComponentType = typeof(Components.Routes),
            };

            blazorWebView.RootComponents.Add(rootComponent);
            stackLayout.Children.Add(blazorWebView);
        }
    }

    protected override void OnDisappearing()
    {
        stackLayout.Children.Remove(blazorWebView);
        base.OnDisappearing();
    }
}

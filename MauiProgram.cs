using Blazored.LocalStorage;
using Manta.Helpers;
using Manta.Singletons;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Manta.Services;

namespace Manta;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<WebView, CustomWebViewHandler>();
        });

        builder.Services.AddBlazoredLocalStorage();
        builder.Services.AddSingleton<DaemonInfoSingleton>();
        builder.Services.AddSingleton<BalanceSingleton>();
        builder.Services.AddSingleton<NotificationSingleton>();

        builder.Services.AddScoped<ISetupService, SetupService>();

#if ANDROID
        builder.Services.AddTransient<INotificationManagerService, NotificationManagerService>();
#endif

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

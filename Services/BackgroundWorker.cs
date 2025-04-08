#if ANDROID
using Android.Content;
using AndroidX.Work;
using Manta.Singletons;

namespace Manta.Services;

public class BackgroundWorker : Worker
{
    public BackgroundWorker(Context context, WorkerParameters workerParams) : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        try
        {
            var serviceProvider = IPlatformApplication.Current?.Services;
            if (serviceProvider is null)
                throw new Exception("serviceProvider was null");

            using var scope = serviceProvider.CreateScope();

            var notificationSingleton = scope.ServiceProvider.GetRequiredService<NotificationSingleton>();

            Task.Run(notificationSingleton.BackgroundSync);

            return Result.InvokeSuccess(); 
        }
        catch (Exception ex)
        {
            return Result.InvokeFailure();
        }
    }

}

#endif
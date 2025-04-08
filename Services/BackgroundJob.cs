#if ANDROID
using Android.App.Job;
using Android.App;

namespace Manta.Services;

[Service(Permission = JobService.PermissionBind, Exported = true, Name = "com.companyname.manta.BackgroundJob")]
public class BackgroundJob : JobService
{
    public BackgroundJob()
    {

    }

    public override bool OnStartJob(JobParameters? jobParameters)
    {
        var serviceProvider = IPlatformApplication.Current?.Services;
        if (serviceProvider is null)
            throw new Exception("serviceProvider was null");

        var notificationManagerService = serviceProvider.GetRequiredService<INotificationManagerService>();

        notificationManagerService.SendNotification("From job", $"Time: {DateTime.Now.ToShortTimeString()}");

        JobFinished(jobParameters, false);

        return true;
    }

    public override bool OnStopJob(JobParameters? jobParameters)
    {
        var serviceProvider = IPlatformApplication.Current?.Services;
        if (serviceProvider is null)
            throw new Exception("serviceProvider was null");

        var notificationManagerService = serviceProvider.GetRequiredService<INotificationManagerService>();

        notificationManagerService.SendNotification("Job required to stop", $"Time: {DateTime.Now.ToShortTimeString()}");
        return false;
    }
}

#endif
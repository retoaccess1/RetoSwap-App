#if ANDROID
using Android.App.Job;
using Android.App;

namespace Manta.Services;

[Service]
public class MyBackgroundJob : JobService
{
    public override bool OnStartJob(JobParameters? @params)
    {
        Task.Run(async () =>
        {
            Console.WriteLine("Background job running...");
            await Task.Delay(5000);
            JobFinished(@params, false);
        });
        return true;
    }

    public override bool OnStopJob(JobParameters? @params) => false;
}

#endif
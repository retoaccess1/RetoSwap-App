#if ANDROID

using Android.App;
using Android.Content;
using Android.OS;
using System.Collections.Concurrent;

namespace Manta.Services;

[Service]
public class PluginResultsService : IntentService
{
    private static readonly Context _context = Android.App.Application.Context;
    private static int _executionId = 1000;

    private static object _lock = new();

    public const string ExecutionId = "execution_id";

    public static ConcurrentDictionary<string, TaskCompletionSource<string?>> TaskSources { get; set; } = [];

    public PluginResultsService() : base("PluginResultsService") { }

    public static int GetNextExecutionId()
    {
        lock (_lock)
        {
            return _executionId++;
        }
    }

    public static Task<string?> ExecuteTermuxCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        var intent = new Intent("com.termux.RUN_COMMAND");
        intent.SetClassName("com.termux", "com.termux.app.RunCommandService");

        intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");

        intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", ["-c", command]);

        intent.PutExtra("com.termux.RUN_COMMAND_WORKDIR", "/data/data/com.termux/files/home");

        intent.PutExtra("com.termux.RUN_COMMAND_BACKGROUND", true);
        intent.PutExtra("com.termux.RUN_COMMAND_SESSION_ACTION", "0");

        var pluginResultsServiceIntent = new Intent(_context, typeof(PluginResultsService));
        var executionId = GetNextExecutionId();

        // Timeout?
        var taskCompletionSource = new TaskCompletionSource<string?>();

        TaskSources.TryAdd(executionId.ToString(), taskCompletionSource);

        pluginResultsServiceIntent.PutExtra(ExecutionId, executionId);

        var pendingIntent = PendingIntent.GetService(_context,
            executionId,
            pluginResultsServiceIntent,
            PendingIntentFlags.OneShot | (Build.VERSION.SdkInt >= BuildVersionCodes.S ? PendingIntentFlags.Mutable : 0));

        intent.PutExtra("com.termux.RUN_COMMAND_PENDING_INTENT", pendingIntent);

        _context.StartService(intent);

        if (cancellationToken == default)
        {
            return taskCompletionSource.Task;
        }
        else
        {
            var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);

            return Task.WhenAny(taskCompletionSource.Task, cancellationTask)
                .ContinueWith(t =>
                {
                    if (t.Result == cancellationTask)
                        throw new System.OperationCanceledException(cancellationToken);

                    return taskCompletionSource.Task.Result;
                }, cancellationToken);
        }
    }

    public static Task<string?> ExecuteUbuntuCommandAsync(string command)
    {
        return ExecuteTermuxCommandAsync($"bash $PREFIX/bin/ubuntu_exec \"{command}\"");
        //return ExecuteTermuxCommandAsync($"sh exec \"{command}\"");
    }

    protected override void OnHandleIntent(Intent? intent)
    {
        if (intent is null) 
            return;

        var extras = intent?.Extras;
        if (extras is null)
        {
            return;
        }

        string? stdout = null;
        string? executionId;

        var keys = extras.KeySet();
        if (keys is null)
            return;

        TaskCompletionSource<string?>? taskCompletionSource = null;

        foreach (var key in keys)
        {
            var value = extras.Get(key);

            if (value is null)
                continue;

            if (key == "execution_id")
            {
                executionId = value.ToString();
                taskCompletionSource = TaskSources[executionId];
                TaskSources.Remove(executionId, out var _);
            }
            else if (value is Bundle bundle)
            {
                var innerKeySet = bundle.KeySet();
                if (innerKeySet is null)
                    continue;

                foreach (var innerKey in innerKeySet)
                {
                    var a = bundle.Get(innerKey)?.ToString();

                    if (innerKey == "stdout")
                    {
                        stdout = bundle.Get(innerKey)?.ToString();
                        Console.WriteLine($"Stdout: {stdout}");
                    }
                }
            }
        }

        taskCompletionSource?.SetResult(stdout);
    }
}

#endif
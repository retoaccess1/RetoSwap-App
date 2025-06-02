using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Manta.Services;

public static class Proot
{
    private static string _prootPath;
    private static string _rootfsDir;
    private static string _filesDir;
    private static string _tmpDir;
    private static string _homeDir;
    private static string _ubuntuTarName;

    static Proot()
    {
        _ubuntuTarName = "ubuntu-base-" + (RuntimeInformation.OSArchitecture.ToString() == "X64" ? "x86_64" : "arm64-v8a");

        _prootPath = Path.Combine(Android.App.Application.Context.ApplicationInfo?.NativeLibraryDir!, "libprootwrapper.so");
        _filesDir = Android.App.Application.Context.FilesDir!.AbsolutePath;
        _rootfsDir = Path.Combine(_filesDir, _ubuntuTarName);
        _tmpDir = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "tmp");
        _homeDir = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "home");

        Directory.CreateDirectory(_tmpDir);
        SetFullPermissions(_tmpDir);
        Directory.CreateDirectory(_homeDir);
        SetFullPermissions(_homeDir);
    }

    private static void SetFullPermissions(string path)
    {
        try
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static async Task CreateFileWithPermissions(string path, Stream data)
    {
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            await data.CopyToAsync(fs);
        }

        SetFullPermissions(path);
    }

    public static async Task<Stream> DownloadUbuntu()
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"https://github.com/atsamd21/ubuntu-rootfs/releases/latest");

        var latestVersion = response.RequestMessage?.RequestUri?.ToString().Split("tag/v").ElementAt(1);
        if (latestVersion is null)
            latestVersion = "0.0.1";

        return await client.GetStreamAsync($"https://github.com/atsamd21/ubuntu-rootfs/releases/download/v{latestVersion}/{_ubuntuTarName}.tar.gz");
    }

    public static async Task ExtractUbuntu(Stream ubuntuDownloadStream)
    {
        File.SetUnixFileMode(
            _tmpDir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute
        );

        Directory.CreateDirectory(_rootfsDir);
        SetFullPermissions(_rootfsDir);

        using var gzipStream = new GZipStream(ubuntuDownloadStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream);

        TarEntry? entry;
        while ((entry = await tarReader.GetNextEntryAsync()) != null)
        {
            string destPath = Path.Combine(_rootfsDir, entry.Name);
            string? parentDir = Path.GetDirectoryName(destPath);

            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(destPath);
                SetFullPermissions(destPath);
                continue;
            }
            else if (entry.EntryType == TarEntryType.SymbolicLink)
            {
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                    SetFullPermissions(parentDir);
                }

                var target = entry.LinkName;
                var command = $"ln -s \"{target}\" \"{destPath}\"";

                await Java.Lang.Runtime.GetRuntime().Exec(["sh", "-c", command])?.WaitForAsync();
            }
            else
            {
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                    SetFullPermissions(parentDir);
                }

                if (entry.DataStream is not null)
                    await CreateFileWithPermissions(destPath, entry.DataStream);
            }
        }

        await ubuntuDownloadStream.DisposeAsync();
    }

    public static string RunProotUbuntuCommand(string command, params string[] arguments)
    {
        var args = new string[]
        {
            _prootPath,
            "-R", _rootfsDir,
            "--link2symlink",
            "-0",
            "-b", $"{_tmpDir}:/tmp",
            "-b", $"{_homeDir}:/home",
            "-w", "/",
            "-b", "/dev",
            "-b", "/proc",
            "-b", "/sys",
            "-b", "/apex:/apex",
            "-b", "/system",
            $"/usr/bin/{command}",
            // Exit?
        }.Concat(arguments).ToArray();

        var processBuilder = new Java.Lang.ProcessBuilder()
            .Command(args)?
            .RedirectErrorStream(true);

        var env = processBuilder?.Environment();
        env?.Remove("TMPDIR");
        env?.Remove("PROOT_TMP_DIR");

        env?.Add("TMPDIR", _tmpDir);
        env?.Add("PROOT_TMP_DIR", _tmpDir);

        var process = processBuilder?.Start();
        if (process is null || process.InputStream is null)
            throw new Exception("process is null or process.InputStream is null");

        using var streamReader = new StreamReader(process.InputStream);

        // Might not be such a good idea with long running processes
        var stringBuilder = new StringBuilder();
        string? line;
        while ((line = streamReader.ReadLine()) is not null)
        {
            Console.WriteLine(line);
            stringBuilder.AppendLine(line);
        }

        process.WaitFor();

        return stringBuilder.ToString();
    }

    public static StreamReader RunProotUbuntuCommand(string command, CancellationToken cancellationToken, params string[] arguments)
    {
        var args = new string[]
        {
            _prootPath,
            "-R", _rootfsDir,
            "--link2symlink",
            "-0",
            "-b", $"{_tmpDir}:/tmp",
            "-b", $"{_homeDir}:/home",
            "-w", "/",
            "-b", "/dev",
            "-b", "/proc",
            "-b", "/sys",
            "-b", "/apex:/apex",
            "-b", "/system",
            $"/usr/bin/{command}",
            // Exit?
        }.Concat(arguments).ToArray();

        var processBuilder = new Java.Lang.ProcessBuilder()
            .Command(args)?
            .RedirectErrorStream(true);

        var env = processBuilder?.Environment();
        env?.Remove("TMPDIR");
        env?.Remove("PROOT_TMP_DIR");

        env?.Add("TMPDIR", _tmpDir);
        env?.Add("PROOT_TMP_DIR", _tmpDir);

        var process = processBuilder?.Start();
        if (process is null || process.InputStream is null)
            throw new Exception("process is null or process.InputStream is null");

        cancellationToken.Register(() =>
        {
            process.Dispose();
        });

        return new StreamReader(process.InputStream);
    }
}

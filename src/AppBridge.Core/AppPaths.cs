using System.Runtime.InteropServices;

namespace AppBridge.Core;

/// <summary>
/// Resolves where AppBridge keeps its data. An installed copy uses %LOCALAPPDATA%\AppBridge;
/// a portable copy (marked by <see cref="PortableMarkerFileName"/> next to the executable)
/// keeps everything in a Data folder beside itself.
/// </summary>
public sealed class AppPaths
{
    public const string PortableMarkerFileName = "portable.txt";

    private AppPaths(string appDirectory, bool isPortable, string dataDirectory, string defaultDownloadDirectory)
    {
        AppDirectory = appDirectory;
        IsPortable = isPortable;
        DataDirectory = dataDirectory;
        DefaultDownloadDirectory = defaultDownloadDirectory;
    }

    public string AppDirectory { get; }

    public bool IsPortable { get; }

    public string DataDirectory { get; }

    public string DefaultDownloadDirectory { get; }

    public string LogsDirectory => Path.Combine(DataDirectory, "Logs");

    /// <summary>Home-screen icons read from iPhones, so saved apps keep theirs.</summary>
    public string IconsDirectory => Path.Combine(DataDirectory, "Icons");

    /// <summary>Private home folder for ipatool so its session never mixes with other installs.</summary>
    public string IpatoolHome => Path.Combine(DataDirectory, "ipatool");

    public string PairRecordsDirectory => Path.Combine(DataDirectory, "PairRecords");

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string IpatoolExecutable => Path.Combine(AppDirectory, "tools", "ipatool.exe");

    public static AppPaths Detect(string? appDirectory = null)
    {
        appDirectory ??= AppContext.BaseDirectory;
        appDirectory = Path.GetFullPath(appDirectory);

        if (File.Exists(Path.Combine(appDirectory, PortableMarkerFileName)))
        {
            var data = Path.Combine(appDirectory, "Data");
            return new AppPaths(appDirectory, true, data, Path.Combine(data, "Downloads"));
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(
            appDirectory,
            false,
            Path.Combine(localAppData, "AppBridge"),
            Path.Combine(GetDownloadsFolder(), "AppBridge"));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(IpatoolHome);
        Directory.CreateDirectory(PairRecordsDirectory);
    }

    private static string GetDownloadsFolder()
    {
        var downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        if (SHGetKnownFolderPath(downloads, 0, IntPtr.Zero, out var pathPtr) == 0)
        {
            try
            {
                var path = Marshal.PtrToStringUni(pathPtr);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);
}

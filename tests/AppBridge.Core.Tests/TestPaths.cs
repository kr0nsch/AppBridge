namespace AppBridge.Core.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AppBridge.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    public static string? Ipatool()
    {
        var path = Path.Combine(RepositoryRoot, "artifacts", "tools", "win-x64", "ipatool.exe");
        return File.Exists(path) ? path : null;
    }

    public static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "fr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

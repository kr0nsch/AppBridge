using AppBridge.Core.Ipatool;

namespace AppBridge.Core.Tests;

public class IpatoolClientTests
{
    [Fact]
    public void PrivateHomeSplitsDriveAndPath()
    {
        var environment = new Dictionary<string, string?> { ["XDG_STATE_HOME"] = @"C:\state", ["XDG_DATA_HOME"] = @"C:\data" };

        IpatoolClient.ConfigureEnvironment(environment, @"C:\Users\anna\AppData\Local\AppBridge\ipatool");

        Assert.Equal("C:", environment["HOMEDRIVE"]);
        Assert.Equal(@"\Users\anna\AppData\Local\AppBridge\ipatool", environment["HOMEPATH"]);
        Assert.False(environment.ContainsKey("XDG_STATE_HOME"));
        Assert.False(environment.ContainsKey("XDG_DATA_HOME"));
    }

    [Fact]
    public void PrivateHomeSupportsUncPaths()
    {
        var environment = new Dictionary<string, string?>();

        IpatoolClient.ConfigureEnvironment(environment, @"\\server\share\AppBridge\Data\ipatool");

        Assert.Equal("", environment["HOMEDRIVE"]);
        Assert.Equal(@"\\server\share\AppBridge\Data\ipatool", environment["HOMEPATH"]);
    }

    [Fact]
    public async Task MissingExecutableIsReported()
    {
        var client = new IpatoolClient(@"C:\does-not-exist\ipatool.exe", Path.Combine(Path.GetTempPath(), "fr-missing"));

        Assert.False(client.IsAvailable);
        var ex = await Assert.ThrowsAsync<IpatoolException>(() => client.GetAccountAsync());
        Assert.Equal(IpatoolErrorKind.ToolMissing, ex.Kind);
    }

    /// <summary>
    /// Runs the real bundled ipatool against an empty private home: it must report "not signed
    /// in" and must not touch any other ipatool session on this machine.
    /// </summary>
    [Fact]
    public async Task RealIpatoolReportsSignedOutForFreshHome()
    {
        var exe = TestPaths.Ipatool();
        if (exe is null)
            return; // Run build\Get-Ipatool.ps1 first to enable this test.

        var home = Path.Combine(Path.GetTempPath(), "fr-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var client = new IpatoolClient(exe, home);
            var account = await client.GetAccountAsync();

            Assert.Null(account);
            Assert.True(File.Exists(Path.Combine(home, "keychain.key")), "the DPAPI-protected passphrase should be created");
            Assert.True(Directory.Exists(Path.Combine(home, ".ipatool")), "ipatool should keep its state inside the private home");
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }
}

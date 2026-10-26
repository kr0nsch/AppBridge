using AppBridge.Core.Ipatool;

namespace AppBridge.Core.Tests;

public class IpatoolOutputTests
{
    [Fact]
    public void ParsesSuccessEvent()
    {
        var output = new IpatoolOutput();
        Assert.True(output.TryAdd("""{"level":"info","name":"Anna","email":"anna@example.com","success":true,"time":"2026-10-26T19:35:56+03:00"}"""));

        Assert.True(output.Succeeded);
        Assert.Equal("anna@example.com", IpatoolOutput.GetString(output.SuccessEvent!.Value, "email"));
        Assert.Null(output.Error);
    }

    [Fact]
    public void ParsesErrorEvent()
    {
        var output = new IpatoolOutput();
        output.TryAdd("""{"level":"error","error":"failed to get account: failed to get item: aes.KeyUnwrap(): integrity check failed.","success":false}""");

        Assert.False(output.Succeeded);
        Assert.StartsWith("failed to get account", output.Error);
        Assert.Equal(IpatoolErrorKind.NotSignedIn, IpatoolOutput.Classify(output.Error));
    }

    [Fact]
    public void CollectsMessagesForTwoFactorPrompt()
    {
        var output = new IpatoolOutput();
        output.TryAdd("""{"level":"info","message":"preparing authentication; the first login may take a few minutes"}""");
        output.TryAdd("""{"level":"info","message":"2FA code is required; run the command again and supply a code using the `--auth-code` flag"}""");

        Assert.Contains(output.Messages, m => m.Contains("2FA code is required"));
        Assert.False(output.Succeeded);
    }

    [Theory]
    [InlineData("downloading")]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("[1,2,3]")]
    public void IgnoresNonEventSegments(string segment) => Assert.False(new IpatoolOutput().TryAdd(segment));

    [Theory]
    [InlineData("downloading  45% |█████     | (123/274 MB, 12 MB/s)", 0.45)]
    [InlineData("downloading 100% |██████████| (274/274 MB)", 1.0)]
    [InlineData("downloading   0% |          | (0 B/274 MB)", 0.0)]
    public void ReadsProgressBar(string segment, double expected) =>
        Assert.Equal(expected, IpatoolOutput.ParseProgressFraction(segment)!.Value, 3);

    [Theory]
    [InlineData("downloading (12 MB, 3 MB/s)")]
    [InlineData("version 2.7.0")]
    public void ProgressWithoutPercentIsUnknown(string segment) => Assert.Null(IpatoolOutput.ParseProgressFraction(segment));

    [Theory]
    [InlineData("license is required", IpatoolErrorKind.LicenseRequired)]
    [InlineData("failed to purchase item with param 'STDQ': purchasing paid apps is not supported", IpatoolErrorKind.PaidAppNotSupported)]
    [InlineData("password token is expired", IpatoolErrorKind.SessionExpired)]
    [InlineData("received error: item is temporarily unavailable", IpatoolErrorKind.TemporarilyUnavailable)]
    [InlineData("app not found", IpatoolErrorKind.AppNotFound)]
    [InlineData("request failed: dial tcp: lookup p25-buy.itunes.apple.com: no such host", IpatoolErrorKind.Network)]
    [InlineData("keychain passphrase is required when not running in interactive mode", IpatoolErrorKind.NotSignedIn)]
    [InlineData("subscription required", IpatoolErrorKind.SubscriptionRequired)]
    [InlineData("something unexpected", IpatoolErrorKind.Unknown)]
    [InlineData(null, IpatoolErrorKind.Unknown)]
    public void ClassifiesErrors(string? error, IpatoolErrorKind expected) => Assert.Equal(expected, IpatoolOutput.Classify(error));
}

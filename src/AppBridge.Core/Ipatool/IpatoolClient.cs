using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AppBridge.Core.Ipatool;

/// <summary>
/// Runs the bundled open-source ipatool (https://github.com/majd/ipatool) to sign in to the
/// App Store and download app packages that the signed-in Apple Account is licensed for.
/// </summary>
/// <remarks>
/// ipatool keeps its session under %HOMEDRIVE%%HOMEPATH%\.ipatool on Windows, so every process
/// gets a private home folder. Only one ipatool process runs at a time because they share the
/// cookie jar and keychain files.
/// </remarks>
public sealed class IpatoolClient : IAppStoreClient
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    private readonly string _executable;
    private readonly string _home;
    private readonly Lazy<string> _passphrase;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public IpatoolClient(string executable, string home)
    {
        _executable = executable;
        _home = home;
        _passphrase = new Lazy<string>(() => KeychainPassphrase.GetOrCreate(home), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsAvailable => File.Exists(_executable);

    /// <summary>Returns the signed-in account, or null when nobody is signed in.</summary>
    public async Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(["auth", "info"], interactive: false, onSegment: null, CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (output.SuccessEvent is { } ok)
            return new IpatoolAccount(IpatoolOutput.GetString(ok, "email") ?? "", IpatoolOutput.GetString(ok, "name") ?? "");

        var kind = IpatoolOutput.Classify(output.Error);
        if (kind == IpatoolErrorKind.NotSignedIn)
            return null;
        throw new IpatoolException(kind, output.Error ?? "ipatool did not report the account state.");
    }

    public async Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default)
    {
        List<string> args = ["auth", "login", "--email", email, "--password", password];
        if (!string.IsNullOrWhiteSpace(authCode))
            args.AddRange(["--auth-code", authCode.Trim()]);

        // The first login can take a few minutes while ipatool prepares its device identity.
        var output = await RunAsync(args, interactive: false, onSegment: null, TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
        if (output.SuccessEvent is { } ok)
        {
            var account = new IpatoolAccount(IpatoolOutput.GetString(ok, "email") ?? email, IpatoolOutput.GetString(ok, "name") ?? "");
            AppLog.Info("Signed in to the App Store.");
            return new LoginResult(LoginStatus.SignedIn, account);
        }

        if (output.Messages.Any(m => m.Contains("2FA code is required", StringComparison.OrdinalIgnoreCase)))
            return new LoginResult(LoginStatus.CodeRequired);

        var error = output.Error ?? "Sign-in failed.";
        AppLog.Warn($"App Store sign-in failed: {error}");
        return new LoginResult(LoginStatus.Failed, Error: error);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(["auth", "revoke"], interactive: false, onSegment: null, CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (!output.Succeeded && IpatoolOutput.Classify(output.Error) != IpatoolErrorKind.NotSignedIn)
            throw new IpatoolException(IpatoolOutput.Classify(output.Error), output.Error ?? "Sign-out failed.");
        AppLog.Info("Signed out of the App Store.");
    }

    /// <summary>Downloads an IPA and returns the path of the finished file.</summary>
    public async Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);

        List<string> args = ["download", "--output", request.OutputPath];
        if (request.AppId > 0)
            args.AddRange(["--app-id", request.AppId.ToString(CultureInfo.InvariantCulture)]);
        else
            args.AddRange(["--bundle-identifier", request.BundleId]);
        if (!string.IsNullOrEmpty(request.ExternalVersionId))
            args.AddRange(["--external-version-id", request.ExternalVersionId]);
        if (request.AcquireLicense)
            args.Add("--purchase");

        // ipatool only draws its progress bar in interactive mode. Every value it could prompt
        // for is passed on the command line and stdin is closed, so it never waits for input.
        double? lastFraction = null;
        var tempFile = request.OutputPath + ".tmp";
        using var poll = new CancellationTokenSource();
        var pollTask = Task.Run(async () =>
        {
            while (!poll.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(400, poll.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var bytes = TryGetLength(tempFile);
                if (lastFraction >= 1.0)
                    progress?.Report(new DownloadProgress(DownloadPhase.Finalizing, 1.0, bytes));
                else if (bytes > 0)
                    progress?.Report(new DownloadProgress(DownloadPhase.Downloading, lastFraction, bytes));
            }
        }, CancellationToken.None);

        IpatoolOutput output;
        try
        {
            output = await RunAsync(args, interactive: true, segment =>
            {
                if (IpatoolOutput.ParseProgressFraction(segment) is { } fraction)
                    lastFraction = fraction;
            }, timeout: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await poll.CancelAsync().ConfigureAwait(false);
            await pollTask.ConfigureAwait(false);
        }

        if (output.SuccessEvent is { } ok)
        {
            var path = IpatoolOutput.GetString(ok, "output");
            path = string.IsNullOrEmpty(path) ? request.OutputPath : Path.GetFullPath(path);
            if (!File.Exists(path))
                throw new IpatoolException(IpatoolErrorKind.Unknown, "ipatool reported success but the IPA file is missing.");
            return path;
        }

        var error = output.Error ?? "The download did not finish.";
        throw new IpatoolException(IpatoolOutput.Classify(error), error);
    }

    internal static void ConfigureEnvironment(IDictionary<string, string?> environment, string home)
    {
        var full = Path.GetFullPath(home);
        var root = Path.GetPathRoot(full) ?? "";
        if (root.Length >= 2 && root[1] == ':')
        {
            environment["HOMEDRIVE"] = full[..2];
            environment["HOMEPATH"] = full[2..];
        }
        else
        {
            environment["HOMEDRIVE"] = "";
            environment["HOMEPATH"] = full;
        }

        // These would redirect (and migrate) ipatool's state away from our private folder.
        environment.Remove("XDG_STATE_HOME");
        environment.Remove("XDG_DATA_HOME");
    }

    private static long TryGetLength(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private async Task<IpatoolOutput> RunAsync(IReadOnlyList<string> args, bool interactive, Action<string>? onSegment, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
            throw new IpatoolException(IpatoolErrorKind.ToolMissing, $"ipatool was not found at {_executable}.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_home);
            var psi = new ProcessStartInfo(_executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = _home,
            };
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);
            psi.ArgumentList.Add("--format");
            psi.ArgumentList.Add("json");
            if (!interactive)
                psi.ArgumentList.Add("--non-interactive");
            psi.ArgumentList.Add("--keychain-passphrase");
            psi.ArgumentList.Add(_passphrase.Value);
            ConfigureEnvironment(psi.Environment, _home);

            // Log the verb only: arguments can contain the Apple Account password.
            AppLog.Info($"ipatool {string.Join(' ', args.Take(2))}");

            using var process = new Process { StartInfo = psi };
            process.Start();
            process.StandardInput.Close();

            var output = new IpatoolOutput();
            var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            var stdout = PumpAsync(process.StandardOutput, segment =>
            {
                if (!output.TryAdd(segment))
                    onSegment?.Invoke(segment);
            });

            using var timeoutCts = timeout is { } t ? new CancellationTokenSource(t) : new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                if (cancellationToken.IsCancellationRequested)
                    throw;
                throw new IpatoolException(IpatoolErrorKind.Network, "ipatool did not respond in time.");
            }

            await stdout.ConfigureAwait(false);
            var errorText = (await stderr.ConfigureAwait(false)).Trim();
            if (errorText.Length > 0)
                AppLog.Warn($"ipatool stderr: {errorText}");
            if (output.Error is { } error)
                AppLog.Warn($"ipatool error: {error}");
            return output;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Splits output on both CR and LF so progress-bar redraws arrive as separate segments.</summary>
    private static async Task PumpAsync(StreamReader reader, Action<string> onSegment)
    {
        var buffer = new char[4096];
        var segment = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (c is '\r' or '\n')
                {
                    if (segment.Length > 0)
                    {
                        onSegment(segment.ToString());
                        segment.Clear();
                    }
                }
                else
                {
                    segment.Append(c);
                }
            }
        }

        if (segment.Length > 0)
            onSegment(segment.ToString());
    }
}

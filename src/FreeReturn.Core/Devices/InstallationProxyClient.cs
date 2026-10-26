using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;

namespace FreeReturn.Core.Devices;

/// <summary>
/// Minimal com.apple.mobile.installation_proxy client. Unlike the library's built-in service it
/// installs a package that is already staged on the device (so uploads can be streamed with
/// progress) and passes the App Store metadata and SINF the way Apple's own tools do.
/// </summary>
internal sealed class InstallationProxyClient(LockdownServiceProvider lockdown, ILogger logger)
    : LockdownService(lockdown, "com.apple.mobile.installation_proxy", "com.apple.mobile.installation_proxy.shim.remote", logger: logger)
{
    public async Task<List<DictionaryNode>> BrowseUserAppsAsync(IEnumerable<string> attributes, CancellationToken cancellationToken)
    {
        var returnAttributes = new ArrayNode();
        foreach (var attribute in attributes)
            returnAttributes.Add(new StringNode(attribute));

        var command = new DictionaryNode
        {
            { "Command", new StringNode("Browse") },
            {
                "ClientOptions", new DictionaryNode
                {
                    { "ApplicationType", new StringNode("User") },
                    { "ReturnAttributes", returnAttributes },
                }
            },
        };
        await Service.SendPlistAsync(command, cancellationToken: cancellationToken).ConfigureAwait(false);

        List<DictionaryNode> apps = [];
        while (true)
        {
            var response = await ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (response.TryGetValue("CurrentList", out var list) && list is ArrayNode items)
                apps.AddRange(items.OfType<DictionaryNode>());
            if (GetString(response, "Status") == "Complete")
                return apps;
        }
    }

    public async Task InstallAsync(string packagePath, IpaPackage package, Action<int> onPercent, CancellationToken cancellationToken)
    {
        var options = new DictionaryNode();
        if (package.ITunesMetadata is { } metadata)
            options.Add("iTunesMetadata", new DataNode(metadata));
        if (package.Sinf is { } sinf)
            options.Add("ApplicationSINF", new DataNode(sinf));

        var command = new DictionaryNode
        {
            { "Command", new StringNode("Install") },
            { "ClientOptions", options },
            { "PackagePath", new StringNode(packagePath) },
        };
        await Service.SendPlistAsync(command, cancellationToken: cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var response = await ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (response.TryGetValue("PercentComplete", out var percent) && percent is IntegerNode value)
                onPercent((int)Math.Clamp(value.SignedValue, 0, 100));
            if (GetString(response, "Status") == "Complete")
                return;
        }
    }

    private async Task<DictionaryNode> ReceiveAsync(CancellationToken cancellationToken)
    {
        var node = await Service.ReceivePlistAsync(cancellationToken).ConfigureAwait(false);
        if (node is not DictionaryNode response)
            throw new DeviceOperationException(DeviceErrorKind.Disconnected, "The iPhone closed the installation connection.");

        if (GetString(response, "Error") is { } error)
        {
            var description = GetString(response, "ErrorDescription") ?? error;
            var kind = error switch
            {
                "DeviceOSVersionTooLow" => DeviceErrorKind.OsTooOld,
                "InsufficientStorage" or "OutOfSpace" => DeviceErrorKind.NotEnoughSpace,
                _ => DeviceErrorKind.InstallFailed,
            };
            throw new DeviceOperationException(kind, $"{error}: {description}");
        }

        return response;
    }

    private static string? GetString(DictionaryNode dict, string key) =>
        dict.TryGetValue(key, out var node) && node is StringNode s ? s.Value : null;
}

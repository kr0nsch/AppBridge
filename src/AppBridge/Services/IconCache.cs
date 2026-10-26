using System.Collections.Concurrent;
using AppBridge.Core;
using AppBridge.Core.Library;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace AppBridge.Services;

/// <summary>
/// Home-screen icons shared by every list in the app. Icons read from an iPhone are also kept
/// on disk, so saved apps still show them weeks later when that iPhone is gone.
/// </summary>
public sealed class IconCache(string folder)
{
    private readonly ConcurrentDictionary<string, byte[]> _png = new();
    private readonly Dictionary<string, ImageSource> _images = [];

    public void Store(string bundleId, byte[] png)
    {
        _png[bundleId] = png;
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(FilePath(bundleId), png);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Debug($"Could not keep the icon of {bundleId}: {ex.Message}");
        }
    }

    /// <summary>Creates (or reuses) a decoded image. Must run on the UI thread.</summary>
    public async Task<ImageSource?> GetAsync(string bundleId)
    {
        if (_images.TryGetValue(bundleId, out var cached))
            return cached;
        if (!_png.TryGetValue(bundleId, out var png))
        {
            png = await Task.Run(() => TryReadFile(bundleId));
            if (png is null)
                return null;
            _png[bundleId] = png;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(png);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = 96 };
            await bitmap.SetSourceAsync(stream);
            _images[bundleId] = bitmap;
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The icon of a saved app: from the cache, or read out of its .ipa file. Must run on the UI thread.</summary>
    public async Task<ImageSource?> GetAsync(SavedApp app)
    {
        if (await GetAsync(app.BundleId) is { } known)
            return known;
        var png = await Task.Run(() => SavedAppLibrary.ReadIcon(app.FilePath));
        if (png is null)
            return null;
        Store(app.BundleId, png);
        return await GetAsync(app.BundleId);
    }

    private string FilePath(string bundleId) =>
        Path.Combine(folder, string.Concat(bundleId.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')) + ".png");

    private byte[]? TryReadFile(string bundleId)
    {
        try
        {
            var path = FilePath(bundleId);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

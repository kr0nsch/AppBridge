using System.Collections.Concurrent;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace AppBridge.Services;

/// <summary>Home-screen icons read from the source iPhone, shared by every list in the app.</summary>
public sealed class IconCache
{
    private readonly ConcurrentDictionary<string, byte[]> _png = new();
    private readonly Dictionary<string, ImageSource> _images = [];

    public void Store(string bundleId, byte[] png) => _png[bundleId] = png;

    /// <summary>Creates (or reuses) a decoded image. Must run on the UI thread.</summary>
    public async Task<ImageSource?> GetAsync(string bundleId)
    {
        if (_images.TryGetValue(bundleId, out var cached))
            return cached;
        if (!_png.TryGetValue(bundleId, out var png))
            return null;

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
}

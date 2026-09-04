using System.Text;

namespace HearthstoneDeckTracker.Core.Cards;

/// <summary>
/// Downloads HearthstoneJSON rendered card art and stores it locally, keyed by CardID.
/// Art is Blizzard IP — cache is for personal tool display only; do not redistribute.
/// </summary>
public sealed class CardArtCache
{
    public const string DefaultUrlTemplate = "https://art.hearthstonejson.com/v1/render/latest/zhCN/256x/{0}.png";

    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly string _urlTemplate;
    private readonly HashSet<string> _missing = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public CardArtCache(
        HttpClient httpClient,
        string? cacheDirectory = null,
        string? urlTemplate = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cacheDirectory = cacheDirectory ?? AppCachePaths.ArtDirectory;
        _urlTemplate = string.IsNullOrWhiteSpace(urlTemplate) ? DefaultUrlTemplate : urlTemplate;
        AppCachePaths.EnsureDirectory(_cacheDirectory);
    }

    public string CacheDirectory => _cacheDirectory;

    public string BuildUrl(string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        return string.Format(_urlTemplate, cardId);
    }

    /// <summary>Local PNG path if already downloaded; otherwise <c>null</c> (no network).</summary>
    public string? TryGetCachedPath(string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;
        var path = PathFor(cardId);
        return File.Exists(path) && new FileInfo(path).Length > 0 ? path : null;
    }

    /// <summary>
    /// Returns a local file path for the card art, downloading once if needed.
    /// Unknown / failed downloads return <c>null</c> and never throw to the UI.
    /// </summary>
    public async Task<string?> GetOrDownloadAsync(string? cardId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;

        var cached = TryGetCachedPath(cardId);
        if (cached is not null)
            return cached;

        lock (_gate)
        {
            if (_missing.Contains(cardId))
                return null;
        }

        try
        {
            using var response = await _http.GetAsync(
                    BuildUrl(cardId),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                RememberMissing(cardId);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                RememberMissing(cardId);
                return null;
            }

            var path = PathFor(cardId);
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            return path;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            RememberMissing(cardId);
            return null;
        }
    }

    public string PathFor(string cardId) =>
        Path.Combine(_cacheDirectory, SanitizeFileName(cardId) + ".png");

    private void RememberMissing(string cardId)
    {
        lock (_gate)
            _missing.Add(cardId);
    }

    internal static string SanitizeFileName(string cardId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(cardId.Length);
        foreach (var ch in cardId)
            builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        var name = builder.ToString();
        return string.IsNullOrWhiteSpace(name) ? "_card" : name;
    }
}

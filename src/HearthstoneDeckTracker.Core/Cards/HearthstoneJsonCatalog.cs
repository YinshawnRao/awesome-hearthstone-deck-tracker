using System.Text.Json;
using System.Text.Json.Serialization;

namespace HearthstoneDeckTracker.Core.Cards;

/// <summary>
/// Fetches <c>cards.json</c> from HearthstoneJSON, caches it on disk with a TTL, and
/// indexes cards by Power.log <c>CardID</c>. Full <c>cards.json</c> (not collectible-only)
/// so tokens / generated IDs that appear in logs still resolve.
/// </summary>
public sealed class HearthstoneJsonCatalog
{
    public const string DefaultCatalogUrl = "https://api.hearthstonejson.com/v1/latest/zhCN/cards.json";
    public const string DefaultLocale = "zhCN";
    public const string DefaultArtSize = "256x";

    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly HttpClient _http;
    private readonly string _cacheFile;
    private readonly string _catalogUrl;
    private readonly TimeSpan _ttl;
    private readonly Func<DateTimeOffset> _utcNow;
    private IReadOnlyDictionary<string, CardInfo> _cards =
        new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase);

    public HearthstoneJsonCatalog(
        HttpClient httpClient,
        string? cacheDirectory = null,
        TimeSpan? ttl = null,
        string? catalogUrl = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        var cacheRoot = cacheDirectory ?? AppCachePaths.CacheRoot;
        AppCachePaths.EnsureDirectory(cacheRoot);
        _cacheFile = Path.Combine(cacheRoot, "cards.zhCN.json");
        _ttl = ttl ?? DefaultTtl;
        _catalogUrl = string.IsNullOrWhiteSpace(catalogUrl) ? DefaultCatalogUrl : catalogUrl;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public IReadOnlyDictionary<string, CardInfo> Cards => _cards;

    public int Count => _cards.Count;

    public string CacheFilePath => _cacheFile;

    public string CatalogUrl => _catalogUrl;

    public CardInfo? Get(string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;
        return _cards.TryGetValue(cardId, out var info) ? info : null;
    }

    public bool TryGet(string? cardId, out CardInfo? info)
    {
        info = Get(cardId);
        return info is not null;
    }

    /// <summary>zhCN name, or the raw CardID when unknown / empty.</summary>
    public string DisplayName(string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return "未知";
        var info = Get(cardId);
        return string.IsNullOrWhiteSpace(info?.Name) ? cardId : info!.Name!;
    }

    public static string BuildArtUrl(string cardId, string locale = DefaultLocale, string size = DefaultArtSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        return $"https://art.hearthstonejson.com/v1/render/latest/{locale}/{size}/{cardId}.png";
    }

    /// <summary>Parse a HearthstoneJSON card array. Missing / blank ids are skipped.</summary>
    public static IReadOnlyDictionary<string, CardInfo> ParseJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return ParseJson(System.Text.Encoding.UTF8.GetBytes(json));
    }

    public static IReadOnlyDictionary<string, CardInfo> ParseJson(ReadOnlySpan<byte> utf8Json)
    {
        List<HsJsonCardDto>? dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<List<HsJsonCardDto>>(utf8Json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("HearthstoneJSON cards.json is not a JSON array of cards.", ex);
        }

        var map = new Dictionary<string, CardInfo>(dtos?.Count ?? 0, StringComparer.OrdinalIgnoreCase);
        if (dtos is null)
            return map;

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Id))
                continue;
            if (map.ContainsKey(dto.Id))
                continue;

            map[dto.Id] = new CardInfo(
                CardId: dto.Id,
                Name: string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name,
                Cost: dto.Cost,
                Rarity: dto.Rarity,
                CardClass: dto.CardClass,
                Type: dto.Type,
                ArtUrl: BuildArtUrl(dto.Id));
        }

        return map;
    }

    public void ReplaceCards(IReadOnlyDictionary<string, CardInfo> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        _cards = cards is Dictionary<string, CardInfo> typed
            && typed.Comparer == StringComparer.OrdinalIgnoreCase
            ? typed
            : new Dictionary<string, CardInfo>(cards, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<CatalogLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var cacheAge = TryCacheTimestamp();
        if (cacheAge is { } fetched && _utcNow() - fetched < _ttl
            && TryLoadCache(out var cached, out _))
        {
            _cards = cached;
            return new CatalogLoadResult(cached.Count, CatalogSource.Cache, fetched, Error: null);
        }

        try
        {
            using var response = await _http.GetAsync(
                    _catalogUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var parsed = ParseJson(bytes);
            await WriteCacheAtomicallyAsync(bytes, cancellationToken).ConfigureAwait(false);
            _cards = parsed;
            return new CatalogLoadResult(parsed.Count, CatalogSource.Network, _utcNow(), Error: null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or IOException)
        {
            if (TryLoadCache(out var fallback, out var fallbackFetched))
            {
                _cards = fallback;
                return new CatalogLoadResult(fallback.Count, CatalogSource.OfflineCache, fallbackFetched, ex.Message);
            }

            return new CatalogLoadResult(0, CatalogSource.None, FetchedAt: null, ex.Message);
        }
    }

    private DateTimeOffset? TryCacheTimestamp()
    {
        try
        {
            if (!File.Exists(_cacheFile))
                return null;
            return new DateTimeOffset(File.GetLastWriteTimeUtc(_cacheFile), TimeSpan.Zero);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private bool TryLoadCache(out IReadOnlyDictionary<string, CardInfo> cards, out DateTimeOffset? fetchedAt)
    {
        cards = new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase);
        fetchedAt = TryCacheTimestamp();
        try
        {
            if (!File.Exists(_cacheFile))
                return false;
            var bytes = File.ReadAllBytes(_cacheFile);
            cards = ParseJson(bytes);
            return cards.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task WriteCacheAtomicallyAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_cacheFile);
        if (!string.IsNullOrEmpty(directory))
            AppCachePaths.EnsureDirectory(directory);

        var temp = _cacheFile + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temp, _cacheFile, overwrite: true);
    }

    private sealed class HsJsonCardDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("cost")]
        public int? Cost { get; set; }

        [JsonPropertyName("rarity")]
        public string? Rarity { get; set; }

        [JsonPropertyName("cardClass")]
        public string? CardClass { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}

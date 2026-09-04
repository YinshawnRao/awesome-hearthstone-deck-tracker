using System.Net;
using HearthstoneDeckTracker.Core.Cards;
using HearthstoneDeckTracker.Core.Logging;

namespace HearthstoneDeckTracker.Core.Tests;

public class HearthstoneJsonCatalogTests
{
    [Fact]
    public void ParseJson_IndexesKnownCardIds_AndSkipsBlankIds()
    {
        var cards = HearthstoneJsonCatalog.ParseJson(ReadFixture());

        Assert.Equal(7, cards.Count);
        Assert.False(cards.ContainsKey(""));

        var fireball = cards["CS2_029"];
        Assert.Equal("火球术", fireball.Name);
        Assert.Equal(4, fireball.Cost);
        Assert.Equal("FREE", fireball.Rarity);
        Assert.Equal("MAGE", fireball.CardClass);
        Assert.Equal("SPELL", fireball.Type);
        Assert.Equal("https://art.hearthstonejson.com/v1/render/latest/zhCN/256x/CS2_029.png", fireball.ArtUrl);

        var sheep = cards["CS2_tk1"];
        Assert.Equal("绵羊", sheep.Name);
        Assert.False(sheep.ArtUrl.Contains("collectible", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Get_ResolvesKnownIds_CaseInsensitive_UnknownReturnsNull()
    {
        var catalog = CatalogFromFixture();

        Assert.Equal("激活", catalog.Get("EX1_169")?.Name);
        Assert.Equal("激活", catalog.Get("ex1_169")?.Name);
        Assert.Null(catalog.Get("NOT_A_REAL_CARD"));
        Assert.Null(catalog.Get(null));
        Assert.Null(catalog.Get(""));
        Assert.Equal("未知", catalog.DisplayName(null));
        Assert.Equal("NOT_A_REAL_CARD", catalog.DisplayName("NOT_A_REAL_CARD"));
        Assert.Equal("火球术", catalog.DisplayName("CS2_029"));
    }

    [Fact]
    public async Task LoadAsync_UsesFreshDiskCache_WithoutHttp()
    {
        using var tmp = new TempDir();
        var cacheFile = Path.Combine(tmp.Path, "cards.zhCN.json");
        File.WriteAllText(cacheFile, ReadFixture());
        File.SetLastWriteTimeUtc(cacheFile, new DateTime(2026, 9, 4, 7, 0, 0, DateTimeKind.Utc));
        var now = new DateTimeOffset(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);
        var handler = new ScriptedHttpHandler(HttpStatusCode.InternalServerError, "should not be called");
        using var http = new HttpClient(handler);
        var catalog = new HearthstoneJsonCatalog(http, tmp.Path, TimeSpan.FromHours(24), utcNow: () => now);

        var result = await catalog.LoadAsync();

        Assert.Equal(0, handler.Calls);
        Assert.Equal(CatalogSource.Cache, result.Source);
        Assert.Equal(7, result.Count);
        Assert.Equal("已加载 7 张", result.StatusText);
        Assert.Equal("奥妮克希亚", catalog.DisplayName("EX1_562"));
    }

    [Fact]
    public async Task LoadAsync_FetchesWhenCacheIsStale()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "cards.zhCN.json"), "[]");
        File.SetLastWriteTimeUtc(Path.Combine(tmp.Path, "cards.zhCN.json"), new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var now = new DateTimeOffset(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);
        var handler = new ScriptedHttpHandler(HttpStatusCode.OK, ReadFixture());
        using var http = new HttpClient(handler);
        var catalog = new HearthstoneJsonCatalog(http, tmp.Path, TimeSpan.FromHours(24), utcNow: () => now);

        var result = await catalog.LoadAsync();

        Assert.Equal(1, handler.Calls);
        Assert.Equal(CatalogSource.Network, result.Source);
        Assert.Equal(7, result.Count);
        Assert.Equal("火球术", catalog.Get("CS2_029")?.Name);
    }

    [Fact]
    public async Task LoadAsync_FallsBackToStaleCache_WhenHttpFails()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "cards.zhCN.json"), ReadFixture());
        File.SetLastWriteTimeUtc(Path.Combine(tmp.Path, "cards.zhCN.json"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var now = new DateTimeOffset(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);
        var handler = new ScriptedHttpHandler((_, _) => throw new HttpRequestException("offline"));
        using var http = new HttpClient(handler);
        var catalog = new HearthstoneJsonCatalog(http, tmp.Path, TimeSpan.FromHours(24), utcNow: () => now);

        var result = await catalog.LoadAsync();

        Assert.Equal(CatalogSource.OfflineCache, result.Source);
        Assert.Equal(7, result.Count);
        Assert.Contains("离线缓存", result.StatusText, StringComparison.Ordinal);
        Assert.Equal("血沼迅猛龙", catalog.DisplayName("CS2_172"));
    }

    [Fact]
    public async Task LoadAsync_EmptyResult_WhenHttpFailsAndNoCache()
    {
        using var tmp = new TempDir();
        var handler = new ScriptedHttpHandler((_, _) => throw new HttpRequestException("offline"));
        using var http = new HttpClient(handler);
        var catalog = new HearthstoneJsonCatalog(http, tmp.Path);

        var result = await catalog.LoadAsync();

        Assert.Equal(CatalogSource.None, result.Source);
        Assert.Equal(0, result.Count);
        Assert.Equal(0, catalog.Count);
        Assert.Contains("失败", result.StatusText, StringComparison.Ordinal);
        Assert.Equal("CS2_029", catalog.DisplayName("CS2_029"));
    }

    [Fact]
    public void Formatter_ResolvesFixtureGameState_ToChineseNames()
    {
        var catalog = CatalogFromFixture();
        var formatter = new CardDisplayFormatter(catalog);
        var builder = new PowerLogGameStateBuilder();
        builder.IngestAll(File.ReadLines(FixturePath("opponent_reveal.log")));

        var hand = formatter.FormatHand(builder.State);
        var played = formatter.FormatPlayed(builder.State);
        var seen = formatter.FormatOpponentSeen(builder.State);

        Assert.Equal("火球术", Assert.Single(hand).DisplayName);
        Assert.Equal(4, hand[0].Cost);
        Assert.Equal("CS2_029", hand[0].CardId);
        Assert.Contains(hand[0].Tooltip, "CS2_029");

        Assert.Equal("奥妮克希亚", Assert.Single(played).DisplayName);
        Assert.Equal("奥妮克希亚", Assert.Single(seen).DisplayName);

        var unknown = formatter.Format("NOPE_99");
        Assert.Equal("NOPE_99", unknown.DisplayName);
        Assert.Equal("NOPE_99", unknown.Tooltip);

        var draw = builder.Events.First(e => e.CardId == "CS2_029");
        Assert.Contains("火球术", formatter.FormatEventSummary(draw), StringComparison.Ordinal);
        Assert.DoesNotContain("CS2_029", formatter.FormatEventSummary(draw), StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogStatus_UsesRequiredChinesePhrases()
    {
        Assert.Equal("卡表加载中", CatalogStatus.Loading);
        Assert.Equal("已加载 12 张", CatalogStatus.Loaded(12));
        Assert.Equal("离线缓存", CatalogStatus.OfflineCache);
        Assert.Equal("卡表加载中", new CatalogLoadResult(0, CatalogSource.None, null, null).StatusText);
    }

    internal static HearthstoneJsonCatalog CatalogFromFixture()
    {
        using var http = new HttpClient(new ScriptedHttpHandler(HttpStatusCode.OK, "[]"));
        var catalog = new HearthstoneJsonCatalog(http, cacheDirectory: Path.GetTempPath());
        catalog.ReplaceCards(HearthstoneJsonCatalog.ParseJson(ReadFixture()));
        return catalog;
    }

    internal static string ReadFixture() => File.ReadAllText(FixturePath("cards.zhCN.subset.json"));

    internal static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}

internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ahdt-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

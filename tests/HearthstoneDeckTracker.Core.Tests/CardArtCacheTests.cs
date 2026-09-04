using System.Net;
using System.Net.Http.Headers;
using HearthstoneDeckTracker.Core.Cards;

namespace HearthstoneDeckTracker.Core.Tests;

public class CardArtCacheTests
{
    [Fact]
    public async Task GetOrDownloadAsync_WritesFile_ThenServesFromDisk()
    {
        using var tmp = new TempDir();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
        var handler = new ScriptedHttpHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(png),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        });
        using var http = new HttpClient(handler);
        var cache = new CardArtCache(http, tmp.Path);

        var first = await cache.GetOrDownloadAsync("CS2_029");
        var second = await cache.GetOrDownloadAsync("CS2_029");

        Assert.Equal(1, handler.Calls);
        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(first, cache.TryGetCachedPath("CS2_029"));
        Assert.True(File.Exists(first));
        Assert.Equal(png, File.ReadAllBytes(first!));
        Assert.Equal(
            "https://art.hearthstonejson.com/v1/render/latest/zhCN/256x/CS2_029.png",
            handler.Requests[0]!.ToString());
    }

    [Fact]
    public async Task GetOrDownloadAsync_UnknownOrFailed_ReturnsNull()
    {
        using var tmp = new TempDir();
        var handler = new ScriptedHttpHandler(HttpStatusCode.NotFound, "missing");
        using var http = new HttpClient(handler);
        var cache = new CardArtCache(http, tmp.Path);

        Assert.Null(await cache.GetOrDownloadAsync("NOPE_99"));
        Assert.Null(await cache.GetOrDownloadAsync(null));
        Assert.Null(await cache.GetOrDownloadAsync(""));
        Assert.Null(cache.TryGetCachedPath("NOPE_99"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetOrDownloadAsync_HttpThrow_ReturnsNull_DoesNotCrash()
    {
        using var tmp = new TempDir();
        var handler = new ScriptedHttpHandler((_, _) => throw new HttpRequestException("offline"));
        using var http = new HttpClient(handler);
        var cache = new CardArtCache(http, tmp.Path);

        Assert.Null(await cache.GetOrDownloadAsync("CS2_029"));
        Assert.Null(cache.TryGetCachedPath("CS2_029"));
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidChars()
    {
        var sanitized = CardArtCache.SanitizeFileName("CS2/029");
        Assert.DoesNotContain('/', sanitized);
        Assert.DoesNotContain(System.IO.Path.DirectorySeparatorChar, sanitized);
    }
}

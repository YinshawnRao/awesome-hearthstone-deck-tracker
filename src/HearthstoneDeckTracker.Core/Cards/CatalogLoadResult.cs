namespace HearthstoneDeckTracker.Core.Cards;

public enum CatalogSource
{
    None = 0,
    Network,
    Cache,
    OfflineCache,
}

/// <summary>Outcome of <see cref="HearthstoneJsonCatalog.LoadAsync"/>.</summary>
public sealed record CatalogLoadResult(
    int Count,
    CatalogSource Source,
    DateTimeOffset? FetchedAt,
    string? Error)
{
    public bool HasCards => Count > 0;

    /// <summary>Status line for MainWindow: 卡表加载中 / 已加载 N 张 / 离线缓存.</summary>
    public string StatusText => Source switch
    {
        CatalogSource.None when Error is null => CatalogStatus.Loading,
        CatalogSource.OfflineCache => Count > 0
            ? $"{CatalogStatus.OfflineCache}（{Count} 张）"
            : CatalogStatus.OfflineCache,
        CatalogSource.Network or CatalogSource.Cache when Count > 0 => CatalogStatus.Loaded(Count),
        _ => Error is null ? CatalogStatus.Loading : $"卡表加载失败: {Error}",
    };
}

public static class CatalogStatus
{
    public const string Loading = "卡表加载中";
    public const string OfflineCache = "离线缓存";

    public static string Loaded(int count) => $"已加载 {count} 张";
}

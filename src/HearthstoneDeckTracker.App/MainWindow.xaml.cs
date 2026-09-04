using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HearthstoneDeckTracker.Core;
using HearthstoneDeckTracker.Core.Cards;
using HearthstoneDeckTracker.Core.Logging;
using HearthstoneDeckTracker.Core.State;

namespace HearthstoneDeckTracker.App;

public partial class MainWindow : Window
{
    private static readonly HttpClient SharedHttp = CreateHttpClient();

    private readonly LogConfigWriter _logConfigWriter = new();
    private readonly HearthstoneJsonCatalog _catalog = new(SharedHttp);
    private readonly CardArtCache _artCache = new(SharedHttp);
    private readonly CardDisplayFormatter _formatter;
    private readonly Dictionary<string, BitmapImage> _artBitmaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _artInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetime = new();

    private OverlayWindow? _overlay;
    private PowerLogGameStateBuilder _builder = new();
    private CancellationTokenSource? _tailCts;

    public MainWindow()
    {
        _formatter = new CardDisplayFormatter(_catalog, _artCache);
        InitializeComponent();
        LogConfigPathText.Text = _logConfigWriter.LogConfigPath;
        PowerLogPathText.Text = HearthstonePaths.FindExistingPowerLog()
            ?? HearthstonePaths.PowerLogPathPlaceholder;
        StatusText.Text = "Idle. Set a Power.log path and check Tail Power.log — missing files are reported, not crashed.";
        CatalogStatusText.Text = CatalogStatus.Loading;
        RefreshTrackerUi();
        Loaded += (_, _) => _ = LoadCatalogAsync();
        Closed += (_, _) =>
        {
            StopTailing(status: null);
            _lifetime.Cancel();
            _overlay?.Close();
        };
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AwesomeHearthstoneDeckTracker/1.0");
        return http;
    }

    private async Task LoadCatalogAsync()
    {
        CatalogStatusText.Text = CatalogStatus.Loading;
        try
        {
            var result = await _catalog.LoadAsync(_lifetime.Token).ConfigureAwait(true);
            CatalogStatusText.Text = result.StatusText;
            RefreshTrackerUi();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CatalogStatusText.Text = $"卡表加载失败: {ex.Message}";
        }
    }

    private void EnsureLogConfigButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _logConfigWriter.Ensure();
            LogConfigPathText.Text = _logConfigWriter.LogConfigPath;
            StatusText.Text =
                $"Updated {_logConfigWriter.LogConfigPath} with [Power] / [LoadingScreen]. Restart Hearthstone if it is already running.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not write log.config: {ex.Message}";
        }
    }

    private void ToggleOverlayButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_overlay is { IsVisible: true })
        {
            _overlay.Hide();
            ToggleOverlayButton.Content = "Show overlay stub";
            return;
        }

        _overlay ??= new OverlayWindow();
        _overlay.Show();
        ToggleOverlayButton.Content = "Hide overlay stub";
    }

    private async void TailPowerLogCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (TailPowerLogCheck.IsChecked != true)
        {
            StopTailing("Stopped tailing Power.log.");
            return;
        }

        if (_catalog.Count == 0)
            _ = LoadCatalogAsync();

        await StartTailingAsync();
    }

    private async Task StartTailingAsync()
    {
        var path = PowerLogPathText.Text.Trim().Trim('"');
        if (!HearthstonePaths.IsUsablePowerLogPath(path))
        {
            TailPowerLogCheck.IsChecked = false;
            StatusText.Text =
                "Set a real Power.log path before tailing. The placeholder is not a filesystem path, so nothing was started.";
            return;
        }

        StopTailing(status: null);
        _builder = new PowerLogGameStateBuilder();
        RefreshTrackerUi();

        if (!File.Exists(path))
        {
            StatusText.Text =
                $"Power.log not found at '{path}'. Waiting (no crash). Write log.config, restart Hearthstone, or point at a tests/.../Fixtures sample.";
        }
        else
        {
            StatusText.Text = $"Tailing '{path}' — existing lines are parsed, then new lines are followed.";
        }

        _tailCts = new CancellationTokenSource();
        var token = _tailCts.Token;
        var reader = new PowerLogTailReader(path, startAtEnd: false, pollInterval: TimeSpan.FromMilliseconds(250));

        try
        {
            await foreach (var line in reader.ReadLinesAsync(token).ConfigureAwait(true))
            {
                _builder.Ingest(line);
                RefreshTrackerUi();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Power.log tail stopped: {ex.Message}";
            TailPowerLogCheck.IsChecked = false;
        }
    }

    private void StopTailing(string? status)
    {
        if (_tailCts is null)
        {
            if (status is not null)
                StatusText.Text = status;
            return;
        }

        _tailCts.Cancel();
        _tailCts.Dispose();
        _tailCts = null;
        if (status is not null)
            StatusText.Text = status;
    }

    private void RefreshTrackerUi()
    {
        var state = _builder.State;
        var phase = state.IsInGame ? "in game" : "idle";
        DeckSummaryText.Text =
            $"Deck remaining {state.FriendlyDeck.TotalCards}  ·  hand {state.FriendlyHand.Count}  ·  played {state.CardsPlayed.Count}  ·  opponent seen {state.OpponentSeen.TotalCards}  ·  {phase} T{state.Turn}";

        BindCardList(DeckList, _formatter.FormatDeckRemaining(state));
        BindCardList(HandList, _formatter.FormatHand(state));
        BindCardList(PlayedList, _formatter.FormatPlayed(state));
        BindCardList(OpponentList, _formatter.FormatOpponentSeen(state));

        foreach (var cardId in _formatter.VisibleCardIds(state).Distinct(StringComparer.OrdinalIgnoreCase))
            _ = EnsureArtAsync(cardId);

        EventList.Items.Clear();
        foreach (var ev in _builder.Events)
        {
            var item = new ListBoxItem
            {
                Content = _formatter.FormatEventSummary(ev),
                ToolTip = string.IsNullOrWhiteSpace(ev.CardId) ? ev.Summary : ev.CardId,
            };
            EventList.Items.Add(item);
        }

        if (EventList.Items.Count > 0)
            EventList.ScrollIntoView(EventList.Items[^1]);
    }

    private void BindCardList(ListBox list, IReadOnlyList<CardDisplayRow> rows)
    {
        var items = new List<CardRow>(rows.Count);
        foreach (var row in rows)
        {
            var item = new CardRow
            {
                CardId = row.CardId,
                DisplayName = row.DisplayName,
                CostText = row.CostText,
                CountText = row.CountText,
                Tooltip = row.Tooltip,
            };
            if (!string.IsNullOrEmpty(row.CardId) && _artBitmaps.TryGetValue(row.CardId, out var bmp))
                item.Thumbnail = bmp;
            items.Add(item);
        }

        list.ItemsSource = items;
        foreach (var item in items)
        {
            if (item.Thumbnail is null && !string.IsNullOrEmpty(item.CardId))
                _ = EnsureArtAsync(item.CardId);
        }
    }

    private async Task EnsureArtAsync(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return;
        if (_artBitmaps.ContainsKey(cardId))
            return;
        if (!_artInFlight.Add(cardId))
            return;

        try
        {
            var path = await _artCache.GetOrDownloadAsync(cardId, _lifetime.Token).ConfigureAwait(true);
            if (path is null)
                return;
            var bitmap = LoadBitmap(path);
            _artBitmaps[cardId] = bitmap;
            ApplyThumbnail(DeckList, cardId, bitmap);
            ApplyThumbnail(HandList, cardId, bitmap);
            ApplyThumbnail(PlayedList, cardId, bitmap);
            ApplyThumbnail(OpponentList, cardId, bitmap);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // Missing art must not take down the tracker window.
        }
        finally
        {
            _artInFlight.Remove(cardId);
        }
    }

    private static void ApplyThumbnail(ListBox list, string cardId, BitmapImage bitmap)
    {
        if (list.ItemsSource is not IEnumerable<CardRow> rows)
            return;
        foreach (var row in rows)
        {
            if (string.Equals(row.CardId, cardId, StringComparison.OrdinalIgnoreCase))
                row.Thumbnail = bitmap;
        }
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        using var stream = File.OpenRead(path);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}

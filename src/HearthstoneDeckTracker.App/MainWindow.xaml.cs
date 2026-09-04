using System.IO;
using System.Windows;
using HearthstoneDeckTracker.Core;
using HearthstoneDeckTracker.Core.Logging;

namespace HearthstoneDeckTracker.App;

public partial class MainWindow : Window
{
    private readonly LogConfigWriter _logConfigWriter = new();
    private OverlayWindow? _overlay;
    private PowerLogGameStateBuilder _builder = new();
    private CancellationTokenSource? _tailCts;

    public MainWindow()
    {
        InitializeComponent();
        LogConfigPathText.Text = _logConfigWriter.LogConfigPath;
        PowerLogPathText.Text = HearthstonePaths.FindExistingPowerLog()
            ?? HearthstonePaths.PowerLogPathPlaceholder;
        StatusText.Text = "Idle. Set a Power.log path and check Tail Power.log — missing files are reported, not crashed.";
        RefreshTrackerUi();
        Closed += (_, _) =>
        {
            StopTailing(status: null);
            _overlay?.Close();
        };
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

        EventList.Items.Clear();
        foreach (var ev in _builder.Events)
            EventList.Items.Add(ev.Summary);

        if (EventList.Items.Count > 0)
            EventList.ScrollIntoView(EventList.Items[^1]);
    }
}

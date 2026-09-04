using System.Windows;
using HearthstoneDeckTracker.Core;
using HearthstoneDeckTracker.Core.Logging;

namespace HearthstoneDeckTracker.App;

public partial class MainWindow : Window
{
    private readonly LogConfigWriter _logConfigWriter = new();
    private OverlayWindow? _overlay;

    public MainWindow()
    {
        InitializeComponent();
        LogConfigPathText.Text = _logConfigWriter.LogConfigPath;
        PowerLogPathText.Text = HearthstonePaths.PowerLogPathPlaceholder;
        StatusText.Text = "Idle — skeleton. Overlay and ingest are stubs; nothing is fully tracking yet.";
        Closed += (_, _) => _overlay?.Close();
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
}

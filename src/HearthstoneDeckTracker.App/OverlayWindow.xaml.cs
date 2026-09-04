using System.Windows;

namespace HearthstoneDeckTracker.App;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // HWND is available here (WindowInteropHelper.Handle).
        // TODO: Set WS_EX_TRANSPARENT so clicks pass through to Hearthstone.
        // TODO: Set WS_EX_NOACTIVATE so the overlay does not activate / steal focus.
        // Do not implement injection or input spoofing — style bits only on our own window.
    }
}

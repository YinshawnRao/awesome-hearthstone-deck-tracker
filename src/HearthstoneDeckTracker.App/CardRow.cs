using System.ComponentModel;
using System.Windows.Media;

namespace HearthstoneDeckTracker.App;

/// <summary>One row in the deck / hand / played / opponent-seen lists.</summary>
public sealed class CardRow : INotifyPropertyChanged
{
    public required string CardId { get; init; }

    public required string DisplayName { get; init; }

    public required string CostText { get; init; }

    public required string CountText { get; init; }

    public required string Tooltip { get; init; }

    private ImageSource? _thumbnail;

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value))
                return;
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

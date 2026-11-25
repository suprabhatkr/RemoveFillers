using Microsoft.UI.Xaml.Controls;
using Windows.Media.Core;

namespace RemoveFillers.Controls;

public sealed partial class PreviewView : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? ForwardRequested;
    public event EventHandler? PlayRequested;

    public PreviewView()
    {
        InitializeComponent();
        BackButton.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        ForwardButton.Click += (_, _) => ForwardRequested?.Invoke(this, EventArgs.Empty);
        PlayButton.Click += (_, _) => PlayRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetSource(MediaSource? source) => PreviewPlayer.Source = source;

    public void Update(string title, string subtitle, string time, bool canPlay, bool playing)
    {
        PreviewTitle.Text = title;
        PreviewSubtitle.Text = subtitle;
        TimeText.Text = time;
        BackButton.IsEnabled = canPlay;
        ForwardButton.IsEnabled = canPlay;
        PlayButton.IsEnabled = canPlay;
        PlayIcon.Glyph = playing ? "\uE769" : "\uE768";
    }
}

using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using RemoveFillers.Domain;

namespace RemoveFillers.Controls;

public sealed partial class MediaLibraryView : UserControl
{
    public event EventHandler? ChooseFileRequested;
    public event EventHandler? AddClipRequested;
    public event EventHandler? TranscribeRequested;
    public event EventHandler? SelectionChanged;

    public MediaLibraryView()
    {
        InitializeComponent();
        ChooseFileButton.Click += (_, _) => ChooseFileRequested?.Invoke(this, EventArgs.Empty);
        AddClipButton.Click += (_, _) => AddClipRequested?.Invoke(this, EventArgs.Empty);
        TranscribeButton.Click += (_, _) => TranscribeRequested?.Invoke(this, EventArgs.Empty);
        MediaList.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public MediaAsset? SelectedAsset
    {
        get => MediaList.SelectedItem as MediaAsset;
        set => MediaList.SelectedItem = value;
    }

    public bool CanChooseFile { set => ChooseFileButton.IsEnabled = value; }
    public bool CanAddClip { set => AddClipButton.IsEnabled = value; }
    public bool CanTranscribe { set => TranscribeButton.IsEnabled = value; }
    public string Message { set => SelectedFileText.Text = value; }

    public void SetItems(ObservableCollection<MediaAsset> items)
    {
        MediaList.ItemsSource = items;
        MediaList.SelectedIndex = items.Count > 0 ? 0 : -1;
    }
}

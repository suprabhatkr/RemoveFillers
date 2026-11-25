using Microsoft.UI.Xaml.Controls;
using RemoveFillers.Domain;

namespace RemoveFillers.Controls;

public sealed partial class TimelineView : UserControl
{
    private bool updating;
    private EditorClip[] displayedClips = [];

    public event EventHandler? SelectionChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler? UndoRequested;
    public event EventHandler? RedoRequested;
    public event EventHandler? SplitRequested;
    public event EventHandler? DeleteRequested;

    public TimelineView()
    {
        InitializeComponent();
        TimelineList.SelectionChanged += (_, _) => { if (!updating) SelectionChanged?.Invoke(this, EventArgs.Empty); };
        PositionSlider.ValueChanged += (_, _) => { if (!updating) PositionChanged?.Invoke(this, EventArgs.Empty); };
        UndoButton.Click += (_, _) => UndoRequested?.Invoke(this, EventArgs.Empty);
        RedoButton.Click += (_, _) => RedoRequested?.Invoke(this, EventArgs.Empty);
        SplitButton.Click += (_, _) => SplitRequested?.Invoke(this, EventArgs.Empty);
        DeleteButton.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    public EditorClip? SelectedClip => TimelineList.SelectedItem as EditorClip;
    public int Position => (int)PositionSlider.Value;
    public string Message { set => StatusText.Text = value; }

    public void Update(IReadOnlyList<EditorClip> clips, EditorClip? selected, int position, int duration,
        bool canUndo, bool canRedo, bool canSplit)
    {
        updating = true;
        try
        {
            if (!displayedClips.SequenceEqual(clips))
            {
                displayedClips = clips.ToArray();
                TimelineList.ItemsSource = displayedClips;
            }
            TimelineList.SelectedItem = selected;
            UndoButton.IsEnabled = canUndo;
            RedoButton.IsEnabled = canRedo;
            DeleteButton.IsEnabled = selected is not null;
            SplitButton.IsEnabled = canSplit;
            PositionSlider.Maximum = Math.Max(1, duration);
            PositionSlider.Value = position;
            PositionSlider.IsEnabled = duration > 0;
        }
        finally { updating = false; }
    }
}

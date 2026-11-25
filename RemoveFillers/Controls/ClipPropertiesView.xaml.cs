using Microsoft.UI.Xaml.Controls;
using RemoveFillers.Domain;

namespace RemoveFillers.Controls;

public sealed partial class ClipPropertiesView : UserControl
{
    private bool updating;
    public event EventHandler? AdjustmentChanged;

    public ClipPropertiesView()
    {
        InitializeComponent();
        VolumeSlider.ValueChanged += (_, _) => NotifyChanged();
        OpacitySlider.ValueChanged += (_, _) => NotifyChanged();
    }

    public int Volume => (int)VolumeSlider.Value;
    public int ClipOpacity => (int)OpacitySlider.Value;

    public void Update(EditorClip? selected)
    {
        updating = true;
        try
        {
            VolumeSlider.IsEnabled = selected is not null;
            OpacitySlider.IsEnabled = selected is not null;
            VolumeSlider.Value = selected?.Volume ?? 100;
            OpacitySlider.Value = selected?.Opacity ?? 100;
            VolumeText.Text = $"Volume: {VolumeSlider.Value:0}%";
            OpacityText.Text = $"Opacity: {OpacitySlider.Value:0}%";
            SelectionTitle.Text = selected?.Name ?? "Nothing selected";
            SelectionDetails.Text = selected is null ? "Select a clip on the timeline to see its settings." : $"{selected.Kind} clip | {selected.DurationSeconds} seconds";
        }
        finally { updating = false; }
    }

    private void NotifyChanged()
    {
        if (!updating) AdjustmentChanged?.Invoke(this, EventArgs.Empty);
    }
}

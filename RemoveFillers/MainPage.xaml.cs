using Azure.Identity;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RemoveFillers.UseCases;
using RemoveFillers.Domain;
using RemoveFillers.Infrastructure;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Storage.Pickers;
using Windows.Media.Core;
using WinRT.Interop;

namespace RemoveFillers;

public sealed partial class MainPage : Page
{
    private readonly EditorSession session = new();
    private readonly ImportedMediaCatalog importedMedia = new();
    private readonly ObservableCollection<MediaAsset> mediaLibrary = new(SampleMediaCatalog.Create());
    private static readonly HttpClient transcriptionHttpClient = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(5) };
    private readonly DispatcherTimer playbackTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Guid? selectedId;
    private int position;
    private bool updating;
    private bool transcribing;

    public MainPage()
    {
        InitializeComponent();

        Library.SetItems(mediaLibrary);
        Library.ChooseFileRequested += ChooseFile_Click;
        Library.SelectionChanged += (_, _) => Refresh();
        Library.AddClipRequested += (_, _) => AddClip();
        Library.TranscribeRequested += Transcribe_Click;
        Timeline.SelectionChanged += (_, _) => SelectClip();
        Timeline.UndoRequested += (_, _) => { session.Undo(); Refresh(); };
        Timeline.RedoRequested += (_, _) => { session.Redo(); Refresh(); };
        Timeline.DeleteRequested += (_, _) => DeleteClip();
        Timeline.SplitRequested += (_, _) => SplitClip();
        Timeline.PositionChanged += (_, _) => Seek(Timeline.Position);
        Preview.BackRequested += (_, _) => Seek(position - 5);
        Preview.ForwardRequested += (_, _) => Seek((int)Math.Min((long)position + 5, session.DurationSeconds));
        Preview.PlayRequested += (_, _) => TogglePlayback();
        Properties.AdjustmentChanged += (_, _) => AdjustClip();
        playbackTimer.Tick += (_, _) => AdvancePlayback();
        Unloaded += (_, _) => playbackTimer.Stop();
        Refresh();
    }

    private async void ChooseFile_Click(object? sender, EventArgs e)
    {
        if (((App)Microsoft.UI.Xaml.Application.Current).MainWindow is not Window window)
        {
            Library.Message = "Cannot open the file picker: the application window is unavailable.";
            return;
        }

        Library.CanChooseFile = false;
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                MediaAsset asset = await importedMedia.ImportAsync(file);
                mediaLibrary.Add(asset);
                Library.SelectedAsset = asset;
                Library.Message = $"Added to library: {file.Name}. Select Add to timeline to use it.";
            }
        }
        catch (COMException ex)
        {
            Library.Message = $"Could not select or read the media file: {ex.Message}";
        }
        catch (InvalidDataException ex)
        {
            Library.Message = $"Cannot import this media: {ex.Message}";
        }
        catch (IOException ex)
        {
            Library.Message = $"Could not read the media file: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            Library.Message = $"Cannot access the media file: {ex.Message}";
        }
        finally
        {
            Library.CanChooseFile = true;
        }
    }

    private async void Transcribe_Click(object? sender, EventArgs e)
    {
        if (transcribing || Library.SelectedAsset is not MediaAsset asset ||
            !importedMedia.TryGetFile(asset.Id, out var file) || file is null)
        {
            Library.Message = "Select an imported media file to transcribe.";
            return;
        }

        transcribing = true;
        Refresh();
        try
        {
            var service = new AzureTranscriptionService(transcriptionHttpClient, new AzureCliCredential(),
                Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"),
                Environment.GetEnvironmentVariable("AZURE_OPENAI_TRANSCRIPTION_DEPLOYMENT"));
            var confirmation = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Send media to Azure?",
                Content = $"The entire original file '{file.Name}' will be uploaded to your configured Azure OpenAI resource for transcription. This may incur charges. Timeline edits are not applied.",
                PrimaryButtonText = "Send and transcribe",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;

            Library.Message = $"Transcribing {file.Name} with Azure...";
            await using var stream = await file.OpenStreamForReadAsync();
            string transcript = await service.TranscribeAsync(stream, file.Name);
            Library.Message = $"Transcription completed: {file.Name}.";
            var result = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = $"Transcript: {file.Name}",
                Content = new TextBox
                {
                    Text = transcript,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinWidth = 350,
                    MaxHeight = 400
                },
                CloseButtonText = "Close"
            };
            await result.ShowAsync();
        }
        catch (AuthenticationFailedException ex)
        {
            Library.Message = $"Azure sign-in failed. Sign in with Azure CLI and check your permissions. {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            Library.Message = $"Transcription request failed: {ex.Message}";
        }
        catch (OperationCanceledException)
        {
            Library.Message = "The transcription request timed out or was cancelled.";
        }
        catch (InvalidOperationException ex)
        {
            Library.Message = $"Cannot transcribe: {ex.Message}";
        }
        catch (InvalidDataException ex)
        {
            Library.Message = $"Cannot transcribe: {ex.Message}";
        }
        catch (IOException ex)
        {
            Library.Message = $"Cannot read media or transcription response: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            Library.Message = $"Cannot access the media file: {ex.Message}";
        }
        catch (COMException ex)
        {
            Library.Message = $"Cannot read the media file: {ex.Message}";
        }
        catch (JsonException)
        {
            Library.Message = "Azure returned an invalid transcription response.";
        }
        finally
        {
            transcribing = false;
            Refresh();
        }
    }

    private void AddClip()
    {
        if (Library.SelectedAsset is not MediaAsset asset) return;
        if (asset.DurationSeconds > int.MaxValue - session.DurationSeconds)
        {
            Timeline.Message = "This clip would exceed the maximum supported timeline length.";
            return;
        }
        session.Add(asset);
        EditorClip added = session.Clips[^1];
        selectedId = added.Id;
        position = session.StartOf(added.Id);
        Timeline.Message = "Clip added. Select it and move the playhead to edit it.";
        if (importedMedia.TryGetFile(added.MediaAssetId, out var file) && file is not null)
        {
            Preview.SetSource(MediaSource.CreateFromStorageFile(file));
        }
        else
        {
            Preview.SetSource(null);
            Timeline.Message = "This sample has no video or audio file to play.";
        }
        Refresh();
    }

    private void SelectClip()
    {
        if (updating) return;
        selectedId = Timeline.SelectedClip?.Id;
        if (selectedId is Guid id) position = session.StartOf(id);
        Refresh();
    }

    private void DeleteClip()
    {
        if (selectedId is not Guid id || !session.Delete(id)) return;
        selectedId = null;
        Timeline.Message = "Clip removed.";
        Refresh();
    }

    private void SplitClip()
    {
        if (selectedId is not Guid id || !session.Split(id, position)) return;
        Timeline.Message = "Clip split at the playhead.";
        Refresh();
    }

    private void AdjustClip()
    {
        if (updating || selectedId is not Guid id) return;
        if (session.Adjust(id, Properties.Volume, Properties.ClipOpacity))
        {
            Timeline.Message = "Clip properties updated; no media is changed.";
            Refresh();
        }
    }

    private void Seek(int seconds)
    {
        position = Math.Clamp(seconds, 0, session.DurationSeconds);
        Refresh();
    }

    private void TogglePlayback()
    {
        if (session.DurationSeconds == 0) return;
        if (playbackTimer.IsEnabled)
        {
            playbackTimer.Stop();
        }
        else
        {
            if (position >= session.DurationSeconds) position = 0;
            playbackTimer.Start();
        }
        Refresh();
    }

    private void AdvancePlayback()
    {
        if (position < session.DurationSeconds) position++;
        if (position >= session.DurationSeconds) playbackTimer.Stop();
        Refresh();
    }

    private void Refresh()
    {
        updating = true;
        try
        {
            position = Math.Clamp(position, 0, session.DurationSeconds);
            if (session.DurationSeconds == 0) playbackTimer.Stop();
            EditorClip? selected = session.Clips.FirstOrDefault(clip => clip.Id == selectedId);
            if (selected is null) selectedId = null;

            Library.CanAddClip = Library.SelectedAsset is not null;
            Library.CanTranscribe = !transcribing && Library.SelectedAsset is MediaAsset asset && importedMedia.TryGetFile(asset.Id, out _);
            int start = selected is null ? 0 : session.StartOf(selected.Id);
            bool canSplit = selected is not null && position > start && position < start + selected.DurationSeconds;
            Timeline.Update(session.Clips, selected, position, session.DurationSeconds, session.CanUndo, session.CanRedo, canSplit);
            Properties.Update(selected);
            EditorClip? current = session.ClipAt(position);
            Preview.Update(
                current?.Name ?? (session.DurationSeconds > 0 ? "End of timeline" : "Your story starts here"),
                current is null ? "Add or play media to begin" : $"{current.Kind} simulation - no real media playback",
                $"{FormatTime(position)} / {FormatTime(session.DurationSeconds)}",
                session.DurationSeconds > 0, playbackTimer.IsEnabled);
        }
        finally
        {
            updating = false;
        }
    }

    private static string FormatTime(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";
}

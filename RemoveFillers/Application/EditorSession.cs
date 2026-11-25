using RemoveFillers.Domain;

namespace RemoveFillers.UseCases;

// Owns the in-memory editing rules; it has no dependency on the UI or file system.
public sealed class EditorSession
{
    private readonly List<EditorClip> clips = [];
    private readonly Stack<EditorClip[]> undo = [];
    private readonly Stack<EditorClip[]> redo = [];

    public IReadOnlyList<EditorClip> Clips => clips.AsReadOnly();
    public int DurationSeconds => clips.Sum(clip => clip.DurationSeconds);
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    public void Add(MediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.DurationSeconds <= 0 || asset.DurationSeconds > int.MaxValue - DurationSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(asset), "The clip duration exceeds the timeline's supported length.");
        }
        SaveChange();
        clips.Add(new EditorClip(Guid.NewGuid(), asset.Id, asset.Name, asset.Kind, asset.DurationSeconds, 100, 100));
    }

    public bool Delete(Guid id)
    {
        int index = clips.FindIndex(clip => clip.Id == id);
        if (index < 0) return false;
        SaveChange();
        clips.RemoveAt(index);
        return true;
    }

    public bool Split(Guid id, int positionSeconds)
    {
        int start = 0;
        for (int i = 0; i < clips.Count; i++)
        {
            EditorClip clip = clips[i];
            if (clip.Id == id)
            {
                int offset = positionSeconds - start;
                if (offset <= 0 || offset >= clip.DurationSeconds) return false;
                SaveChange();
                clips[i] = clip with { DurationSeconds = offset };
                clips.Insert(i + 1, clip with { Id = Guid.NewGuid(), DurationSeconds = clip.DurationSeconds - offset });
                return true;
            }
            start += clip.DurationSeconds;
        }
        return false;
    }

    public bool Adjust(Guid id, int volume, int opacity)
    {
        int index = clips.FindIndex(clip => clip.Id == id);
        if (index < 0 || volume is < 0 or > 100 || opacity is < 0 or > 100) return false;
        EditorClip clip = clips[index];
        if (clip.Volume == volume && clip.Opacity == opacity) return false;
        SaveChange();
        clips[index] = clip with { Volume = volume, Opacity = opacity };
        return true;
    }

    public void Undo()
    {
        if (undo.Count == 0) return;
        redo.Push(clips.ToArray());
        Restore(undo.Pop());
    }

    public void Redo()
    {
        if (redo.Count == 0) return;
        undo.Push(clips.ToArray());
        Restore(redo.Pop());
    }

    public int StartOf(Guid id)
    {
        int start = 0;
        foreach (EditorClip clip in clips)
        {
            if (clip.Id == id) return start;
            start += clip.DurationSeconds;
        }
        return 0;
    }

    public EditorClip? ClipAt(int positionSeconds)
    {
        if (positionSeconds < 0 || positionSeconds >= DurationSeconds) return null;
        int start = 0;
        foreach (EditorClip clip in clips)
        {
            start += clip.DurationSeconds;
            if (positionSeconds < start) return clip;
        }
        return null;
    }

    private void SaveChange()
    {
        undo.Push(clips.ToArray());
        redo.Clear();
    }

    private void Restore(EditorClip[] snapshot)
    {
        clips.Clear();
        clips.AddRange(snapshot);
    }
}

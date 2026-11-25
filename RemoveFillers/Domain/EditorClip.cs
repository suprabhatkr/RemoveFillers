namespace RemoveFillers.Domain;

public enum MediaKind
{
    Video,
    Audio
}

public sealed record MediaAsset(Guid Id, string Name, MediaKind Kind, int DurationSeconds);

public sealed record EditorClip(Guid Id, Guid MediaAssetId, string Name, MediaKind Kind, int DurationSeconds, int Volume, int Opacity)
{
    public string DisplayName => $"{Name}  |  {DurationSeconds}s";
}

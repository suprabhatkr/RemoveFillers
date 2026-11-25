using RemoveFillers.Domain;

namespace RemoveFillers.Infrastructure;

public static class SampleMediaCatalog
{
    public static IReadOnlyList<MediaAsset> Create() =>
    new MediaAsset[]
    {
        new(Guid.NewGuid(), "Mountain sunrise (sample)", MediaKind.Video, 12),
        new(Guid.NewGuid(), "City lights (sample)", MediaKind.Video, 8),
        new(Guid.NewGuid(), "Background music (sample)", MediaKind.Audio, 10)
    };
}

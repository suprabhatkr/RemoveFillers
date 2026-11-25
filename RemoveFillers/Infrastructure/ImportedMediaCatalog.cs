using RemoveFillers.Domain;
using Windows.Storage;

namespace RemoveFillers.Infrastructure;

public sealed class ImportedMediaCatalog
{
    private readonly Dictionary<Guid, StorageFile> files = [];

    public async Task<MediaAsset> ImportAsync(StorageFile file)
    {
        MediaKind kind = file.FileType.ToLowerInvariant() switch
        {
            ".mp4" or ".mov" => MediaKind.Video,
            ".mp3" or ".wav" => MediaKind.Audio,
            _ => throw new InvalidDataException("This file type is not supported.")
        };

        TimeSpan duration = kind == MediaKind.Video
            ? (await file.Properties.GetVideoPropertiesAsync()).Duration
            : (await file.Properties.GetMusicPropertiesAsync()).Duration;

        if (duration <= TimeSpan.Zero || duration.TotalSeconds > int.MaxValue)
        {
            throw new InvalidDataException("The file does not have a supported media duration.");
        }

        var asset = new MediaAsset(Guid.NewGuid(), file.Name, kind, (int)Math.Ceiling(duration.TotalSeconds));
        files.Add(asset.Id, file);
        return asset;
    }

    public bool TryGetFile(Guid assetId, out StorageFile? file) => files.TryGetValue(assetId, out file);
}

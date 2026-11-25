using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;

namespace RemoveFillers.Infrastructure;

public sealed class AzureTranscriptionService
{
    public const long MaxFileBytes = 25_000_000;
    private static readonly TokenRequestContext TokenContext = new(["https://cognitiveservices.azure.com/.default"]);
    private readonly HttpClient client;
    private readonly TokenCredential credential;
    private readonly Uri requestUri;
    private readonly string deployment;

    public AzureTranscriptionService(HttpClient client, TokenCredential credential, string? endpoint, string? deployment)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !uri.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT to your Azure OpenAI resource's HTTPS endpoint, for example https://your-resource.openai.azure.com/.");
        }
        if (string.IsNullOrWhiteSpace(deployment))
        {
            throw new InvalidOperationException("Set AZURE_OPENAI_TRANSCRIPTION_DEPLOYMENT to your transcription model deployment name.");
        }
        this.client = client;
        this.credential = credential;
        this.deployment = deployment.Trim();
        requestUri = new Uri(uri, "openai/v1/audio/transcriptions");
    }

    public async Task<string> TranscribeAsync(Stream file, string fileName, CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".mp4" or ".mp3" or ".wav"))
            throw new InvalidDataException("Transcription supports MP4, MP3 and WAV files. Convert MOV to MP4 first.");
        if (!file.CanSeek || file.Length <= 0 || file.Length > MaxFileBytes)
            throw new InvalidDataException("Select a non-empty media file no larger than 25 MB.");

        AccessToken token = await credential.GetTokenAsync(TokenContext, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(deployment), "model");
        using var streamContent = new StreamContent(file);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(extension switch
        {
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            _ => "audio/wav"
        });
        form.Add(streamContent, "file", Path.GetFileName(fileName));
        request.Content = form;
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Azure transcription failed (HTTP {(int)response.StatusCode}). Check the deployment, permissions, file format and service availability.", null, response.StatusCode);

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument result = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        if (!result.RootElement.TryGetProperty("text", out JsonElement text) || text.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Azure returned a transcription response without a text field.");
        return text.GetString()!;
    }
}

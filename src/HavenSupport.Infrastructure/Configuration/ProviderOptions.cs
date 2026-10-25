namespace HavenSupport.Infrastructure.Configuration;

public sealed class ProviderOptions
{
    public const string SectionName = "Providers";
    public string OllamaBaseUrl { get; set; } = "http://127.0.0.1:11434";
    public string ChatModel { get; set; } = "llama3.2:3b";
    public string EmbeddingModel { get; set; } = "all-minilm:22m";
    public string QdrantHost { get; set; } = "127.0.0.1";
    public int QdrantGrpcPort { get; set; } = 6334;

    public bool IsValid() =>
        Uri.TryCreate(OllamaBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && !string.IsNullOrWhiteSpace(ChatModel)
        && !string.IsNullOrWhiteSpace(EmbeddingModel)
        && !string.IsNullOrWhiteSpace(QdrantHost)
        && QdrantGrpcPort is > 0 and <= 65535;
}

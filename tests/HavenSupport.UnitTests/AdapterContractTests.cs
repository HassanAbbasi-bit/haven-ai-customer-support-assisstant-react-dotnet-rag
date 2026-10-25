using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OllamaSharp;
namespace HavenSupport.UnitTests;

public sealed class AdapterContractTests
{
    [Fact]
    public async Task EmbeddingAdapterMustDisableSilentTruncation()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434") };
        using IEmbeddingGenerator<string, Embedding<float>> generator = new OllamaApiClient(http) { SelectedModel = "all-minilm:22m" };
        await generator.GenerateAsync(["Synthetic compatibility check"], new EmbeddingGenerationOptions { AdditionalProperties = new() { ["truncate"] = false } });
        Assert.NotNull(handler.Body);
        using var body = JsonDocument.Parse(handler.Body);
        Assert.True(body.RootElement.TryGetProperty("truncate", out var truncate), "The adapter omitted truncate=false; long inputs could be silently shortened.");
        Assert.False(truncate.GetBoolean());
    }
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Body = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"model\":\"all-minilm:22m\",\"embeddings\":[[0.1,0.2,0.3]],\"total_duration\":1,\"load_duration\":1,\"prompt_eval_count\":3}", Encoding.UTF8, "application/json") };
        }
    }
}

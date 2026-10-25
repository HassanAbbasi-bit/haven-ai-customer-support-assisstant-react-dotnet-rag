using System.Net.Sockets;
using System.Text.Json;
using HavenSupport.Infrastructure.Configuration;
using HavenSupport.Infrastructure.Diagnostics;
using Microsoft.Data.SqlClient;

if (args.Length == 1 && args[0] == "--export-demo-pdfs") { HavenSupport.Evaluation.FixturePdfExporter.Run(); return 0; }

// Provider checks only. The 30-case RAG evaluation is not implemented in M0.
var allowed = new[] { "all", "pdf", "sql", "qdrant", "ollama-chat", "ollama-embeddings" };
if (args.Length != 2 || args[0] != "--providers" || !allowed.Contains(args[1]))
{
    Console.Error.WriteLine("Usage: --providers all|pdf|sql|qdrant|ollama-chat|ollama-embeddings");
    return 64;
}
var options = new ProviderOptions
{
    OllamaBaseUrl = Environment.GetEnvironmentVariable("Providers__OllamaBaseUrl") ?? "http://127.0.0.1:11434",
    ChatModel = Environment.GetEnvironmentVariable("Providers__ChatModel") ?? "llama3.2:3b",
    EmbeddingModel = Environment.GetEnvironmentVariable("Providers__EmbeddingModel") ?? "all-minilm:22m",
    QdrantHost = Environment.GetEnvironmentVariable("Providers__QdrantHost") ?? "127.0.0.1",
    QdrantGrpcPort = int.TryParse(Environment.GetEnvironmentVariable("Providers__QdrantGrpcPort"), out var port) ? port : 6334
};
if (!options.IsValid()) { Console.Error.WriteLine("Invalid provider settings."); return 64; }
var results = new List<ProbeResult>();
foreach (var name in allowed.Skip(1).Where(name => args[1] == "all" || args[1] == name))
{
    try
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        if (name == "pdf") results.Add(new(name, "passed", PdfProbe.Run()));
        else if (name == "sql")
        {
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer");
            if (string.IsNullOrWhiteSpace(connection)) throw new ProbeBlockedException("Set ConnectionStrings__SqlServer; no SQL secret is configured.");
            try
            {
                var settings = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master", ConnectTimeout = 5 };
                await using var sql = new SqlConnection(settings.ConnectionString);
                await sql.OpenAsync(deadline.Token);
            }
            catch (SqlException) { throw new ProbeBlockedException("SQL connection/authentication unavailable; verify service and secret."); }
            results.Add(new(name, "passed", await ProviderProbes.SqlAsync(connection, deadline.Token)));
        }
        else if (name == "qdrant")
        {
            await RequirePortAsync(options.QdrantHost, options.QdrantGrpcPort);
            results.Add(new(name, "passed", await ProviderProbes.QdrantAsync(options, deadline.Token)));
        }
        else
        {
            using var http = new HttpClient { BaseAddress = new Uri(options.OllamaBaseUrl), Timeout = TimeSpan.FromSeconds(10) };
            JsonDocument tags;
            try { tags = JsonDocument.Parse(await http.GetStringAsync("/api/tags", deadline.Token)); }
            catch (HttpRequestException) { throw new ProbeBlockedException("Ollama is unreachable at the configured URL."); }
            using (tags)
            {
                var model = name == "ollama-chat" ? options.ChatModel : options.EmbeddingModel;
                if (!tags.RootElement.GetProperty("models").EnumerateArray().Any(item => item.GetProperty("name").GetString() == model))
                    throw new ProbeBlockedException($"Model not installed: {model}. Models are never pulled automatically.");
            }
            var detail = name == "ollama-chat"
                ? await ProviderProbes.ChatAsync(options, deadline.Token)
                : await ProviderProbes.EmbeddingsAsync(options, deadline.Token);
            results.Add(new(name, "passed", detail));
        }
    }
    catch (ProbeBlockedException error) { results.Add(new(name, "blocked", error.Message)); }
    catch (Exception error)
    {
        // Exception messages may contain secrets. Inspect locally in a debugger.
        results.Add(new(name, "failed", $"{error.GetType().Name}: provider check did not pass."));
    }
    Console.WriteLine($"{results[^1].Name}: {results[^1].Status} — {results[^1].Detail}");
}
Directory.CreateDirectory(".local");
await File.WriteAllTextAsync($".local/m0-provider-{args[1]}.json", JsonSerializer.Serialize(
    new { executedAtUtc = DateTimeOffset.UtcNow, scope = "M0 providers only", results }, new JsonSerializerOptions { WriteIndented = true }));
return results.Any(result => result.Status == "failed") ? 1 : results.Any(result => result.Status == "blocked") ? 2 : 0;

static async Task RequirePortAsync(string host, int port)
{
    using var socket = new TcpClient();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    try { await socket.ConnectAsync(host, port, timeout.Token); }
    catch (Exception error) when (error is SocketException or OperationCanceledException)
    {
        throw new ProbeBlockedException("Qdrant gRPC is unreachable; start the supporting service.");
    }
}

internal sealed record ProbeResult(string Name, string Status, string Detail);
internal sealed class ProbeBlockedException(string message) : Exception(message);

using System.Text.Json;
using HavenSupport.Infrastructure.Configuration;
using HavenSupport.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace HavenSupport.Infrastructure.Diagnostics;

// Developer-only probes; never registered as public API routes.
public static class ProviderProbes
{
    public static async Task<string> SqlAsync(string connection, CancellationToken token)
    {
        var settings = new SqlConnectionStringBuilder(connection)
        {
            InitialCatalog = $"HavenSupport_M0_{Guid.NewGuid():N}",
            ConnectTimeout = 5
        };
        await using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer(settings.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync(token);
            var id = Guid.NewGuid();
            db.FoundationProbes.Add(new FoundationProbe { Id = id, Label = "M0 round trip" });
            await db.SaveChangesAsync(token);
            var found = await db.FoundationProbes.AsNoTracking().SingleAsync(probe => probe.Id == id, token);
            if (found.Label != "M0 round trip") throw new InvalidOperationException("SQL content mismatch.");
        }
        finally
        {
            // Only the GUID-named database selected above can be removed.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await db.Database.EnsureDeletedAsync(cleanup.Token);
        }
        return $"Migration, insert/read and isolated database cleanup passed ({settings.InitialCatalog}).";
    }

    public static async Task<string> QdrantAsync(ProviderOptions options, CancellationToken token)
    {
        using var client = new QdrantClient(options.QdrantHost, options.QdrantGrpcPort);
        var collection = $"haven_m0_{Guid.NewGuid():N}";
        var created = false;
        try
        {
            await client.CreateCollectionAsync(collection, new VectorParams { Size = 3, Distance = Distance.Cosine }, cancellationToken: token);
            created = true;
            await client.CreatePayloadIndexAsync(collection, "visibility", PayloadSchemaType.Keyword, cancellationToken: token);
            await client.UpsertAsync(collection,
            [
                new PointStruct { Id = 1UL, Vectors = new float[] { 1, 0, 0 }, Payload = { ["visibility"] = "private" } },
                new PointStruct { Id = 2UL, Vectors = new float[] { 0.9f, 0.1f, 0 }, Payload = { ["visibility"] = "public" } }
            ], cancellationToken: token);
            var filter = new Filter
            {
                Must = { new Condition { Field = new FieldCondition { Key = "visibility", Match = new Match { Keyword = "public" } } } }
            };
            var results = await client.QueryAsync(collection, new float[] { 1, 0, 0 }, filter: filter, limit: 5, cancellationToken: token);
            if (results.Count != 1 || results[0].Id.Num != 2UL)
                throw new InvalidOperationException("Qdrant filter did not exclude the private point.");
        }
        finally
        {
            if (created)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await client.DeleteCollectionAsync(collection, cancellationToken: cleanup.Token);
            }
        }
        return $"Collection, index, upsert, filtered search and cleanup passed ({collection}).";
    }

    public static async Task<string> ChatAsync(ProviderOptions options, CancellationToken token)
    {
        using var http = new HttpClient { BaseAddress = new Uri(options.OllamaBaseUrl), Timeout = TimeSpan.FromMinutes(5) };
        using IChatClient chat = new OllamaApiClient(http) { SelectedModel = options.ChatModel };
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { status = new { type = "string", @enum = new[] { "ok" } } },
            required = new[] { "status" },
            additionalProperties = false
        });
        var response = await chat.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Return a JSON object with the status string equal to ok.")],
            new ChatOptions
            {
                Temperature = 0,
                MaxOutputTokens = 48,
                ResponseFormat = ChatResponseFormat.ForJsonSchema(schema),
                AdditionalProperties = new() { ["num_ctx"] = 2048, ["num_gpu"] = 0 }
            }, token);
        using var json = JsonDocument.Parse(response.Text);
        if (json.RootElement.EnumerateObject().Count() != 1 || json.RootElement.GetProperty("status").GetString() != "ok")
            throw new InvalidOperationException("Structured output content/schema validation failed.");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await RequireCancellationAsync(() => chat.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hello")], cancellationToken: canceled.Token));
        using var inFlight = CancellationTokenSource.CreateLinkedTokenSource(token);
        inFlight.CancelAfter(TimeSpan.FromMilliseconds(200));
        await RequireCancellationAsync(() => chat.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Write a detailed list of 500 different household objects.")],
            new ChatOptions { MaxOutputTokens = 2048, AdditionalProperties = new() { ["num_ctx"] = 2048, ["num_gpu"] = 0 } }, inFlight.Token));
        return "IChatClient structured JSON validated; pre-canceled and in-flight cancellation passed; num_ctx=2048, num_gpu=0.";
    }

    public static async Task<string> EmbeddingsAsync(ProviderOptions options, CancellationToken token)
    {
        using var http = new HttpClient { BaseAddress = new Uri(options.OllamaBaseUrl), Timeout = TimeSpan.FromMinutes(5) };
        using IEmbeddingGenerator<string, Embedding<float>> generator =
            new OllamaApiClient(http) { SelectedModel = options.EmbeddingModel };
        var vectors = await generator.GenerateAsync(
            ["Unopened items can be returned within thirty days.", "Opened items cannot be returned for change of mind."], cancellationToken: token);
        if (vectors.Count != 2 || vectors.Any(vector => vector.Vector.Length != 384
            || vector.Vector.ToArray().Any(value => !float.IsFinite(value))
            || vector.Vector.ToArray().All(value => value == 0)))
            throw new InvalidOperationException("Expected two finite nonzero 384-dimensional all-minilm embeddings.");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await RequireCancellationAsync(() => generator.GenerateAsync(["cancellation check"], cancellationToken: canceled.Token));
        return "IEmbeddingGenerator: two finite nonzero vectors, dimension=384; pre-canceled call passed.";
    }

    private static async Task RequireCancellationAsync<T>(Func<Task<T>> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Cancellation was ignored or the probe finished before cancellation.");
    }
}

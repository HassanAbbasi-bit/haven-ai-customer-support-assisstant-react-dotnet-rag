using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HavenSupport.Api;
using HavenSupport.Domain;
using HavenSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace HavenSupport.IntegrationTests;

public sealed class SqlFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string database = "HavenSupport_Test_" + Guid.NewGuid().ToString("N");
    private readonly string signingKey = SessionSecurity.Secret();
    public string AdminPassword { get; } = SessionSecurity.Secret() + "Aa1!";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer") ?? throw new InvalidOperationException("SQL integration tests require ConnectionStrings__SqlServer. Run Set-LocalSqlConnection.ps1 first, or exclude Category=Sql for provider-independent tests.");
        var settings = new SqlConnectionStringBuilder(configured) { InitialCatalog = database };
        builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:SigningKey"] = signingKey, ["ConnectionStrings:SqlServer"] = settings.ConnectionString }));
    }
    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        await db.Database.MigrateAsync(); db.Corpus.Add(new CorpusRevision()); await db.SaveChangesAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(); await roles.CreateAsync(new IdentityRole("Admin")); await roles.CreateAsync(new IdentityRole("Customer"));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<HavenUser>>(); var admin = new HavenUser { UserName = "admin@test.local", Email = "admin@test.local" };
        Assert.True((await users.CreateAsync(admin, AdminPassword)).Succeeded); Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
    }
    async Task IAsyncLifetime.DisposeAsync()
    {
        using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var name = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
        if (name != database || !name.StartsWith("HavenSupport_Test_", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing to delete a non-test database.");
        await db.Database.EnsureDeletedAsync(); await DisposeAsync();
    }
    public async Task<HttpClient> GuestAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }); client.DefaultRequestHeaders.Add("X-Haven-Request", "1");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/guest-session", null)).StatusCode); return client;
    }
    public async Task<HttpClient> AdminAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }); client.DefaultRequestHeaders.Add("X-Haven-Request", "1");
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@test.local", password = AdminPassword });
        response.EnsureSuccessStatusCode(); var login = await response.Content.ReadFromJsonAsync<Token>(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken); return client;
    }
    private sealed record Token(string AccessToken);
}
[Trait("Category", "Sql")]
public sealed class OwnershipTests(SqlFactory factory) : IClassFixture<SqlFactory>
{
    [Fact]
    public async Task TwoGuestsCannotReadEachOthersConversationsOrSubmitRequests()
    {
        using var alice = await factory.GuestAsync(); using var bob = await factory.GuestAsync();
        var created = await alice.PostAsync("/api/v1/conversations", null); var conversation = await created.Content.ReadFromJsonAsync<Conversation>();
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/conversations/{conversation!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/requests", new { clientRequestId = Guid.NewGuid(), message = "hello" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/v1/admin/documents")).StatusCode);
    }
    [Fact]
    public async Task RequestRetriesAreIdempotentAndCancellationIsTerminal()
    {
        using var client = await factory.GuestAsync(); var created = await client.PostAsync("/api/v1/conversations", null); var conversation = await created.Content.ReadFromJsonAsync<Conversation>();
        var path = $"/api/v1/conversations/{conversation!.Id}/requests"; var key = Guid.NewGuid();
        var first = await client.PostAsJsonAsync(path, new { clientRequestId = key, message = "return policy" }); first.EnsureSuccessStatusCode(); var accepted = await first.Content.ReadFromJsonAsync<ChatRequest>();
        var retry = await client.PostAsJsonAsync(path, new { clientRequestId = key, message = "return policy" }); Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        Assert.Equal(accepted!.Id, (await retry.Content.ReadFromJsonAsync<ChatRequest>())!.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { clientRequestId = key, message = "different" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { clientRequestId = Guid.NewGuid(), message = "second active" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{path}/{accepted.Id}/cancel", null)).StatusCode);
        Assert.Equal("canceled", (await client.GetFromJsonAsync<ChatRequest>($"{path}/{accepted.Id}"))!.State);
    }
    [Fact]
    public async Task TicketsRequireConsentAndCannotBeReadByAnotherOwner()
    {
        using var alice = await factory.GuestAsync(); using var bob = await factory.GuestAsync(); var key = Guid.NewGuid();
        var body = new { clientRequestId = key, subject = "Delivery help", summary = "A synthetic test case.", email = "customer@example.test", orderReference = (string?)null, sharedContext = "", consent = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/v1/tickets", new { clientRequestId = Guid.NewGuid(), subject = "x", summary = "x", email = "a@example.test", consent = false })).StatusCode);
        var first = await alice.PostAsJsonAsync("/api/v1/tickets", body); first.EnsureSuccessStatusCode(); var ticket = await first.Content.ReadFromJsonAsync<SupportTicket>();
        var repeated = await alice.PostAsJsonAsync("/api/v1/tickets", body); repeated.EnsureSuccessStatusCode(); Assert.Equal(ticket!.Id, (await repeated.Content.ReadFromJsonAsync<SupportTicket>())!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/tickets/{ticket.Id}")).StatusCode);
    }
    [Fact]
    public async Task AdminRoleIsServerEnforcedAndBadFilesAreRejected()
    {
        using var client = await factory.AdminAsync(); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/documents")).StatusCode);
        using var form = new MultipartFormDataContent(); form.Add(new StringContent("Policy"), "title"); form.Add(new StringContent("1.0"), "version"); form.Add(new StringContent("2026-10-01"), "effectiveDate"); form.Add(new ByteArrayContent("not a PDF"u8.ToArray()), "file", "policy.pdf");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/documents", form)).StatusCode);
    }

    [Fact]
    public async Task CitationsCheckOwnershipAndWithdrawnSourcesDoNotLeakText()
    {
        using var alice = await factory.GuestAsync(); using var bob = await factory.GuestAsync();
        var created = await alice.PostAsync("/api/v1/conversations", null); created.EnsureSuccessStatusCode();
        var conversation = (await created.Content.ReadFromJsonAsync<Conversation>())!;
        Guid messageId; Guid citationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var document = new KnowledgeDocument { Title = "Synthetic policy" }; var version = new DocumentVersion { DocumentId = document.Id, State = "ready", Publication = "published" }; document.ActiveVersionId = version.Id;
            var chunk = new KnowledgeChunk { Id = Guid.NewGuid(), VersionId = version.Id, Text = "Synthetic source text for ownership verification.", SourceKey = "TEST-01" };
            var request = new ChatRequest { ConversationId = conversation.Id, ClientRequestId = Guid.NewGuid(), Question = "test", State = "completed" };
            var answer = new Message { ConversationId = conversation.Id, RequestId = request.Id, Role = "assistant", Text = "Synthetic answer" };
            var citation = new Citation { ChunkId = chunk.Id, MessageId = answer.Id, Quote = chunk.Text, Claim = answer.Text }; messageId = answer.Id; citationId = citation.Id;
            db.Documents.Add(document); db.Versions.Add(version); db.Chunks.Add(chunk); db.ChatRequests.Add(request); db.Messages.Add(answer); db.Citations.Add(citation); await db.SaveChangesAsync();
        }
        var path = $"/api/v1/messages/{messageId}/citations/{citationId}";
        Assert.Contains("Synthetic source text", await alice.GetStringAsync(path));
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/v1/messages/{messageId}/feedback", new { helpful = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.PutAsJsonAsync($"/api/v1/messages/{messageId}/feedback", new { helpful = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.PutAsJsonAsync($"/api/v1/messages/{messageId}/feedback", new { helpful = false })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            Assert.Single(await db.Feedback.Where(f => f.MessageId == messageId).ToArrayAsync());
            await db.Documents.Where(d => d.Title == "Synthetic policy").ExecuteUpdateAsync(s => s.SetProperty(d => d.ActiveVersionId, (Guid?)null));
        }
        Assert.Equal(HttpStatusCode.Gone, (await alice.GetAsync(path)).StatusCode);
        var history = await alice.GetStringAsync($"/api/v1/conversations/{conversation.Id}");
        Assert.DoesNotContain("Synthetic source text", history);
        Assert.Contains("Synthetic answer", history);
    }

    [Fact]
    public async Task LogoutRevokesAnExistingJwtImmediately()
    {
        using var admin = await factory.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/v1/admin/documents")).StatusCode);
    }
}

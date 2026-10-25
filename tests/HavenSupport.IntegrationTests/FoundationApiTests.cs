using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
namespace HavenSupport.IntegrationTests;

public sealed class FoundationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:SigningKey"] = new string('x', 64),
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1,1;Database=Unavailable;Integrated Security=true;Connect Timeout=1"
        }));
    }
}
public sealed class FoundationApiTests : IClassFixture<FoundationFactory>
{
    private readonly HttpClient client;
    public FoundationApiTests(FoundationFactory factory) => client = factory.CreateClient();
    [Fact]
    public async Task LivenessDoesNotClaimProviderReadiness()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }
    [Fact]
    public async Task BusinessRoutesRequireAnAuthenticatedOwner()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/conversations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/documents")).StatusCode);
    }
    [Fact]
    public async Task MutationsRejectMissingBrowserRequestHeader()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/auth/guest-session", null)).StatusCode);
    }
    [Fact]
    public async Task MutationsRejectAnUntrustedOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/guest-session");
        request.Headers.Add("X-Haven-Request", "1"); request.Headers.Add("Origin", "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }
}

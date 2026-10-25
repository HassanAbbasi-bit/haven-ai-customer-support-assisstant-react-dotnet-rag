using System.Security.Claims;
using System.Threading.RateLimiting;
using HavenSupport.Api;
using HavenSupport.Domain;
using HavenSupport.Infrastructure;
using HavenSupport.Infrastructure.Configuration;
using HavenSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var repository = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../.."));
if (builder.Environment.IsDevelopment()) builder.Configuration.AddJsonFile(Path.Combine(repository, ".local/runtime.json"), optional: true).AddEnvironmentVariables();
builder.Services.AddOptions<AuthConfiguration>().Bind(builder.Configuration.GetSection("Auth"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 64, "Configure a random Auth:SigningKey of at least 64 characters using scripts/Initialize-App.ps1.").ValidateOnStart();
builder.Configuration["Storage:Path"] ??= Path.Combine(repository, ".local/private-documents");
builder.Services.AddDbContext<SupportDbContext>((services, options) => options.UseSqlServer(services.GetRequiredService<IConfiguration>().GetConnectionString("SqlServer") ?? throw new InvalidOperationException("Configure ConnectionStrings:SqlServer.")));
builder.Services.AddOptions<ProviderOptions>().Bind(builder.Configuration.GetSection("Providers")).Validate(o => o.IsValid(), "Invalid provider settings.").ValidateOnStart();
builder.Services.AddScoped<RagEngine>();
SessionSecurity.Configure(builder);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 21 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 21 * 1024 * 1024);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    foreach (var policy in new[] { ("auth", 20), ("chat", 10), ("upload", 5) })
        options.AddPolicy(policy.Item1, context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : policy.Item2, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
if (!builder.Environment.IsEnvironment("Testing")) builder.Services.AddHostedService<SupportWorker>();
var app = builder.Build();
if (args.Contains("--initialize"))
{
    using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
    await db.Database.MigrateAsync();
    if (!await db.Corpus.AnyAsync()) { db.Corpus.Add(new CorpusRevision()); await db.SaveChangesAsync(); }
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Customer" }) if (!await roles.RoleExistsAsync(role)) { var result = await roles.CreateAsync(new IdentityRole(role)); if (!result.Succeeded) throw new InvalidOperationException("Role initialization failed."); }
    var users = scope.ServiceProvider.GetRequiredService<UserManager<HavenUser>>();
    var email = builder.Configuration["Bootstrap:Email"] ?? throw new InvalidOperationException("Set Bootstrap:Email.");
    var admin = await users.FindByEmailAsync(email);
    if (admin is null)
    {
        admin = new HavenUser { UserName = email, Email = email };
        var result = await users.CreateAsync(admin, builder.Configuration["Bootstrap:Password"] ?? throw new InvalidOperationException("Set Bootstrap:Password."));
        if (!result.Succeeded) throw new InvalidOperationException("Admin bootstrap failed password validation.");
        if (!(await users.AddToRoleAsync(admin, "Admin")).Succeeded) throw new InvalidOperationException("Admin role assignment failed.");
    }
    if (!await users.IsInRoleAsync(admin, "Admin")) throw new InvalidOperationException("The bootstrap email belongs to an existing non-admin account. Choose a distinct bootstrap email; it will not be elevated automatically.");
    Console.WriteLine("SQL migrations, roles and local administrator initialized. Existing accounts and passwords were preserved.");
    return;
}
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var conflict = error is DbUpdateConcurrencyException;
    context.Response.StatusCode = conflict ? 412 : 503;
    await Results.Problem(statusCode: context.Response.StatusCode, title: conflict ? "The record changed" : "Service temporarily unavailable",
        detail: conflict ? "Refresh this record before trying again." : "The request could not be completed. Please try again. Your existing data has been preserved.",
        extensions: new Dictionary<string, object?> { ["code"] = conflict ? "version_conflict" : "service_unavailable", ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
}));
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    if (context.Request.Path.StartsWithSegments("/api") && context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
    {
        var origin = context.Request.Headers.Origin.ToString();
        var allowed = new[] { "http://127.0.0.1:5173", "http://localhost:5173", "http://127.0.0.1:5080", "https://localhost:5173", "https://127.0.0.1:5173" };
        if (context.Request.Headers["X-Haven-Request"] != "1" || (origin.Length > 0 && !allowed.Contains(origin, StringComparer.Ordinal)) || context.Request.Headers["Sec-Fetch-Site"] == "cross-site")
        {
            await ApiErrors.Bad("invalid_request_origin", "Reload Haven Support and try again.", 403).ExecuteAsync(context); return;
        }
    }
    await next();
});
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
SessionSecurity.MapAuth(app); SupportEndpoints.MapSupport(app);
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (SupportDbContext db, RagEngine engine, Microsoft.Extensions.Options.IOptions<ProviderOptions> options, CancellationToken token) =>
{
    try
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        if (!await db.Database.CanConnectAsync(timeout.Token) || !await db.Corpus.AnyAsync(timeout.Token)) return Results.StatusCode(503);
        await engine.EnsureCollectionAsync(timeout.Token);
        using var http = new HttpClient { BaseAddress = new Uri(options.Value.OllamaBaseUrl), Timeout = TimeSpan.FromSeconds(3) };
        using var tags = System.Text.Json.JsonDocument.Parse(await http.GetStringAsync("/api/tags", timeout.Token));
        var names = tags.RootElement.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("name").GetString()).ToArray();
        return names.Contains(options.Value.ChatModel) && names.Contains(options.Value.EmbeddingModel) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
    }
    catch { return Results.StatusCode(503); }
});
app.MapGet("/api/v1/status", () => Results.Ok(new { name = "Haven Support", milestone = "functional-rag", businessFeaturesImplemented = true }));
app.Run();
public partial class Program;

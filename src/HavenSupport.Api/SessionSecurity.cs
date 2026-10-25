using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using HavenSupport.Domain;
using HavenSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HavenSupport.Api;

public sealed class AuthConfiguration { public string SigningKey { get; set; } = ""; }

public static class SessionSecurity
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Owner(this ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing authenticated subject.");
    public static CookieOptions Cookie(HttpContext context, int days) => new()
    {
        HttpOnly = true,
        Secure = !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() || context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",
        Expires = DateTimeOffset.UtcNow.AddDays(days),
        IsEssential = true
    };
    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityCore<HavenUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<SupportDbContext>();
        builder.Services.AddAuthentication(options => { options.DefaultScheme = "session"; options.DefaultChallengeScheme = "session"; })
            .AddPolicyScheme("session", "session", options => options.ForwardDefaultSelector = context =>
                context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? JwtBearerDefaults.AuthenticationScheme : "guest")
            .AddScheme<AuthenticationSchemeOptions, GuestAuthenticationHandler>("guest", _ => { })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Auth:SigningKey"]!)),
                    ValidateIssuer = true,
                    ValidIssuer = "HavenSupport",
                    ValidateAudience = true,
                    ValidAudience = "HavenSupport.Web",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(15),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<HavenUser>>();
                        var user = await users.FindByIdAsync(context.Principal!.Owner());
                        if (user is null || user.SecurityStamp != context.Principal!.FindFirstValue("stamp")) context.Fail("Session is no longer valid.");
                        else if (context.Principal!.IsInRole("Admin") && !await users.IsInRoleAsync(user, "Admin")) context.Fail("Admin access has been revoked.");
                    }
                };
            });
        builder.Services.AddAuthorization();
    }
    public static void MapAuth(WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").RequireRateLimiting("auth");
        auth.MapPost("/guest-session", async (HttpContext context, SupportDbContext db) =>
        {
            // Guest cookie must cover owned-resource endpoints, not just /auth.
            var existing = context.Request.Cookies["haven_guest"];
            if (existing is not null && await db.Guests.AnyAsync(g => g.TokenHash == Hash(existing) && g.ExpiresAt > DateTimeOffset.UtcNow))
                return Results.Ok(new { guest = true });
            var secret = Secret();
            db.Guests.Add(new GuestSession { TokenHash = Hash(secret), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
            var cookie = Cookie(context, 1); cookie.Path = "/api";
            context.Response.Cookies.Append("haven_guest", secret, cookie);
            return Results.Ok(new { guest = true });
        });
        auth.MapPost("/register", async (Credentials input, UserManager<HavenUser> users) =>
        {
            if (!ValidCredentials(input)) return ApiErrors.Bad("invalid_credentials", "Enter a valid email and a password between 12 and 128 characters.");
            var user = new HavenUser { UserName = input.Email.Trim(), Email = input.Email.Trim() };
            var result = await users.CreateAsync(user, input.Password);
            if (!result.Succeeded) return ApiErrors.Bad("registration_failed", "Registration could not be completed. Use a unique email and a password with uppercase, lowercase, number and symbol.");
            await users.AddToRoleAsync(user, "Customer");
            return Results.Ok(new { registered = true });
        });
        auth.MapPost("/login", async (Credentials input, HttpContext context, UserManager<HavenUser> users, SupportDbContext db, IConfiguration configuration) =>
        {
            if (!ValidCredentials(input)) return Results.Unauthorized();
            var user = await users.FindByEmailAsync(input.Email.Trim());
            if (user is null || await users.IsLockedOutAsync(user)) return Results.Unauthorized();
            if (!await users.CheckPasswordAsync(user, input.Password)) { await users.AccessFailedAsync(user); return Results.Unauthorized(); }
            await users.ResetAccessFailedCountAsync(user);
            return await IssueAsync(user, Guid.NewGuid(), context, db, users, configuration);
        });
        auth.MapPost("/refresh", async (HttpContext context, SupportDbContext db, UserManager<HavenUser> users, IConfiguration configuration) =>
        {
            var secret = context.Request.Cookies["haven_refresh"];
            if (secret is null) return Results.Unauthorized();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var session = await db.RefreshSessions.SingleOrDefaultAsync(s => s.TokenHash == Hash(secret));
            if (session is null) return Results.Unauthorized();
            if (session.Revoked || session.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                await db.RefreshSessions.Where(s => s.FamilyId == session.FamilyId).ExecuteUpdateAsync(set => set.SetProperty(s => s.Revoked, true));
                var compromised = await users.FindByIdAsync(session.UserId);
                if (compromised is not null) await users.UpdateSecurityStampAsync(compromised);
                await transaction.CommitAsync();
                return Results.Unauthorized();
            }
            session.Revoked = true; await db.SaveChangesAsync();
            var user = await users.FindByIdAsync(session.UserId);
            if (user is null) return Results.Unauthorized();
            var result = await IssueAsync(user, session.FamilyId, context, db, users, configuration);
            await transaction.CommitAsync(); return result;
        });
        auth.MapPost("/logout", async (HttpContext context, SupportDbContext db, UserManager<HavenUser> users) =>
        {
            var secret = context.Request.Cookies["haven_refresh"];
            var session = secret is null ? null : await db.RefreshSessions.SingleOrDefaultAsync(s => s.TokenHash == Hash(secret));
            if (session is not null)
            {
                await db.RefreshSessions.Where(s => s.UserId == session.UserId).ExecuteUpdateAsync(set => set.SetProperty(s => s.Revoked, true));
                var user = await users.FindByIdAsync(session.UserId); if (user is not null) await users.UpdateSecurityStampAsync(user);
            }
            context.Response.Cookies.Delete("haven_refresh", Cookie(context, 0));
            return Results.NoContent();
        });
        auth.MapGet("/me", (ClaimsPrincipal principal) => Results.Ok(new { id = principal.Owner(), email = principal.FindFirstValue(ClaimTypes.Email), role = principal.FindFirstValue(ClaimTypes.Role) })).RequireAuthorization();
    }
    private static bool ValidCredentials(Credentials input) => input.Email is { Length: <= 254 } && System.Net.Mail.MailAddress.TryCreate(input.Email, out _) && input.Password is { Length: >= 12 and <= 128 };
    private static async Task<IResult> IssueAsync(HavenUser user, Guid family, HttpContext context, SupportDbContext db, UserManager<HavenUser> users, IConfiguration configuration)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Email, user.Email!), new("stamp", user.SecurityStamp!), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
        claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim(ClaimTypes.Role, role)));
        var jwt = new JwtSecurityToken("HavenSupport", "HavenSupport.Web", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Auth:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var secret = Secret();
        db.RefreshSessions.Add(new RefreshSession { UserId = user.Id, FamilyId = family, TokenHash = Hash(secret), ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
        await db.SaveChangesAsync();
        context.Response.Cookies.Append("haven_refresh", secret, Cookie(context, 7));
        return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(jwt), email = user.Email, role = (await users.GetRolesAsync(user)).Contains("Admin") ? "Admin" : "Customer" });
    }
    public sealed record Credentials(string Email, string Password);
}
public sealed class GuestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, SupportDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var secret = Request.Cookies["haven_guest"];
        if (secret is null || secret.Length != 64) return AuthenticateResult.NoResult();
        var hash = SessionSecurity.Hash(secret);
        var guest = await db.Guests.AsNoTracking().SingleOrDefaultAsync(g => g.TokenHash == hash && g.ExpiresAt > DateTimeOffset.UtcNow, Context.RequestAborted);
        if (guest is null) return AuthenticateResult.NoResult();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, $"guest:{guest.Id}"), new Claim(ClaimTypes.Role, "Guest")], Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
public static class ApiErrors
{
    public static IResult Bad(string code, string detail, int status = 400) => Results.Problem(statusCode: status, title: code.Replace('_', ' '), detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}

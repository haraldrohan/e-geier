using System.Threading.RateLimiting;
using EGeier;
using EGeier.Geocoding;
using EGeier.Mcp;
using Microsoft.AspNetCore.HttpOverrides;
using ModelContextProtocol.Protocol;

// Remote (Streamable HTTP) variant of the E-Geier MCP server, for claude.ai, the Claude apps and
// other clients that cannot start local processes. Tools are identical to the stdio server.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSpritClient();
builder.Services.AddNominatimGeocoder();
builder.Services.Configure<NominatimOptions>(builder.Configuration.GetSection("Nominatim"));
builder.Services.AddSingleton<RegionDirectory>();

builder.Services
    .AddMcpServer(options => options.ServerInfo = new Implementation
    {
        Name = "e-geier",
        Title = "E-Geier",
        Version = typeof(SpritTools).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
    })
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<SpritTools>();

// Hosting platforms terminate TLS in front of the app; trust their forwarded client address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Keeps a single client from exhausting the upstream APIs (Nominatim allows one request per second in total).
// Behind a CDN such as Cloudflare, set RateLimit:ClientIpHeader (e.g. "CF-Connecting-IP") so that clients are
// told apart; only do so if the origin is reachable exclusively through that CDN, as the header can be forged.
var permitsPerMinute = builder.Configuration.GetValue("RateLimit:PermitsPerMinute", 30);
var clientIpHeader = builder.Configuration["RateLimit:ClientIpHeader"];
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

string ClientKey(HttpContext context) =>
    clientIpHeader is { Length: > 0 } && context.Request.Headers[clientIpHeader] is [{ Length: > 0 } ip, ..]
        ? ip
        : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

var app = builder.Build();

app.UseForwardedHeaders();
app.UseRateLimiter();

app.MapGet("/", () => Results.Text(
    """
    E-Geier – MCP server for Austrian fuel prices (Spritpreisrechner).
    MCP endpoint: /mcp (Streamable HTTP, no authentication).
    Unofficial, not affiliated with E-Control Austria. No guarantee of accuracy or timeliness.
    https://github.com/haraldrohan/e-geier
    """));
app.MapGet("/health", () => Results.Ok("ok")).DisableRateLimiting();
app.MapMcp("/mcp");

await app.RunAsync().ConfigureAwait(false);

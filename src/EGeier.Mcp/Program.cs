using EGeier;
using EGeier.Geocoding;
using EGeier.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol; all logging must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

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
    .WithStdioServerTransport()
    .WithTools<SpritTools>();

await builder.Build().RunAsync().ConfigureAwait(false);

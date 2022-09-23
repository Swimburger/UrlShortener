using System.Text.Json.Nodes;
using StackExchange.Redis;
using UrlShortener.Data;

var builder = WebApplication.CreateBuilder(args);
var forwarderBaseUrl = builder.Configuration["UrlShortener:ForwarderBaseUrl"]
    ?? throw new Exception("Missing 'UrlShortener:ForwarderBaseUrl' configuration");
var connectionString = builder.Configuration.GetConnectionString("UrlsDb")
            ?? throw new Exception("Missing 'UrlsDb' connection string");
var redisConnection = await ConnectionMultiplexer.ConnectAsync(connectionString);
builder.Services.AddSingleton(redisConnection);
builder.Services.AddTransient<ShortUrlRepository>();

var app = builder.Build();

app.UseDefaultFiles()
    .UseStaticFiles();

app.MapPost("/api/urls", async (
    HttpRequest request,
    ShortUrlRepository shortUrlRepository
) =>
{
    var jsonObject = await request.ReadFromJsonAsync<JsonObject>();
    var path = jsonObject?["path"]?.GetValue<string?>()?.Trim('/');
    var destination = jsonObject?["destination"]?.GetValue<string?>();

    var shortUrl = new ShortUrl(destination, path);
    if (shortUrl.Validate(out var validationResults) == false)
    {
        return Results.ValidationProblem(validationResults);
    }

    if (await shortUrlRepository.Exists(path))
    {
        return Results.Problem($"Path is already in use.");
    }
    
    await shortUrlRepository.Create(shortUrl);
    
    return Results.Created(
        uri: new Uri($"{request.Scheme}://{request.Host}{request.PathBase}/api/urls/{path}"),
        value: new
        {
            Path = path,
            Destination = destination,
            ShortenedUrl = $"{forwarderBaseUrl}/{path}"
        }
    );
});

app.Run();
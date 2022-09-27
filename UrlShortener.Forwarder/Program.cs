using StackExchange.Redis;
using UrlShortener.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ShortenedUrlsDb")
            ?? throw new Exception("Missing 'ShortenedUrlsDb' connection string");
var redisConnection = await ConnectionMultiplexer.ConnectAsync(connectionString);
builder.Services.AddSingleton(redisConnection);
builder.Services.AddTransient<ShortUrlRepository>();

var app = builder.Build();

app.MapGet("/{path}", async (
    string path,
    ShortUrlRepository shortUrlRepository
) =>
{
    if(ShortUrlValidator.ValidatePath(path, out _))
        return Results.BadRequest();

    var shortUrl = await shortUrlRepository.Get(path);
    if (shortUrl == null || string.IsNullOrEmpty(shortUrl.Destination))
        return Results.NotFound();

    return Results.Redirect(shortUrl.Destination);
});

app.Run();
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var forwarderBaseUrl = builder.Configuration["UrlShortener:ForwarderBaseUrl"]
    ?? throw new Exception("Missing 'UrlShortener:ForwarderBaseUrl' configuration");

{
    var connectionString = builder.Configuration.GetConnectionString("UrlsDb")
                ?? throw new Exception("Missing 'UrlsDb' connection string");
    var redisDb = await ConnectionMultiplexer.ConnectAsync(connectionString);
    builder.Services.AddSingleton<ConnectionMultiplexer>(redisDb);
    builder.Services.AddTransient(provider =>
        provider.GetRequiredService<ConnectionMultiplexer>().GetDatabase()
    );
}

var app = builder.Build();

app.UseDefaultFiles()
    .UseStaticFiles();

var pathRegex = new Regex(
    "^[a-zA-Z0-9_]*$",
    RegexOptions.None,
    TimeSpan.FromMilliseconds(1)
);

app.MapPost("/api/urls", async (
    HttpRequest request,
    IDatabase redisDb
) =>
{
    var jsonObject = await request.ReadFromJsonAsync<JsonObject>();
    var path = jsonObject?["path"]?.GetValue<string?>()?.Trim('/');
    var destination = jsonObject?["destination"]?.GetValue<string?>();

    if (string.IsNullOrEmpty(path))
        return Results.Problem("Path cannot be empty.");

    if (path.Length > 10)
        return Results.Problem("Path cannot be longer than 10 characters.");

    if (pathRegex.IsMatch(path) == false)
        return Results.Problem("Path can only contain alphanumeric characters and underscores.");

    if (string.IsNullOrEmpty(destination))
        return Results.Problem("Destination cannot be empty.");

    if (!Uri.IsWellFormedUriString(destination, UriKind.Absolute))
        return Results.Problem("Destination has to be a valid absolute URL.");

    if (await redisDb.KeyExistsAsync(path))
        return Results.Problem("Path is already in use.");

    var urlWasSet = await redisDb.StringSetAsync(path, destination);
    if (!urlWasSet)
        return Results.Problem(
            "Failed to create shortened URL.",
            statusCode: (int)HttpStatusCode.InternalServerError
        );

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
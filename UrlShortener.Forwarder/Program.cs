using System.Text.RegularExpressions;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

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

var pathRegex = new Regex(
    "^[a-zA-Z0-9_]*$",
    RegexOptions.None,
    TimeSpan.FromMilliseconds(1)
);

app.MapGet("/{path}", async (
    string path,
    IDatabase redisDb
) =>
{
    if (string.IsNullOrEmpty(path) ||
        path.Length > 10 ||
        pathRegex.IsMatch(path) == false)
        return Results.BadRequest();

    var redisValue = await redisDb.StringGetAsync(path);
    if (redisValue.IsNullOrEmpty)
        return Results.NotFound();

    var destination = redisValue.ToString();

    return Results.Redirect(destination);
});

app.Run();
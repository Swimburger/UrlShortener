using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<UrlShortenerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString($"UrlDb")));

var app = builder.Build();

app.UseDefaultFiles()
    .UseStaticFiles();

string AbsoluteUrl(HttpRequest request, string path)
    => $"{request.Scheme}://{request.Host}{request.PathBase}{path}";

var pathRegex = new Regex(
    "^[a-zA-Z0-9_]*$",
    RegexOptions.None,
    TimeSpan.FromMilliseconds(1)
);

app.MapPost("/api/urls", async (
    HttpRequest request,
    UrlShortenerDbContext dbContext
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

    if (dbContext.Urls.Any(u => u.Path.Equals(path)))
        return Results.Problem("Path is already in use.");

    var url = new Url {Path = path, Destination = destination};
    await dbContext.Urls.AddAsync(url);
    await dbContext.SaveChangesAsync();

    return Results.Created(
        uri: new Uri(AbsoluteUrl(request, $"/api/urls/{url.Path}")),
        value: new
        {
            Path = url.Path,
            Destination = url.Destination,
            Id = url.Id,
            ShortenedUrl = AbsoluteUrl(request, $"/{url.Path}")
        }
    );
});

app.MapGet("/{path}", async (
    string path,
    UrlShortenerDbContext dbContext
) =>
{
    if (string.IsNullOrEmpty(path) ||
        path.Length > 10 ||
        pathRegex.IsMatch(path) == false)
        return Results.BadRequest();

    var url = await dbContext.Urls.FirstOrDefaultAsync(u => u.Path == path);
    if (url == null)
        return Results.NotFound();

    return Results.Redirect(url.Destination);
});

app.Run();

public sealed class UrlShortenerDbContext : DbContext
{
    public DbSet<Url> Urls { get; set; }

    public UrlShortenerDbContext(DbContextOptions<UrlShortenerDbContext> options)
        : base(options)
    {
    }
}

[Index(nameof(Path), IsUnique = true)]
public sealed record Url
{
    public Guid Id { get; set; }
    public string Path { get; set; }
    public string Destination { get; set; }
}
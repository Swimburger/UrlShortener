using System.CommandLine;
using StackExchange.Redis;
using UrlShortener.Data;

#region Options

var destinationOption = new Option<string>(
    new[] {"--destination-url", "-d"},
    description: "The URL that the shortened URL will forward to."
);
destinationOption.IsRequired = true;
destinationOption.AddValidator(result =>
{
    var destination = result.Tokens[0].Value;
    if (ShortUrlValidator.ValidateDestination(destination, out var validationResults) == false)
    {
        result.ErrorMessage = string.Join(", ", validationResults);
    }
});

var pathOption = new Option<string>(
    new[] {"--path", "-p"},
    description: "The path used for the shortened URL."
);
pathOption.IsRequired = true;
pathOption.AddValidator(result =>
{
    var path = result.Tokens[0].Value;
    if (ShortUrlValidator.ValidatePath(path, out var validationResults) == false)
    {
        result.ErrorMessage = string.Join(", ", validationResults);
    }
});

var connectionStringOption = new Option<string?>(
    new[] {"--connection-string", "-c"},
    description: "Connection string to connect to the Redis Database where URLs are stored. " +
                 "Alternatively, you can set the 'URL_SHORTENER_CONNECTION_STRING'."
);
var envConnectionString = Environment.GetEnvironmentVariable("URL_SHORTENER_CONNECTION_STRING");
if (string.IsNullOrEmpty(envConnectionString))
{
    connectionStringOption.IsRequired = true;
}

#endregion

var rootCommand = new RootCommand("Manage the shortened URLs.");

async Task<ConnectionMultiplexer> GetRedisConnection(string? connectionString)
{
    var redisConnection = await ConnectionMultiplexer.ConnectAsync(
        connectionString ??
        envConnectionString ??
        throw new Exception("Missing connection string")
    );
    return redisConnection;
}

#region Create Command

var createCommand = new Command("create", "Create a shortened URL")
{
    destinationOption,
    pathOption,
    connectionStringOption
};

createCommand.SetHandler(async (destination, path, connectionString) =>
{
    var shortUrlRepository = new ShortUrlRepository(await GetRedisConnection(connectionString));
    try
    {
        await shortUrlRepository.Create(new ShortUrl(destination, path));
        Console.WriteLine($"Shortened URL created.");
    }
    catch (Exception e)
    {
        Console.Error.WriteLine(e.Message);
    }
}, destinationOption, pathOption, connectionStringOption);

rootCommand.AddCommand(createCommand);

#endregion

#region Delete Command

var deleteCommand = new Command("delete", "Delete a shortened URL")
{
    pathOption,
    connectionStringOption
};

deleteCommand.SetHandler(async (path, connectionString) =>
{
    var shortUrlRepository = new ShortUrlRepository(await GetRedisConnection(connectionString));
    try
    {
        await shortUrlRepository.Delete(path);
        Console.WriteLine($"Shortened URL deleted.");
    }
    catch (Exception e)
    {
        Console.Error.WriteLine(e.Message);
    }
}, pathOption, connectionStringOption);

rootCommand.AddCommand(deleteCommand);

#endregion

#region Get Command

var getCommand = new Command("get", "Get a shortened URL")
{
    pathOption,
    connectionStringOption
};

getCommand.SetHandler(async (path, connectionString) =>
{
    var shortUrlRepository = new ShortUrlRepository(await GetRedisConnection(connectionString));
    try
    {
        var shortUrl = await shortUrlRepository.GetByPath(path);
        if (shortUrl == null)
            Console.Error.WriteLine($"Shortened URL for path '{path}' not found.");
        else
            Console.WriteLine($"Destination URL: {shortUrl.Destination}, Path: {path}");
    }
    catch (Exception e)
    {
        Console.Error.WriteLine(e.Message);
    }
}, pathOption, connectionStringOption);

rootCommand.AddCommand(getCommand);

#endregion

#region List Command

var listCommand = new Command("list", "List shortened URLs")
{
    connectionStringOption
};

listCommand.SetHandler(async (connectionString) =>
{
    var shortUrlRepository = new ShortUrlRepository(await GetRedisConnection(connectionString));
    try
    {
        var shortUrls = await shortUrlRepository.GetAll();
        foreach (var shortUrl in shortUrls)
        {
            Console.WriteLine($"Destination URL: {shortUrl.Destination}, Path: {shortUrl.Path}");
        }
    }
    catch (Exception e)
    {
        Console.Error.WriteLine(e.Message);
    }
}, connectionStringOption);

rootCommand.AddCommand(listCommand);

#endregion

return rootCommand.InvokeAsync(args).Result;
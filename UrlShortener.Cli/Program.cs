using System.CommandLine;
using System.Text.RegularExpressions;
using StackExchange.Redis;

#region Options
var destinationOption = new Option<string>(
    new []{"--destination-url", "-d"},
    description: "The URL that the shortened URL will forward to."
);
destinationOption.IsRequired = true;
destinationOption.AddValidator(result =>
{
    var destination = result.Tokens[0].Value;
    if (!Uri.IsWellFormedUriString(destination, UriKind.Absolute))
    {
        result.ErrorMessage = "Destination has to be a valid absolute URL.";
    }
});

var pathOption = new Option<string>(
    new []{"--path", "-p"},
    description: "The path used for the shortened URL."
);
pathOption.IsRequired = true;
pathOption.AddValidator(result =>
{
    var path = result.Tokens[0].Value;

    if (path.Length > 10)
    {
        result.ErrorMessage = "Path cannot be longer than 10 characters.";
        return;
    }

    var pathRegex = new Regex("^[a-zA-Z0-9_]*$");
    if (!pathRegex.IsMatch(path))
    {
        result.ErrorMessage = "Path can only contain alphanumeric characters and underscores.";
    }
});

var connectionStringOption = new Option<string?>(
    new []{"--connection-string", "-c"},
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

#region Create Command
var createCommand = new Command("create", "Create a shortened URL")
{
    destinationOption,
    pathOption,
    connectionStringOption
};

createCommand.SetHandler(async (destination, path, connectionString) =>
{
    var redisConnection = await ConnectionMultiplexer.ConnectAsync(
        connectionString ?? 
        envConnectionString ??
        throw new Exception("Missing connection string")
    );
    var redisDb = redisConnection.GetDatabase();

    if (await redisDb.KeyExistsAsync(path))
    {
        Console.WriteLine($"Path {path} is already in use.");
        return;
    }

    var urlWasSet = await redisDb.StringSetAsync(path, destination);
    Console.WriteLine(urlWasSet ? "Created shortened URL." : "Failed to create shortened URL.");
}, destinationOption, pathOption, connectionStringOption);

rootCommand.AddCommand(createCommand);
#endregion

#region Delet Command
var deleteCommand = new Command("delete", "Delete a shortened URL")
{
    pathOption,
    connectionStringOption
};

deleteCommand.SetHandler(async (path, connectionString) =>
{
    var redisConnection = await ConnectionMultiplexer.ConnectAsync(
        connectionString ?? 
        envConnectionString ??
        throw new Exception("Missing connection string")
    );
    var redisDb = redisConnection.GetDatabase();

    if (await redisDb.KeyExistsAsync(path) == false)
    {
        Console.WriteLine($"Path does not exist.");
        return;
    }

    var urlWasDeleted = await redisDb.KeyDeleteAsync(path);
    Console.WriteLine(urlWasDeleted ? "Deleted shortened URL." : "Failed to delete shortened URL.");
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
    var redisConnection = await ConnectionMultiplexer.ConnectAsync(
        connectionString ?? 
        envConnectionString ??
        throw new Exception("Missing connection string")
    );
    var redisDb = redisConnection.GetDatabase();

    if (await redisDb.KeyExistsAsync(path) == false)
    {
        Console.WriteLine($"Path does not exist.");
        return;
    }
    var redisValue = await redisDb.StringGetAsync(path);
    Console.WriteLine($"Destination URL: {redisValue.ToString()}, Path: {path}");
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
    var redisConnection = await ConnectionMultiplexer.ConnectAsync(
        connectionString ?? 
        envConnectionString ??
        throw new Exception("Missing connection string")
    );
    var redisServers = redisConnection.GetServers();
    var keys = new List<string>();
    foreach (var redisServer in redisServers)
    {
        await foreach (var redisKey in redisServer.KeysAsync())
        {
            var key = redisKey.ToString();
            if(keys.Contains(key)) continue;
            keys.Add(key);
        }
    }

    var redisDb = redisConnection.GetDatabase();

    foreach (var key in keys)
    {
        var redisValue = redisDb.StringGet(key);
        Console.WriteLine($"Destination URL: {redisValue.ToString()}, Path: {key}");
    }
    
}, connectionStringOption);

rootCommand.AddCommand(listCommand);
#endregion

return rootCommand.InvokeAsync(args).Result;
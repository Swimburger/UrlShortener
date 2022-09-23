namespace UrlShortener.Data;

using StackExchange.Redis;

public class ShortUrlRepository
{
    private readonly ConnectionMultiplexer redisConnection;
    private readonly IDatabase redisDatabase;

    public ShortUrlRepository(ConnectionMultiplexer redisConnection)
    {
        this.redisConnection = redisConnection;
        this.redisDatabase = redisConnection.GetDatabase();
    }

    public async Task Create(ShortUrl shortUrl)
    {
        if (await Exists(shortUrl.Path))
            throw new Exception($"Shortened URL with path '{shortUrl.Path}' already exists.");

        var urlWasSet = await redisDatabase.StringSetAsync(shortUrl.Path, shortUrl.Destination);
        if (!urlWasSet)
            throw new Exception($"Failed to create shortened URL.");
    }

    public async Task Delete(string path)
    {
        if (await Exists(path) == false)
            throw new Exception($"Shortened URL with path '{path}' does not exist.");

        var urlWasDeleted = await redisDatabase.KeyDeleteAsync(path);
        if (!urlWasDeleted)
            throw new Exception("Failed to delete shortened URL.");
    }

    public async Task<ShortUrl?> GetByPath(string path)
    {
        if (await Exists(path) == false)
            throw new Exception($"Shortened URL with path '{path}' does not exist.");

        var redisValue = await redisDatabase.StringGetAsync(path);
        if (redisValue.IsNullOrEmpty)
            return null;
        
        return new ShortUrl(redisValue.ToString(), path);
    }

    public async Task<List<ShortUrl>> GetAll()
    {
        var redisServers = redisConnection.GetServers();
        var keys = new List<string>();
        foreach (var redisServer in redisServers)
        {
            await foreach (var redisKey in redisServer.KeysAsync())
            {
                var key = redisKey.ToString();
                if (keys.Contains(key)) continue;
                keys.Add(key);
            }
        }

        var redisDb = redisConnection.GetDatabase();

        var shortUrls = new List<ShortUrl>();
        foreach (var key in keys)
        {
            var redisValue = redisDb.StringGet(key);
            shortUrls.Add(new ShortUrl(redisValue.ToString(), key));
        }

        return shortUrls;
    }

    public async Task<bool> Exists(string? path)
        => await redisDatabase.KeyExistsAsync(path);
}
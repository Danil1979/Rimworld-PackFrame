using PackFrame;

internal static class ModpackIndexChecks
{
    public static async Task Run(string root)
    {
        const string url = "https://example.com/packs/index.json";
        var directory = Path.Combine(root, "index-cache");
        var response = new TaskCompletionSource<ModpackIndex>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        var cache = new ModpackIndexCache(directory, _ =>
        {
            requests++;
            return response.Task;
        });
        var startup = cache.LoadAsync(url, refresh: true);
        var opening = cache.LoadAsync(url);
        Require(!startup.IsCompleted && ReferenceEquals(startup, opening) && requests == 1,
            "opening the collection page shares an unfinished startup fetch");
        response.SetResult(Index("old"));
        await startup;
        var path = Directory.GetFiles(directory, "*.json").Single();
        var original = File.ReadAllText(path);

        var offlineRequests = 0;
        var offline = new ModpackIndexCache(directory, _ =>
        {
            offlineRequests++;
            return Task.FromException<ModpackIndex>(new HttpRequestException("simulated offline"));
        });
        Require((await offline.LoadAsync(url)).Packs.Single().Id == "old" && offlineRequests == 0,
            "a new session displays persisted collection data without waiting for the network");
        try
        {
            await offline.LoadAsync(url, refresh: true);
            throw new Exception("Expected refresh failure");
        }
        catch (HttpRequestException) { }
        Require(File.ReadAllText(path) == original &&
            (await offline.LoadAsync(url)).Packs.Single().Id == "old",
            "failed refresh preserves the last good collection list");

        var gate = new TaskCompletionSource<ModpackIndex>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updating = new ModpackIndexCache(directory, _ => gate.Task);
        var refresh = updating.LoadAsync(url, refresh: true);
        var cached = updating.LoadAsync(url);
        Require(cached.IsCompletedSuccessfully && (await cached).Packs.Single().Id == "old" && !refresh.IsCompleted,
            "cached collection list is available while startup refresh is still running");
        gate.SetResult(Index("new"));
        await refresh;
        Require((await offline.LoadAsync(url)).Packs.Single().Id == "new",
            "successful startup refresh replaces the persisted collection list");

        var isolated = new ModpackIndexCache(directory, source => Task.FromResult(Index(source)));
        const string other = "https://example.com/other/index.json";
        Require((await isolated.LoadAsync(other)).Packs.Single().Id == other &&
            (await isolated.LoadAsync(url)).Packs.Single().Id == "new",
            "different collection sources have separate caches");

        File.WriteAllText(path, "broken JSON");
        Require((await isolated.LoadAsync(url)).Packs.Single().Id == url,
            "corrupt collection cache is fetched again and repaired");

        var blocked = Path.Combine(root, "blocked-index-cache");
        File.WriteAllText(blocked, "not a directory");
        var readOnly = new ModpackIndexCache(blocked, _ => Task.FromResult(Index("online")));
        Require((await readOnly.LoadAsync(url)).Packs.Single().Id == "online" &&
            (await readOnly.LoadAsync(url)).Packs.Single().Id == "online",
            "cache write failure does not prevent using the downloaded list");
        Require(ModpackService.StartupIndexUrl(null) == ModpackService.GiteeIndexUrl &&
            ModpackService.StartupIndexUrl(ModpackService.GitHubIndexUrl) == ModpackService.GiteeIndexUrl &&
            ModpackService.StartupIndexUrl(url) == url,
            "startup preload and collection window use the same existing source selection");
    }

    private static ModpackIndex Index(string id) => new() { Packs = new() { new ModpackEntry { Id = id } } };

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

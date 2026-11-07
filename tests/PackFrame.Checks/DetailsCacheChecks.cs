using PackFrame;

internal static class DetailsCacheChecks
{
    public static async Task Run(string root)
    {
        var folder = Path.Combine(root, "pack-details");
        var source = new Uri(ModpackService.GitHubIndexUrl);
        var entry = Entry();
        var calls = 0;
        var version = 1;
        var cache = new ModpackDetailsCache(folder, (_, indexUri, _) =>
        {
            calls++;
            return Task.FromResult(Pack(indexUri, version));
        });
        Require(cache.LoadLocal(entry, source) is null && calls == 0,
            "first pack visit has no cached details and performs no implicit fetch");
        var first = await cache.RefreshAsync(entry, source);
        var firstHash = first.ContentHash;
        Require(first.Pack.Name == "Original pack" && calls == 1,
            "first pack fetch stores validated details");
        Require(Enumerable.Range(0, 4).All(_ =>
                cache.LoadLocal(entry, source)?.Pack.Name == "Original pack" && calls == 1),
            "revisiting a pack reads saved details without another fetch");

        first.Pack.Name = "Changed by the detail view";
        first.Pack.PackageIds.Clear();
        first.Pack.Mods[0].Name = "Changed mod";
        var local = cache.LoadLocal(entry, source)!;
        Require(local.Pack.Name == "Original pack" &&
                local.Pack.PackageIds.SequenceEqual(new[] { "example.core" }) &&
                local.Pack.Mods.Single().Name == "Original mod",
            "mutating fetched detail models cannot change the cached pack");
        local.Pack.Description = "Changed local copy";
        local.Pack.ConfigFiles.Clear();
        Require(cache.LoadLocal(entry, source)?.Pack.Description == "original-description" &&
                cache.LoadLocal(entry, source)!.Pack.ConfigFiles.SequenceEqual(new[] { "ModsConfig.xml" }),
            "each cached pack visit receives an independent model");

        var restarted = new ModpackDetailsCache(folder, (_, _, _) =>
            throw new InvalidOperationException("Restart should read saved details"));
        Require(restarted.LoadLocal(entry, source)?.ContentHash == firstHash &&
                restarted.LoadLocal(entry, source)?.Pack.Name == "Original pack",
            "pack details survive restarting the app");
        version = 2;
        var updated = await cache.RefreshAsync(entry, source);
        Require(updated.ContentHash != firstHash &&
                new ModpackDetailsCache(folder).LoadLocal(entry, source)?.Pack.Name == "Updated pack",
            "a changed pack updates the saved details and content hash");

        await CheckSourceIdentity(root);
        await CheckFailuresAndCancellation(root);
        await CheckCorruptDisk(root);
    }

    private static async Task CheckSourceIdentity(string root)
    {
        var entry = Entry();
        var github = new Uri(ModpackService.GitHubIndexUrl);
        var gitee = new Uri(ModpackService.GiteeIndexUrl);
        var calls = 0;
        var cache = new ModpackDetailsCache(Path.Combine(root, "pack-details-sources"), (_, source, _) =>
        {
            calls++;
            return Task.FromResult(Pack(source, source == github ? 1 : 2));
        });
        Require(ModpackDetailsCache.GetKey(entry, github) == ModpackDetailsCache.GetKey(entry, gitee),
            "default catalog mirrors share a pack-details identity");
        await cache.RefreshAsync(entry, github);
        var alias = cache.LoadLocal(entry, gitee);
        Require(alias is not null && alias.Pack.Name == "Original pack" && calls == 1 &&
                alias.Pack.PackSource == gitee.AbsoluteUri &&
                alias.Pack.ConfigBaseUrl == "https://cdn.example.com/config/" &&
                alias.Pack.Image == "https://cdn.example.com/cover.png",
            "mirror failover reuses details with the requested source and resolved asset URLs");

        var customA = new Uri("https://example.com/first/index.json");
        var customB = new Uri("https://example.com/second/index.json");
        Require(ModpackDetailsCache.GetKey(entry, customA) != ModpackDetailsCache.GetKey(entry, customB) &&
                ModpackDetailsCache.GetKey(entry, customA) != ModpackDetailsCache.GetKey(entry, github),
            "pack-details identity separates custom catalogs and default mirrors");
        Require(cache.LoadLocal(entry, customA) is null,
            "same pack ID in another catalog cannot receive cached default details");
        await cache.RefreshAsync(entry, customA);
        Require(cache.LoadLocal(entry, customA)?.Pack.PackSource == customA.AbsoluteUri &&
                cache.LoadLocal(entry, customB) is null &&
                cache.LoadLocal(entry, github)?.Pack.Name == "Original pack",
            "custom pack details remain isolated when catalog sources change");
        var otherEntry = Entry();
        otherEntry.Id = "another-pack";
        otherEntry.File = "packs/another.json";
        Require(cache.LoadLocal(otherEntry, customA) is null,
            "visiting another pack does not show the previous pack's details");
    }

    private static async Task CheckFailuresAndCancellation(string root)
    {
        var source = new Uri(ModpackService.GitHubIndexUrl);
        var entry = Entry();
        var mode = "good";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var folder = Path.Combine(root, "pack-details-failed");
        var cache = new ModpackDetailsCache(folder, async (_, indexUri, token) =>
        {
            if (mode == "failed") throw new IOException("simulated offline pack server");
            if (mode == "wrong-source") return Pack(new Uri("https://other.example.com/index.json"), 2);
            if (mode == "cancel")
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return Pack(indexUri, 1);
        });
        var good = await cache.RefreshAsync(entry, source);
        mode = "failed";
        await ExpectFailure(() => cache.RefreshAsync(entry, source));
        Require(cache.LoadLocal(entry, source)?.ContentHash == good.ContentHash,
            "failed pack revalidation keeps the last valid details");
        mode = "wrong-source";
        await ExpectFailure(() => cache.RefreshAsync(entry, source));
        Require(new ModpackDetailsCache(folder).LoadLocal(entry, source)?.ContentHash == good.ContentHash,
            "pack result from the wrong source cannot overwrite saved details");
        mode = "cancel";
        using var cancellation = new CancellationTokenSource();
        var request = cache.RefreshAsync(entry, source, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        var failed = await ExpectFailure(() => request.WaitAsync(TimeSpan.FromSeconds(3)));
        Require(failed is OperationCanceledException &&
                cache.LoadLocal(entry, source)?.ContentHash == good.ContentHash,
            "cancelled pack refresh preserves details for the next visit");
    }

    private static async Task CheckCorruptDisk(string root)
    {
        var source = new Uri(ModpackService.GitHubIndexUrl);
        var entry = Entry();
        var folder = Path.Combine(root, "pack-details-corrupt");
        var cache = new ModpackDetailsCache(folder, (_, indexUri, _) => Task.FromResult(Pack(indexUri, 1)));
        await cache.RefreshAsync(entry, source);
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.WriteAllText(file, File.ReadAllText(file).Replace("original-description", "tampered-description"));
        }
        Require(new ModpackDetailsCache(folder).LoadLocal(entry, source) is null,
            "pack-details checksum mismatch is ignored on restart");
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.WriteAllText(file, "corrupt detail cache");
        }
        Require(new ModpackDetailsCache(folder).LoadLocal(entry, source) is null,
            "corrupt saved pack details fall back to normal first-visit loading");
    }

    private static ModpackEntry Entry() => new()
    {
        Id = "pack-cache",
        Name = "Pack cache test",
        File = "packs/cache.json"
    };

    private static ModpackDefinition Pack(Uri source, int version) => new()
    {
        Id = "pack-cache",
        Name = version == 1 ? "Original pack" : "Updated pack",
        Description = "original-description",
        PackSource = source.AbsoluteUri,
        ConfigBaseUrl = "https://cdn.example.com/config/",
        Image = "https://cdn.example.com/cover.png",
        ConfigFiles = new() { "ModsConfig.xml" },
        PackageIds = new() { "example.core" },
        Mods = new() { new ModpackMod { PackageId = "example.core", Name = "Original mod", Source = "local" } }
    };

    private static async Task<Exception> ExpectFailure(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception) { return exception; }
        throw new Exception("Expected pack-details refresh failure");
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

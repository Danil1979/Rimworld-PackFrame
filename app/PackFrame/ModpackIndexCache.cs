using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PackFrame;

internal sealed class ModpackIndexCache
{
    internal static ModpackIndexCache Shared { get; } = new(
        Path.Combine(AppPaths.FindPackRoot() ?? AppContext.BaseDirectory, "runtime", "modpack-index"),
        url => ModpackService.LoadIndexAsync(url));

    private readonly string _directory;
    private readonly Func<string, Task<ModpackIndex>> _load;
    private readonly Dictionary<string, Task<ModpackIndex>> _loads = new(StringComparer.Ordinal);

    internal ModpackIndexCache(string directory, Func<string, Task<ModpackIndex>> load)
    {
        _directory = directory;
        _load = load;
    }

    internal Task<ModpackIndex> LoadAsync(string url, bool refresh = false)
    {
        var normalized = ModpackService.NormalizeIndexUrl(url);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        var path = Path.Combine(_directory, hash + ".json");
        lock (_loads)
        {
            if (!refresh)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var cached = JsonSerializer.Deserialize(File.ReadAllText(path),
                            PackFrameJsonContext.Default.ModpackIndex);
                        if (cached?.Packs is not null) return Task.FromResult(cached);
                    }
                }
                catch (Exception exception)
                {
                    AppLog.Error("Read modpack index cache: " + path, exception);
                }
            }

            if (_loads.TryGetValue(normalized, out var pending) &&
                (!pending.IsCompleted || (!refresh && pending.IsCompletedSuccessfully)))
                return pending;

            return _loads[normalized] = FetchAsync(normalized, path);
        }
    }

    private async Task<ModpackIndex> FetchAsync(string url, string path)
    {
        var index = await _load(url).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);
            var json = JsonSerializer.Serialize(index, PackFrameJsonContext.Default.ModpackIndex);
            FileTransactions.WriteAtomic(path, temporary => File.WriteAllText(temporary, json));
        }
        catch (Exception exception)
        {
            // Cache write failure must not discard a successful network response.
            AppLog.Error("Save modpack index cache: " + path, exception);
        }
        return index;
    }
}

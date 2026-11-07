using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PackFrame;

internal sealed record ModpackDetailsSnapshot(ModpackDefinition Pack, string ContentHash);

internal sealed class ModpackDetailsDocument
{
    public string CacheKey { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Json { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}

// Browsing can reuse these resolved details. Install and update checks continue to
// force a fresh manifest read when they require authoritative data.
internal sealed class ModpackDetailsCache
{
    private const int MaxEntries = 32;
    private const int MaxDetailsBytes = 4 * 1024 * 1024;
    private const int MaxDocumentBytes = MaxDetailsBytes * 3;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly string _cacheDirectory;
    private readonly Func<ModpackEntry, Uri, CancellationToken, Task<ModpackDefinition>> _loader;
    private readonly object _memoryGate = new();
    private readonly Dictionary<string, ModpackDetailsDocument> _memory = new();
    private readonly Queue<string> _memoryOrder = new();

    public static ModpackDetailsCache Shared { get; } = new(
        Path.Combine(AppContext.BaseDirectory, "runtime", "pack-details")
    );

    internal ModpackDetailsCache(
        string cacheDirectory,
        Func<ModpackEntry, Uri, CancellationToken, Task<ModpackDefinition>>? loader = null
    )
    {
        _cacheDirectory = cacheDirectory;
        _loader = loader ?? ModpackService.LoadPackAsync;
    }

    internal static string GetKey(ModpackEntry entry, Uri indexUri)
    {
        var source = GetSourceIdentity(indexUri);
        var parts = new[]
        {
            source, entry.Id ?? string.Empty, entry.File ?? string.Empty,
            entry.GiteeFile ?? string.Empty, entry.Name ?? string.Empty,
            entry.Summary ?? string.Empty, entry.Image ?? string.Empty,
            entry.ModCount.ToString(CultureInfo.InvariantCulture)
        };
        return Hash(string.Concat(parts.Select(part => part.Length.ToString(CultureInfo.InvariantCulture) + ":" + part)));
    }

    public ModpackDetailsSnapshot? LoadLocal(ModpackEntry entry, Uri indexUri)
    {
        var key = GetKey(entry, indexUri);
        lock (_memoryGate)
        {
            if (_memory.TryGetValue(key, out var remembered))
                return Snapshot(remembered, indexUri);
        }

        try
        {
            var path = CachePath(key);
            if (!File.Exists(path) || new FileInfo(path).Length > MaxDocumentBytes) return null;
            var saved = JsonSerializer.Deserialize(
                File.ReadAllText(path, Utf8), PackFrameJsonContext.Default.ModpackDetailsDocument
            );
            if (saved is null || saved.CacheKey != key ||
                Utf8.GetByteCount(saved.Json) > MaxDetailsBytes ||
                !string.Equals(Hash(saved.Json), saved.ContentHash, StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(saved.SourceUrl, UriKind.Absolute, out var savedSource) ||
                GetKey(entry, savedSource) != key) return null;
            ValidatePack(ParsePack(saved.Json), savedSource);
            Remember(saved);
            return Snapshot(saved, indexUri);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            return null;
        }
    }

    public async Task<ModpackDetailsSnapshot> RefreshAsync(
        ModpackEntry entry,
        Uri indexUri,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = GetKey(entry, indexUri);
        var pack = await _loader(entry, indexUri, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePack(pack, indexUri);
        var json = JsonSerializer.Serialize(pack, PackFrameJsonContext.Default.ModpackDefinition);
        if (Utf8.GetByteCount(json) > MaxDetailsBytes)
            throw new InvalidOperationException("Modpack details exceed the cache size limit.");
        var saved = new ModpackDetailsDocument
        {
            CacheKey = key,
            SourceUrl = indexUri.AbsoluteUri,
            Json = json,
            ContentHash = Hash(json)
        };
        cancellationToken.ThrowIfCancellationRequested();
        Remember(saved);
        SaveCache(saved);
        return Snapshot(saved, indexUri);
    }

    private void Remember(ModpackDetailsDocument saved)
    {
        lock (_memoryGate)
        {
            if (!_memory.ContainsKey(saved.CacheKey))
            {
                while (_memory.Count >= MaxEntries)
                    _memory.Remove(_memoryOrder.Dequeue());
                _memoryOrder.Enqueue(saved.CacheKey);
            }
            _memory[saved.CacheKey] = saved;
        }
    }

    private void SaveCache(ModpackDetailsDocument saved)
    {
        try
        {
            var json = JsonSerializer.Serialize(saved, PackFrameJsonContext.Default.ModpackDetailsDocument);
            if (Utf8.GetByteCount(json) > MaxDocumentBytes) return;
            Directory.CreateDirectory(_cacheDirectory);
            var path = CachePath(saved.CacheKey);
            FileTransactions.WriteAtomic(path, temporary => File.WriteAllText(temporary, json, Utf8));
            foreach (var stale in Directory.EnumerateFiles(_cacheDirectory, "*.json")
                .Where(IsCacheFile)
                .Select(file => new FileInfo(file))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(MaxEntries))
            {
                if (!string.Equals(stale.FullName, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    File.Delete(stale.FullName);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Save modpack details cache", exception);
        }
    }

    private static bool IsCacheFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Length == 64 && name.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private string CachePath(string key) => Path.Combine(_cacheDirectory, key + ".json");

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(value)));

    private static string GetSourceIdentity(Uri indexUri)
    {
        if (!indexUri.IsAbsoluteUri || indexUri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Modpack index URI must use HTTP or HTTPS.", nameof(indexUri));
        var source = indexUri.AbsoluteUri;
        return source.Equals(ModpackService.GiteeIndexUrl, StringComparison.OrdinalIgnoreCase) ||
            source.Equals(ModpackService.GitHubIndexUrl, StringComparison.OrdinalIgnoreCase)
                ? ModpackService.GitHubIndexUrl
                : source;
    }

    private static ModpackDefinition ParsePack(string json) =>
        JsonSerializer.Deserialize(json, PackFrameJsonContext.Default.ModpackDefinition)
        ?? throw new InvalidOperationException("Cached modpack details are empty.");

    private static void ValidatePack(ModpackDefinition pack, Uri indexUri)
    {
        if (!string.Equals(pack.PackSource, indexUri.AbsoluteUri, StringComparison.Ordinal) ||
            pack.Mods is null || pack.PackageIds is null || pack.ConfigFiles is null ||
            pack.Mods.Any(mod => mod is null))
            throw new InvalidOperationException("Modpack details do not match their catalog source.");
        foreach (var value in new[] { pack.Image, pack.ConfigBaseUrl, pack.ModsArchive, pack.ModsArchiveR2 })
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !Uri.TryCreate(value, UriKind.Absolute, out _))
                throw new InvalidOperationException("Cached modpack details contain an unresolved URL.");
        }
    }

    private static ModpackDetailsSnapshot Snapshot(ModpackDetailsDocument saved, Uri requestedSource)
    {
        var pack = ParsePack(saved.Json);
        // The built-in mirrors are one catalog. Retain resolved resource URLs,
        // while preserving the selected source's existing binding semantics.
        pack.PackSource = requestedSource.AbsoluteUri;
        return new ModpackDetailsSnapshot(pack, saved.ContentHash);
    }
}

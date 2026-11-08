using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PackFrame;

internal sealed record ModpackCatalogSnapshot(ModpackIndex Index, Uri IndexUri, string ContentHash, bool IsBundled = false);

internal sealed class ModpackCatalogDocument
{
    public string RequestedUrl { get; set; } = string.Empty;
    public string CurrentSourceUrl { get; set; } = string.Empty;
    public Dictionary<string, ModpackCatalogSource> Sources { get; set; } = new();
}

internal sealed class ModpackCatalogSource
{
    public string Json { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string? ETag { get; set; }
    public DateTimeOffset? LastModified { get; set; }
}

internal sealed class ModpackCatalogCache
{
    private const int MaxCatalogBytes = 1024 * 1024;
    private const int MaxCacheBytes = MaxCatalogBytes * 4;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly string _bundledJson;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _inflightGate = new();
    private readonly Dictionary<string, InflightRefresh> _inflight = new(StringComparer.Ordinal);

    private sealed class InflightRefresh
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public Task<ModpackCatalogSnapshot> Task { get; set; } = null!;
        public int Waiters { get; set; }
    }

    public static ModpackCatalogCache Shared { get; } = new(
        new HttpClient(),
        Path.Combine(AppContext.BaseDirectory, "runtime", "catalog")
    );

    internal ModpackCatalogCache(HttpClient http, string cacheDirectory, string? bundledJson = null)
    {
        _http = http;
        _cacheDirectory = cacheDirectory;
        _bundledJson = bundledJson ?? ReadEmbeddedCatalog();
    }

    public ModpackCatalogSnapshot? LoadLocal(string url)
    {
        var normalized = ModpackService.NormalizeIndexUrl(url);
        var candidates = GetCandidates(normalized);
        var saved = ReadCache(normalized, candidates);
        if (saved is not null)
        {
            return Snapshot(saved.CurrentSourceUrl, saved.Sources[saved.CurrentSourceUrl]);
        }

        return IsKnownIndex(normalized)
            ? new ModpackCatalogSnapshot(ParseIndex(_bundledJson), new Uri(normalized), Hash(_bundledJson), IsBundled: true)
            : null;
    }

    public async Task<ModpackCatalogSnapshot> RefreshAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = ModpackService.NormalizeIndexUrl(url);
        InflightRefresh operation;
        lock (_inflightGate)
        {
            if (!_inflight.TryGetValue(normalized, out operation!) || operation.Task.IsCompleted)
            {
                operation = new InflightRefresh();
                operation.Task = RefreshCoreAsync(normalized, operation.Cancellation.Token);
                _inflight[normalized] = operation;
            }
            operation.Waiters++;
        }

        try
        {
            // Startup preloading and an open window can share HTTP work without
            // sharing caller cancellation or exposing the same mutable index.
            var snapshot = await operation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return snapshot with
            {
                Index = ParseIndex(JsonSerializer.Serialize(snapshot.Index, PackFrameJsonContext.Default.ModpackIndex))
            };
        }
        finally
        {
            ReleaseRefresh(normalized, operation);
        }
    }

    private void ReleaseRefresh(string normalized, InflightRefresh operation)
    {
        lock (_inflightGate)
        {
            operation.Waiters--;
            if (operation.Waiters != 0) return;
            if (_inflight.TryGetValue(normalized, out var current) && ReferenceEquals(current, operation))
                _inflight.Remove(normalized);
        }

        // With no callers left, stop queued work and prevent mirror fallback.
        // Disposal waits for the shared task to finish using its token.
        if (!operation.Task.IsCompleted) operation.Cancellation.Cancel();
        _ = operation.Task.ContinueWith(completed =>
        {
            _ = completed.Exception;
            operation.Cancellation.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task<ModpackCatalogSnapshot> RefreshCoreAsync(
        string normalized,
        CancellationToken cancellationToken
    )
    {
        var candidates = GetCandidates(normalized);
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var saved = ReadCache(normalized, candidates) ?? new ModpackCatalogDocument
            {
                RequestedUrl = normalized
            };
            Exception? lastError = null;
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    saved.Sources.TryGetValue(candidate, out var previous);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    using var request = new HttpRequestMessage(HttpMethod.Get, candidate);
                    request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                    var conditional = false;
                    if (EntityTagHeaderValue.TryParse(previous?.ETag, out var etag))
                    {
                        request.Headers.IfNoneMatch.Add(etag);
                        conditional = true;
                    }
                    else if (previous?.LastModified is { } modified)
                    {
                        request.Headers.IfModifiedSince = modified;
                        conditional = true;
                    }

                    using var response = await _http.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, timeout.Token
                    ).ConfigureAwait(false);
                    ModpackCatalogSource current;
                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        if (previous is null || !conditional)
                        {
                            throw new InvalidOperationException("Catalog server returned 304 without a validated local copy.");
                        }
                        current = new ModpackCatalogSource
                        {
                            Json = previous.Json,
                            ContentHash = previous.ContentHash,
                            ETag = response.Headers.ETag?.ToString() ?? previous.ETag,
                            LastModified = response.Content.Headers.LastModified ?? previous.LastModified
                        };
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                        var json = await ReadLimitedJsonAsync(response.Content, timeout.Token).ConfigureAwait(false);
                        ParseIndex(json);
                        current = new ModpackCatalogSource
                        {
                            Json = json,
                            ContentHash = Hash(json),
                            ETag = response.Headers.ETag?.ToString(),
                            LastModified = response.Content.Headers.LastModified
                        };
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    saved.Sources[candidate] = current;
                    saved.CurrentSourceUrl = candidate;
                    SaveCache(normalized, saved);
                    return Snapshot(candidate, current);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or
                    OperationCanceledException or JsonException or InvalidOperationException or DecoderFallbackException)
                {
                    lastError = exception;
                }
            }
            throw lastError ?? new InvalidOperationException("Modpack catalog could not be loaded.");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private ModpackCatalogDocument? ReadCache(string normalized, IReadOnlyList<string> candidates)
    {
        try
        {
            var path = CachePath(normalized);
            if (!File.Exists(path) || new FileInfo(path).Length > MaxCacheBytes) return null;
            var saved = JsonSerializer.Deserialize(
                File.ReadAllText(path, Utf8), PackFrameJsonContext.Default.ModpackCatalogDocument
            );
            if (saved is null || saved.RequestedUrl != normalized || saved.Sources is null ||
                saved.Sources.Count > candidates.Count || !candidates.Contains(saved.CurrentSourceUrl) ||
                !saved.Sources.ContainsKey(saved.CurrentSourceUrl)) return null;
            foreach (var pair in saved.Sources)
            {
                if (!candidates.Contains(pair.Key) || pair.Value is null ||
                    Utf8.GetByteCount(pair.Value.Json) > MaxCatalogBytes ||
                    !string.Equals(Hash(pair.Value.Json), pair.Value.ContentHash, StringComparison.OrdinalIgnoreCase))
                    return null;
                ParseIndex(pair.Value.Json);
            }
            return saved;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            JsonException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private void SaveCache(string normalized, ModpackCatalogDocument saved)
    {
        try
        {
            var json = JsonSerializer.Serialize(saved, PackFrameJsonContext.Default.ModpackCatalogDocument);
            if (Utf8.GetByteCount(json) > MaxCacheBytes) return;
            Directory.CreateDirectory(_cacheDirectory);
            FileTransactions.WriteAtomic(CachePath(normalized), temporary => File.WriteAllText(temporary, json, Utf8));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Save modpack catalog cache", exception);
        }
    }

    private string CachePath(string normalized) => Path.Combine(_cacheDirectory, Hash(normalized) + ".json");

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(value)));

    private static ModpackCatalogSnapshot Snapshot(string source, ModpackCatalogSource entry) =>
        new(ParseIndex(entry.Json), new Uri(source), entry.ContentHash);

    private static ModpackIndex ParseIndex(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("packs", out var packs) || packs.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Modpack catalog has no packs array.");
        var index = JsonSerializer.Deserialize(json, PackFrameJsonContext.Default.ModpackIndex)
            ?? throw new InvalidOperationException("Modpack catalog is empty.");
        if (index.Packs.Any(entry => entry is null))
            throw new InvalidOperationException("Modpack catalog contains an invalid entry.");
        return index;
    }

    private static async Task<string> ReadLimitedJsonAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxCatalogBytes)
            throw new InvalidOperationException("Modpack catalog exceeds the size limit.");
        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + count > MaxCatalogBytes)
                throw new InvalidOperationException("Modpack catalog exceeds the size limit.");
            buffer.Write(chunk, 0, count);
        }
        return Utf8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static bool IsKnownIndex(string url) =>
        url.Equals(ModpackService.GiteeIndexUrl, StringComparison.OrdinalIgnoreCase) ||
        url.Equals(ModpackService.GitHubIndexUrl, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> GetCandidates(string normalized)
    {
        var uri = new Uri(normalized);
        if (uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Modpack catalog URL must use HTTP or HTTPS.");
        if (IsKnownIndex(normalized))
            return normalized.Equals(ModpackService.GiteeIndexUrl, StringComparison.OrdinalIgnoreCase)
                ? new[] { ModpackService.GiteeIndexUrl, ModpackService.GitHubIndexUrl }
                : new[] { ModpackService.GitHubIndexUrl, ModpackService.GiteeIndexUrl };
        if (uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("/main/index.json", StringComparison.OrdinalIgnoreCase))
            return new[] { normalized, normalized.Replace("/main/index.json", "/master/index.json", StringComparison.OrdinalIgnoreCase) };
        return new[] { normalized };
    }

    private static string ReadEmbeddedCatalog()
    {
        using var stream = typeof(ModpackCatalogCache).Assembly.GetManifestResourceStream("PackFrame.Assets.ModpackCatalog.json")
            ?? throw new InvalidOperationException("Bundled modpack catalog is missing.");
        using var reader = new StreamReader(stream, Utf8);
        return reader.ReadToEnd();
    }
}

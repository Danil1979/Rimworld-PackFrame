using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PackFrame;

internal static class CatalogChecks
{
    private const string Bundled = "{\"schemaVersion\":1,\"packs\":[{\"id\":\"bundled\",\"name\":\"Bundled pack\",\"file\":\"packs/bundled.json\"}]}";
    private const string First = "{\"schemaVersion\":1,\"packs\":[{\"id\":\"live-one\",\"name\":\"First live pack\",\"file\":\"packs/first.json\"}]}";
    private const string Updated = "{\"schemaVersion\":1,\"packs\":[{\"id\":\"live-two\",\"name\":\"Updated live pack\",\"file\":\"packs/second.json\"}]}";

    public static async Task Run(string root)
    {
        const string customSource = "https://example.com/custom/index.json";
        Require(ModpackService.StartupIndexUrl(null) == ModpackService.GiteeIndexUrl &&
                ModpackService.StartupIndexUrl(ModpackService.GitHubIndexUrl) == ModpackService.GitHubIndexUrl &&
                ModpackService.StartupIndexUrl(customSource) == customSource,
            "startup preload preserves the selected GitHub or custom catalog source");
        var folder = Path.Combine(root, "catalog");
        using var handler = new CatalogHttp();
        using var client = new HttpClient(handler);
        var cache = new ModpackCatalogCache(client, folder, Bundled);
        var initial = cache.LoadLocal(ModpackService.GiteeIndexUrl);
        Require(initial?.Index.Packs.Single().Id == "bundled" && handler.Requests == 0,
            "bundled catalog opens without a network request");
        Require(cache.LoadLocal("https://example.com/custom/index.json") is null,
            "custom catalog never receives the default bundled catalog");

        handler.Respond = (_, _) => Task.FromResult(Json(First, "\"first\""));
        var first = await cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        Require(first.Index.Packs.Single().Id == "live-one" &&
                first.ContentHash != initial!.ContentHash,
            "validated catalog update replaces the bundled content and hash");

        var responseBody = new UnreadableContent();
        handler.Respond = (request, _) =>
        {
            Require(request.Headers.IfNoneMatch.Single().Tag == "\"first\"",
                "catalog update check sends the saved ETag");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)
            {
                Content = responseBody
            });
        };
        var unchanged = await cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        Require(unchanged.ContentHash == first.ContentHash &&
                unchanged.Index.Packs.Single().Id == "live-one" && !responseBody.ReadAttempted,
            "304 response reuses the local catalog without downloading its body");

        var restarted = new ModpackCatalogCache(client, folder, Bundled);
        var requestCount = handler.Requests;
        Require(restarted.LoadLocal(ModpackService.GitHubIndexUrl)?.ContentHash == first.ContentHash &&
                handler.Requests == requestCount,
            "validated catalog survives a restart and loads without HTTP");
        handler.Respond = (request, _) =>
        {
            Require(request.Headers.IfNoneMatch.Single().Tag == "\"first\"",
                "catalog validators survive a restart");
            return Task.FromResult(Json(Updated, "\"second\""));
        };
        var updated = await restarted.RefreshAsync(ModpackService.GitHubIndexUrl);
        Require(updated.ContentHash != first.ContentHash &&
                new ModpackCatalogCache(client, folder, Bundled)
                    .LoadLocal(ModpackService.GitHubIndexUrl)?.Index.Packs.Single().Id == "live-two",
            "changed catalog content is saved with a new hash");

        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await ExpectFailure(() => restarted.RefreshAsync(ModpackService.GitHubIndexUrl));
        Require(restarted.LoadLocal(ModpackService.GitHubIndexUrl)?.ContentHash == updated.ContentHash,
            "offline catalog checks preserve the last valid catalog");
        handler.Respond = (_, _) => Task.FromResult(Json("{", "\"invalid\""));
        await ExpectFailure(() => restarted.RefreshAsync(ModpackService.GitHubIndexUrl));
        Require(new ModpackCatalogCache(client, folder, Bundled)
                .LoadLocal(ModpackService.GitHubIndexUrl)?.ContentHash == updated.ContentHash,
            "malformed catalog updates cannot overwrite the saved catalog");

        await CheckMirrorFallback(root);
        await CheckCustomIsolation(root);
        await CheckLastModified(root);
        await CheckCorruptCache(root);
        await CheckCancellation(root);
        await CheckConcurrentRefresh(root);
        await CheckAllWaitersCancelled(root);
        await CheckReadOnlyCache(root);
    }

    private static async Task CheckMirrorFallback(string root)
    {
        using var handler = new CatalogHttp();
        using var client = new HttpClient(handler);
        var cache = new ModpackCatalogCache(client, Path.Combine(root, "catalog-mirror"), Bundled);
        handler.Respond = (request, _) => Task.FromResult(
            request.RequestUri!.AbsoluteUri == ModpackService.GiteeIndexUrl
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json(First, "\"github-only\""));
        var snapshot = await cache.RefreshAsync(ModpackService.GiteeIndexUrl);
        Require(snapshot.IndexUri.AbsoluteUri == ModpackService.GitHubIndexUrl &&
                new Uri(snapshot.IndexUri, snapshot.Index.Packs.Single().File!).AbsoluteUri ==
                    "https://raw.githubusercontent.com/ZXUW/PackFrame-Modpacks/main/packs/first.json",
            "mirror fallback preserves the source URI for relative pack references");
        handler.Respond = (request, _) =>
        {
            if (request.RequestUri!.AbsoluteUri == ModpackService.GiteeIndexUrl)
            {
                Require(!request.Headers.IfNoneMatch.Any(),
                    "GitHub validators are never sent to the Gitee mirror");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            Require(request.Headers.IfNoneMatch.Single().Tag == "\"github-only\"",
                "mirror update checks use the validator for the responding source");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        };
        var repeated = await cache.RefreshAsync(ModpackService.GiteeIndexUrl);
        Require(repeated.ContentHash == snapshot.ContentHash,
            "unchanged mirror response retains the validated catalog");
    }

    private static async Task CheckCustomIsolation(string root)
    {
        using var handler = new CatalogHttp { Respond = (_, _) => Task.FromResult(Json(First, "\"custom\"")) };
        using var client = new HttpClient(handler);
        var folder = Path.Combine(root, "catalog-custom");
        var cache = new ModpackCatalogCache(client, folder, Bundled);
        const string source = "https://example.com/first/index.json";
        var custom = await cache.RefreshAsync(source);
        var restarted = new ModpackCatalogCache(client, folder, Bundled);
        Require(restarted.LoadLocal(source)?.ContentHash == custom.ContentHash &&
                restarted.LoadLocal("https://example.com/second/index.json") is null &&
                restarted.LoadLocal(ModpackService.GitHubIndexUrl)?.Index.Packs.Single().Id == "bundled",
            "custom catalog caches stay isolated from other URLs and the default bundle");
        Require(new Uri(custom.IndexUri, custom.Index.Packs.Single().File!).AbsoluteUri ==
                "https://example.com/first/packs/first.json",
            "custom catalog retains its base URI for relative pack references");
    }

    private static async Task CheckLastModified(string root)
    {
        var modified = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        using var handler = new CatalogHttp();
        using var client = new HttpClient(handler);
        var cache = new ModpackCatalogCache(client, Path.Combine(root, "catalog-modified"), Bundled);
        handler.Respond = (_, _) =>
        {
            var response = Json(First);
            response.Content.Headers.LastModified = modified;
            return Task.FromResult(response);
        };
        var first = await cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        handler.Respond = (request, _) =>
        {
            Require(!request.Headers.IfNoneMatch.Any() && request.Headers.IfModifiedSince == modified,
                "catalog update checks use Last-Modified when ETag is unavailable");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        };
        var unchanged = await cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        Require(unchanged.ContentHash == first.ContentHash,
            "Last-Modified validation reuses the unchanged local catalog");
    }

    private static async Task CheckCorruptCache(string root)
    {
        using var handler = new CatalogHttp { Respond = (_, _) => Task.FromResult(Json(First, "\"valid\"")) };
        using var client = new HttpClient(handler);
        var folder = Path.Combine(root, "catalog-corrupt");
        var cache = new ModpackCatalogCache(client, folder, Bundled);
        await cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.WriteAllText(file, File.ReadAllText(file).Replace("live-one", "tampered"));
        }
        var fresh = new ModpackCatalogCache(client, folder, Bundled);
        Require(fresh.LoadLocal(ModpackService.GitHubIndexUrl)?.Index.Packs.Single().Id == "bundled",
            "catalog hash mismatch falls back to the embedded catalog");
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.WriteAllText(file, "invalid cache");
        }
        Require(new ModpackCatalogCache(client, folder, Bundled)
                .LoadLocal(ModpackService.GitHubIndexUrl)?.Index.Packs.Single().Id == "bundled",
            "corrupt catalog cache does not prevent opening the bundled catalog");
    }

    private static async Task CheckCancellation(string root)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CatalogHttp
        {
            Respond = async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Unexpected uncancelled catalog request");
            }
        };
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var cache = new ModpackCatalogCache(client, Path.Combine(root, "catalog-cancel"), Bundled);
        var request = cache.RefreshAsync(ModpackService.GitHubIndexUrl, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();
        var failed = await ExpectFailure(() => request.WaitAsync(TimeSpan.FromSeconds(3)));
        Require(failed is OperationCanceledException && handler.Requests == 1 &&
                cache.LoadLocal(ModpackService.GitHubIndexUrl)?.Index.Packs.Single().Id == "bundled",
            "cancelled catalog checks stop without fallback or corrupting local content");
    }

    private static async Task CheckConcurrentRefresh(string root)
    {
        using var handler = new CatalogHttp { Respond = (_, _) => Task.FromResult(Json(First, "\"existing\"")) };
        using var client = new HttpClient(handler);
        var cache = new ModpackCatalogCache(client, Path.Combine(root, "catalog-shared"), Bundled);
        var saved = await cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        var requestCount = handler.Requests;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sharedCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Respond = async (_, token) =>
        {
            started.TrySetResult();
            try { await release.Task.WaitAsync(token); }
            catch (OperationCanceledException)
            {
                sharedCancelled.TrySetResult();
                throw;
            }
            return Json(Updated, "\"shared-update\"");
        };
        using var preloadCancellation = new CancellationTokenSource();
        var preload = cache.RefreshAsync(ModpackService.GitHubIndexUrl, preloadCancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var window = cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        Require(cache.LoadLocal(ModpackService.GitHubIndexUrl)?.ContentHash == saved.ContentHash,
            "saved catalog remains immediately available during a shared startup refresh");
        preloadCancellation.Cancel();
        var cancelled = await ExpectFailure(() => preload.WaitAsync(TimeSpan.FromSeconds(3)));
        Require(cancelled is OperationCanceledException && !sharedCancelled.Task.IsCompleted && !window.IsCompleted,
            "cancelling startup preload leaves the collection window's shared request running");
        release.TrySetResult();
        var refreshed = await window.WaitAsync(TimeSpan.FromSeconds(3));
        Require(handler.Requests == requestCount + 1 && refreshed.Index.Packs.Single().Id == "live-two" &&
                refreshed.ContentHash != saved.ContentHash,
            "simultaneous startup and window checks use one HTTP operation and publish its result");
    }

    private static async Task CheckAllWaitersCancelled(string root)
    {
        using var handler = new CatalogHttp { Respond = (_, _) => Task.FromResult(Json(First, "\"existing\"")) };
        using var client = new HttpClient(handler);
        var folder = Path.Combine(root, "catalog-shared-cancel");
        var cache = new ModpackCatalogCache(client, folder, Bundled);
        var saved = await cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        var requestCount = handler.Requests;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sharedCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Respond = async (_, token) =>
        {
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException)
            {
                sharedCancelled.TrySetResult();
                throw;
            }
            throw new InvalidOperationException("Unexpected uncancelled shared catalog request");
        };
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var first = cache.RefreshAsync(ModpackService.GitHubIndexUrl, firstCancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = cache.RefreshAsync(ModpackService.GitHubIndexUrl, secondCancellation.Token);
        firstCancellation.Cancel();
        secondCancellation.Cancel();
        var firstFailure = await ExpectFailure(() => first.WaitAsync(TimeSpan.FromSeconds(3)));
        var secondFailure = await ExpectFailure(() => second.WaitAsync(TimeSpan.FromSeconds(3)));
        await sharedCancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Require(firstFailure is OperationCanceledException && secondFailure is OperationCanceledException &&
                handler.Requests == requestCount + 1 &&
                new ModpackCatalogCache(client, folder, Bundled)
                    .LoadLocal(ModpackService.GitHubIndexUrl)?.ContentHash == saved.ContentHash,
            "cancelling all shared callers stops HTTP without fallback or damaging the saved catalog");
        handler.Respond = (_, _) => Task.FromResult(Json(Updated, "\"next-update\""));
        var next = await cache.RefreshAsync(ModpackService.GitHubIndexUrl).WaitAsync(TimeSpan.FromSeconds(3));
        Require(handler.Requests == requestCount + 2 && next.Index.Packs.Single().Id == "live-two",
            "a new catalog check starts a fresh request after all earlier callers cancel");
    }

    private static async Task CheckReadOnlyCache(string root)
    {
        var cachePath = Path.Combine(root, "catalog-unwritable");
        File.WriteAllText(cachePath, "a file prevents creating the cache directory");
        using var handler = new CatalogHttp { Respond = (_, _) => Task.FromResult(Json(First)) };
        using var client = new HttpClient(handler);
        var cache = new ModpackCatalogCache(client, cachePath, Bundled);
        var result = await cache.RefreshAsync(ModpackService.GitHubIndexUrl);
        Require(result.Index.Packs.Single().Id == "live-one",
            "unwritable catalog cache does not discard a valid network result");
    }

    private static HttpResponseMessage Json(string json, string? etag = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (etag is not null) response.Headers.ETag = new EntityTagHeaderValue(etag);
        return response;
    }

    private static async Task<Exception> ExpectFailure(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception) { return exception; }
        throw new Exception("Expected catalog refresh failure");
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }

    private sealed class CatalogHttp : HttpMessageHandler
    {
        public int Requests;
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond =
            (_, _) => throw new InvalidOperationException("Unexpected catalog network request");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            return Respond(request, token);
        }
    }

    private sealed class UnreadableContent : HttpContent
    {
        public bool ReadAttempted;
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempted = true;
            throw new InvalidOperationException("304 content must not be read");
        }
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

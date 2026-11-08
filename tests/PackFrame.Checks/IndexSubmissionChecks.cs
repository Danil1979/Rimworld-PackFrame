using System.Net;
using System.Text;
using System.Text.Json;
using PackFrame;

internal static class IndexSubmissionChecks
{
    public static async Task Run()
    {
        CheckDestinationRetries();
        await CheckManifestUrls();
    }

    private static void CheckDestinationRetries()
    {
        const string github = "https://github.com/owner/pack";
        const string gitee = "https://gitee.com/owner/pack";
        const string githubPr = "https://github.com/catalog/index/pull/1";
        const string giteePr = "https://gitee.com/catalog/index/pulls/2";
        const string manifest = "https://raw.githubusercontent.com/owner/pack/master/pack.json";
        var state = new ExportIndexSubmissionState();
        Require(!state.HasPending && !state.AllSubmitted, "empty index targets are not reported as submitted");
        state.SetTarget(IndexDestination.GitHub, github);
        state.SetTarget(IndexDestination.Gitee, gitee);
        Require(state.NeedsSubmission(IndexDestination.GitHub) && state.NeedsSubmission(IndexDestination.Gitee),
            "both new index destinations initially need submission");
        state.MarkSuccess(IndexDestination.GitHub, github, githubPr, manifest);
        Require(!state.NeedsSubmission(IndexDestination.GitHub) && state.NeedsSubmission(IndexDestination.Gitee) &&
            state.SuccessUrl(IndexDestination.GitHub) == githubPr && state.SuccessManifestUrl(IndexDestination.GitHub) == manifest,
            "partial index success retains its PR and verified manifest while the failed destination remains retryable");

        var submitted = new List<IndexDestination>();
        for (var retry = 0; retry < 2; retry++)
        {
            foreach (var destination in new[] { IndexDestination.GitHub, IndexDestination.Gitee })
            {
                if (!state.NeedsSubmission(destination)) continue;
                submitted.Add(destination);
                state.MarkSuccess(destination, destination == IndexDestination.GitHub ? github : gitee,
                    destination == IndexDestination.GitHub ? githubPr : giteePr);
            }
        }
        Require(submitted.SequenceEqual(new[] { IndexDestination.Gitee }) && state.AllSubmitted,
            "repeated index retries submit only the failed destination and never duplicate successful submissions");
        state.SetTarget(IndexDestination.GitHub, "https://github.com/owner/other-pack");
        Require(state.NeedsSubmission(IndexDestination.GitHub) && !state.NeedsSubmission(IndexDestination.Gitee) &&
            state.SuccessUrl(IndexDestination.Gitee) == giteePr,
            "changing the GitHub repository resets only the GitHub destination");
        state.MarkSuccess(IndexDestination.GitHub, github, githubPr, manifest);
        Require(state.NeedsSubmission(IndexDestination.GitHub),
            "a completed request for an old repository cannot mark a newly selected repository as submitted");
        state.SetTarget(IndexDestination.GitHub, github + ".git/");
        Require(state.AllSubmitted && state.SuccessUrl(IndexDestination.GitHub) == githubPr,
            "reselecting a successful repository restores its result instead of publishing another PR");
        state.SetTarget(IndexDestination.GitHub, github + "/tree/master?tab=readme-ov-file");
        Require(!state.NeedsSubmission(IndexDestination.GitHub),
            "equivalent GitHub repository links do not duplicate a successful index submission");
        state.SetTarget(IndexDestination.Gitee, "https://gitee.com/owner/other-pack");
        Require(state.NeedsSubmission(IndexDestination.Gitee) && !state.NeedsSubmission(IndexDestination.GitHub),
            "changing the Gitee repository resets only the Gitee destination");
        state.SetTarget(IndexDestination.Gitee, string.Empty);
        Require(!state.IsRequested(IndexDestination.Gitee) && !state.NeedsSubmission(IndexDestination.Gitee) && state.AllSubmitted,
            "clearing a repository link removes that destination without resubmitting the successful one");
    }

    private static async Task CheckManifestUrls()
    {
        const string master = "https://raw.githubusercontent.com/owner/pack/master/pack.json";
        using (var handler = new ResolveApi { DownloadUrl = master })
        using (var api = new HttpClient(handler, false))
        using (var raw = new HttpClient(handler, false))
        {
            var info = await GitHubIndexPublisher.ResolvePackAsync(api, raw, "https://github.com/owner/pack");
            Require(info.ManifestUrl == master && handler.MetadataReads == 0 && handler.RawReads == 1,
                "index resolution verifies GitHub's master-branch download URL anonymously");
        }
        using (var handler = new ResolveApi
        {
            DownloadUrl = master + "?token=temporary-test-token#private-fragment",
            ApiPack = "{\"id\":\"pack\",\"description\":\"private API metadata\"}",
            RawPack = "{\"id\":\"pack\",\"description\":\"public metadata\"}"
        })
        using (var api = new HttpClient(handler, false))
        using (var raw = new HttpClient(handler, false))
        {
            api.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "api-test-credential");
            var info = await GitHubIndexPublisher.ResolvePackAsync(api, raw, "https://github.com/owner/pack");
            Require(info.ManifestUrl == master && handler.RawUrls.SequenceEqual(new[] { master }),
                "temporary query credentials and fragments are removed before public verification and index publication");
            Require(info.Summary == "public metadata" && !handler.RawAuthenticated,
                "index metadata comes only from the anonymous public response, never the authenticated private content");
        }
        foreach (var signed in new[] { true, false })
        using (var handler = new ResolveApi
        {
            DownloadUrl = signed ? master + "?token=temporary-test-token#private-fragment" : null,
            RawUnavailable = true
        })
        using (var api = new HttpClient(handler, false))
        using (var raw = new HttpClient(handler, false))
        {
            try
            {
                await GitHubIndexPublisher.ResolvePackAsync(api, raw, "https://github.com/owner/pack");
                throw new Exception("Expected private manifest rejection");
            }
            catch (InvalidOperationException exception)
            {
                Require(exception.Message.Contains("publicly accessible", StringComparison.Ordinal) &&
                    !exception.Message.Contains("temporary-test-token", StringComparison.Ordinal) && handler.RawReads == 1 &&
                    handler.RawUrls.All(url => !url.Contains('?') && !url.Contains('#')),
                    signed ? "private signed manifests are rejected without forwarding credentials or trying another branch" :
                        "private default-branch manifests are rejected even when the API returns their contents");
            }
        }
        using (var handler = new ResolveApi { DownloadUrl = master })
        using (var api = new HttpClient(handler, false))
        using (var raw = new HttpClient(handler, false))
        {
            raw.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-anonymous");
            try
            {
                await GitHubIndexPublisher.ResolvePackAsync(api, raw, "https://github.com/owner/pack");
                throw new Exception("Expected authenticated verification rejection");
            }
            catch (InvalidOperationException exception)
            {
                Require(exception.Message.Contains("anonymous", StringComparison.Ordinal) && handler.RawReads == 0,
                    "an authenticated verification client cannot be used to certify a manifest as public");
            }
        }
        using (var handler = new ResolveApi { DefaultBranch = "release/2026" })
        using (var api = new HttpClient(handler, false))
        using (var raw = new HttpClient(handler, false))
        {
            var info = await GitHubIndexPublisher.ResolvePackAsync(api, raw, "https://github.com/owner/pack");
            Require(info.ManifestUrl == "https://raw.githubusercontent.com/owner/pack/release%2F2026/pack.json" &&
                handler.MetadataReads == 1,
                "missing download URLs use the verified default branch, including branches with slashes");
        }
        using (var handler = new ResolveApi { DownloadUrl = "https://unrelated.example/pack.json", DefaultBranch = "stable" })
        using (var api = new HttpClient(handler, false))
        using (var raw = new HttpClient(handler, false))
        {
            var info = await GitHubIndexPublisher.ResolvePackAsync(api, raw, "https://github.com/owner/pack");
            Require(info.ManifestUrl == "https://raw.githubusercontent.com/owner/pack/stable/pack.json",
                "an unrelated API download URL is replaced with the verified repository default branch");
        }
        using (var handler = new ResolveApi { ApiNotFound = true })
        using (var api = new HttpClient(handler, false))
        using (var raw = new HttpClient(handler, false))
        {
            var info = await GitHubIndexPublisher.ResolvePackAsync(api, raw, "https://github.com/owner/pack");
            Require(info.ManifestUrl == master && handler.RawReads == 2,
                "raw fallback remembers the branch that actually served pack.json instead of assuming main");
        }
        using (var handler = new ResolveApi { CancelRequests = true })
        using (var api = new HttpClient(handler, false))
        using (var raw = new HttpClient(handler, false))
        {
            try
            {
                await GitHubIndexPublisher.ResolvePackAsync(api, raw, "https://github.com/owner/pack");
                throw new Exception("Expected cancellation");
            }
            catch (OperationCanceledException) { }
            Require(handler.RawReads == 0, "cancelled index metadata reads do not start a fallback request");
        }
    }

    private sealed class ResolveApi : HttpMessageHandler
    {
        private const string Pack = "{\"id\":\"pack\",\"name\":\"Pack\",\"packageIds\":[\"test.mod\"]}";
        public string? DownloadUrl;
        public string DefaultBranch = "master";
        public string ApiPack = Pack, RawPack = Pack;
        public bool ApiNotFound, CancelRequests, RawUnavailable, RawAuthenticated;
        public List<string> RawUrls { get; } = new();
        public int MetadataReads, RawReads;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method != HttpMethod.Get) throw new Exception("Resolution must never publish or mutate remote state.");
            if (CancelRequests) return Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true));
            var uri = request.RequestUri!;
            if (uri.Host == "api.github.com")
            {
                if (uri.AbsolutePath.EndsWith("/contents/pack.json"))
                    return Task.FromResult(ApiNotFound ? new HttpResponseMessage(HttpStatusCode.NotFound) :
                        Json(JsonSerializer.Serialize(new { content = Convert.ToBase64String(Encoding.UTF8.GetBytes(ApiPack)), download_url = DownloadUrl })));
                if (uri.AbsolutePath == "/repos/owner/pack")
                {
                    MetadataReads++;
                    return Task.FromResult(Json(JsonSerializer.Serialize(new { default_branch = DefaultBranch })));
                }
            }
            if (uri.Host == "raw.githubusercontent.com")
            {
                RawReads++;
                RawUrls.Add(uri.AbsoluteUri);
                RawAuthenticated |= request.Headers.Authorization is not null || request.Headers.Contains("Cookie");
                return Task.FromResult(!RawUnavailable && (!ApiNotFound || uri.AbsolutePath == "/owner/pack/master/pack.json")
                    ? Json(RawPack) : new HttpResponseMessage(HttpStatusCode.NotFound));
            }
            throw new Exception("Unexpected metadata request: " + uri);
        }

        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value) };
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

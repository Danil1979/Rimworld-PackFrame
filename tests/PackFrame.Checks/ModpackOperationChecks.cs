using PackFrame;

internal static class ModpackOperationChecks
{
    internal static async Task Run(string root)
    {
        CheckWorkshopLinks();
        await CheckInstallCompletion();
        await CheckConfigCommit(Path.Combine(root, "config-commit-state"));
    }

    private static void CheckWorkshopLinks()
    {
        Require(ModpackService.TryResolveWorkshopOverride(null, null, out var optional) && optional is null &&
            ModpackService.TryResolveWorkshopOverride("  ", null, out optional) && optional is null,
            "local and GitHub-only packs do not require a Workshop link");
        const string link = "https://steamcommunity.com/sharedfiles/filedetails/?id=12345";
        Require(ModpackService.TryResolveWorkshopOverride(" " + link + " ", link, out var automatic) && automatic is null,
            "an auto-filled Workshop link follows the fresh installation manifest");
        Require(ModpackService.TryResolveWorkshopOverride("67890", link, out var custom) && custom == "67890",
            "an explicitly changed Workshop target remains an override");
        Require(ModpackService.TryResolveWorkshopOverride(string.Empty, link, out var empty) && empty is null,
            "clearing the optional Workshop field retains the pack's own manifest source");
        Require(!ModpackService.TryResolveWorkshopOverride("not-a-workshop-link", link, out _),
            "a nonempty malformed Workshop override is rejected");
    }

    private static async Task CheckInstallCompletion()
    {
        var result = new ModpackInstallResult("Test pack", Array.Empty<string>(), 0, 0, 0, 0,
            null, null, "test", null, Array.Empty<string>(), false);
        using var cancellation = new CancellationTokenSource();
        var producer = new TaskCompletionSource<ModpackInstallResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = ModpackService.AwaitInstallCompletionAsync(producer.Task, cancellation.Token);
        cancellation.Cancel();
        Require(!completion.IsCompleted, "cancellation waits for the uninterruptible install step to settle");
        producer.SetResult(result);
        await ExpectCancellation(completion);
        Require(true, "a late successful install result is not handed off after cancellation");
        Require(ReferenceEquals(result, await ModpackService.AwaitInstallCompletionAsync(Task.FromResult(result), default)),
            "successful uncancelled installation retains its complete result");
    }

    private static async Task CheckConfigCommit(string root)
    {
        var staging = Path.Combine(root, "staging");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(target);
        var existing = Path.Combine(target, "existing.xml");
        File.WriteAllText(existing, "old");
        File.WriteAllText(Path.Combine(staging, "existing.xml"), "new");
        File.WriteAllText(Path.Combine(staging, "additional.xml"), "additional");
        var notified = false;
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            await ExpectCancellation(ModpackService.CommitConfigFilesAsync(staging, target, cancelled.Token,
                () => notified = true));
        }
        Require(!notified && File.ReadAllText(existing) == "old" && !File.Exists(Path.Combine(target, "additional.xml")),
            "cancel before configuration commit leaves every target file unchanged");
        using (var cancellation = new CancellationTokenSource())
        {
            await ModpackService.CommitConfigFilesAsync(staging, target, cancellation.Token, () =>
            {
                Require(File.ReadAllText(existing) == "old", "commit notification reaches the owner before replacement starts");
                notified = true;
                cancellation.Cancel();
            });
        }
        Require(notified && File.ReadAllText(existing) == "new" &&
            File.ReadAllText(Path.Combine(target, "additional.xml")) == "additional",
            "cancellation after commit begins cannot interrupt the multi-file transaction");

        File.WriteAllText(existing, "keep");
        try
        {
            await ModpackService.CommitConfigFilesAsync(staging, target, default,
                () => throw new InvalidOperationException("Owner cannot enter commit state"));
            throw new Exception("Expected notification failure");
        }
        catch (InvalidOperationException) { }
        Require(File.ReadAllText(existing) == "keep", "failure to enter commit state prevents target writes");

        File.Delete(Path.Combine(target, "additional.xml"));
        Directory.CreateDirectory(Path.Combine(target, "additional.xml"));
        try
        {
            await ModpackService.CommitConfigFilesAsync(staging, target, default);
            throw new Exception("Expected file replacement failure");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Require(File.ReadAllText(existing) == "keep", "configuration commit failure still rolls back prior replacements");
    }

    private static async Task ExpectCancellation(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { return; }
        throw new Exception("Expected cancellation");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

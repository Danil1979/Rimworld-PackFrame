using System.Text.Json;
using PackFrame;

internal static class WorkshopDownloadStateChecks
{
    public static async Task Run()
    {
        var downloading = new WorkshopDownloadItem(11, WorkshopItemState.Subscribed |
            WorkshopItemState.NeedsUpdate | WorkshopItemState.Downloading, 25, 100);
        Require(downloading.IsDownloading && !downloading.IsQueued && !downloading.IsInstalled &&
                downloading.IsSubscribed && downloading.NeedsUpdate,
            "Steam's downloading flag identifies a real incomplete Workshop download");
        var queued = downloading with { State = WorkshopItemState.Subscribed | WorkshopItemState.DownloadPending };
        Require(queued.IsDownloading && queued.IsQueued && !queued.IsInstalled,
            "a pending Steam Workshop download remains queued rather than missing or installed");
        var updating = downloading with { State = WorkshopItemState.Installed | WorkshopItemState.NeedsUpdate |
            WorkshopItemState.Downloading };
        Require(updating.IsDownloading && !updating.IsInstalled,
            "existing installed files do not make an in-progress Workshop update complete");
        Require(!(updating with { BytesDownloaded = 100, BytesTotal = 100 }).IsInstalled,
            "reported download byte completion alone cannot authorize a Workshop completion refresh");
        var stale = downloading with { State = WorkshopItemState.Installed | WorkshopItemState.NeedsUpdate };
        Require(!stale.IsDownloading && !stale.IsInstalled,
            "a needed update alone is not falsely labeled as actively downloading or complete");
        var completed = downloading with { State = WorkshopItemState.Subscribed | WorkshopItemState.Installed };
        Require(completed.IsInstalled && !completed.IsDownloading && !completed.IsQueued,
            "Workshop completion requires Steam's installed state without pending update flags");
        var unknown = downloading with { State = WorkshopItemState.None };
        Require(!unknown.IsDownloading && !unknown.IsInstalled,
            "an untracked Workshop item is not mislabeled as downloading");

        var snapshot = new WorkshopDownloadSnapshot(new() { [11] = downloading, [12] = completed with { WorkshopId = 12 } });
        var json = JsonSerializer.Serialize(snapshot, WorkshopStateJsonContext.Default.WorkshopDownloadSnapshot);
        var restored = JsonSerializer.Deserialize(json, WorkshopStateJsonContext.Default.WorkshopDownloadSnapshot);
        Require(SteamWorkshopClient.IsCompleteDownloadSnapshot(restored, new ulong[] { 11, 12 }) &&
                restored!.Items[11].BytesDownloaded == 25 && restored.Items[11].BytesTotal == 100 &&
                restored.Items[12].IsInstalled && !json.Contains("isInstalled", StringComparison.OrdinalIgnoreCase),
            "isolated helper snapshots preserve Steam IDs, flags and progress without trusting derived status fields");
        Require(!SteamWorkshopClient.IsCompleteDownloadSnapshot(null, new ulong[] { 11 }) &&
                !SteamWorkshopClient.IsCompleteDownloadSnapshot(snapshot, new ulong[] { 11 }) &&
                !SteamWorkshopClient.IsCompleteDownloadSnapshot(new(new() { [11] = completed with { WorkshopId = 99 } }), new ulong[] { 11 }),
            "partial or mismatched helper results remain unknown instead of authorizing a refresh");

        var directory = Path.Combine(Path.GetTempPath(), "PackFrame query fixture");
        var game = "C:/Games/游戏 & notes/RimWorld";
        var apphost = SteamWorkshopClient.CreateDownloadQueryStartInfo("C:/PackFrame/packframe.exe", "",
            directory, game);
        Require(!apphost.UseShellExecute && apphost.CreateNoWindow && apphost.ArgumentList.SequenceEqual(new[]
            { SteamWorkshopClient.DownloadStateHelperArg, game, Path.Combine(directory, "result.json"), Path.Combine(directory, "request.json") }),
            "Workshop status queries pass paths as separate shell-free arguments and keep item IDs in a request file");
        var hosted = SteamWorkshopClient.CreateDownloadQueryStartInfo("C:/dotnet/dotnet.exe", "C:/Pack Frame/packframe.dll",
            directory, game);
        Require(hosted.ArgumentList[0] == "C:/Pack Frame/packframe.dll" &&
                hosted.ArgumentList[1] == SteamWorkshopClient.DownloadStateHelperArg,
            "framework-dependent Workshop helpers launch the PackFrame assembly before their query arguments");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            await SteamWorkshopClient.QueryDownloadStateAsync("unused", new ulong[] { 11 }, cancelled.Token);
            throw new Exception("FAIL: cancelled Workshop status query did not cancel");
        }
        catch (OperationCanceledException)
        {
            Require(true, "cancelled Workshop queries stop before any helper or Steam operation starts");
        }
        var empty = await SteamWorkshopClient.QueryDownloadStateAsync("unused", Array.Empty<ulong>());
        Require(empty?.Items.Count == 0, "an empty Workshop query requires no native Steam session");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

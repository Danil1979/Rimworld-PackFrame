using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PackFrame;

// Values are the Steamworks ISteamUGC EItemState bit flags. Installed alone is
// usable, but an installed copy may still be downloading its replacement.
[Flags]
internal enum WorkshopItemState : uint
{
    None = 0,
    Subscribed = 1,
    LegacyItem = 2,
    Installed = 4,
    NeedsUpdate = 8,
    Downloading = 16,
    DownloadPending = 32
}

internal sealed record WorkshopDownloadItem(ulong WorkshopId, WorkshopItemState State,
    ulong BytesDownloaded = 0, ulong BytesTotal = 0)
{
    [JsonIgnore] public bool IsDownloading =>
        (State & (WorkshopItemState.Downloading | WorkshopItemState.DownloadPending)) != 0;
    [JsonIgnore] public bool IsQueued =>
        State.HasFlag(WorkshopItemState.DownloadPending) && !State.HasFlag(WorkshopItemState.Downloading);
    [JsonIgnore] public bool IsInstalled => State.HasFlag(WorkshopItemState.Installed) &&
        !NeedsUpdate && !IsDownloading;
    [JsonIgnore] public bool NeedsUpdate => State.HasFlag(WorkshopItemState.NeedsUpdate);
    [JsonIgnore] public bool IsSubscribed => State.HasFlag(WorkshopItemState.Subscribed);
}

internal sealed record WorkshopDownloadSnapshot(Dictionary<ulong, WorkshopDownloadItem> Items);
internal sealed record WorkshopDownloadRequest(List<ulong> WorkshopIds);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WorkshopDownloadSnapshot))]
[JsonSerializable(typeof(WorkshopDownloadRequest))]
internal partial class WorkshopStateJsonContext : JsonSerializerContext { }

internal static partial class SteamWorkshopClient
{
    internal const string DownloadStateHelperArg = "--packframe-steam-download-state";
    private const int MaximumStateItems = 10000;
    private const long MaximumStateBytes = 4 * 1024 * 1024;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint SteamUgcGetItemState(IntPtr self, ulong publishedFileId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool SteamUgcGetItemDownloadInfo(IntPtr self, ulong publishedFileId,
        out ulong bytesDownloaded, out ulong bytesTotal);

    public static async Task<WorkshopDownloadSnapshot?> QueryDownloadStateAsync(string gamePath,
        IReadOnlyList<ulong> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = ids.Where(id => id != 0).Distinct().ToList();
        if (requested.Count == 0) return new WorkshopDownloadSnapshot(new());
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || requested.Count > MaximumStateItems)
            return null;
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return null;

        var directory = Path.Combine(Path.GetTempPath(), "PackFrame", "steam-state-" + Guid.NewGuid().ToString("N"));
        Process? process = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            Directory.CreateDirectory(directory);
            var requestPath = Path.Combine(directory, "request.json");
            var resultPath = Path.Combine(directory, "result.json");
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(new WorkshopDownloadRequest(requested),
                WorkshopStateJsonContext.Default.WorkshopDownloadRequest), timeout.Token);
            var start = CreateDownloadQueryStartInfo(executable, Path.Combine(AppContext.BaseDirectory, "packframe.dll"),
                directory, gamePath);
            process = Process.Start(start);
            if (process is null) return null;
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0 || !File.Exists(resultPath) || new FileInfo(resultPath).Length > MaximumStateBytes)
                return null;
            var snapshot = JsonSerializer.Deserialize(await File.ReadAllTextAsync(resultPath, timeout.Token),
                WorkshopStateJsonContext.Default.WorkshopDownloadSnapshot);
            return IsCompleteDownloadSnapshot(snapshot, requested) ? snapshot : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            JsonException or ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return null; }
        finally
        {
            if (process is not null)
            {
                try
                {
                    // This process was created only for this read-only query.
                    // Never stop Steam or any other PackFrame process.
                    if (!process.HasExited)
                    {
                        process.Kill();
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1));
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or
                    System.ComponentModel.Win32Exception or TimeoutException) { }
                process.Dispose();
            }
            DeleteDownloadQueryDirectory(directory);
        }
    }

    internal static ProcessStartInfo CreateDownloadQueryStartInfo(string executable, string assemblyPath,
        string directory, string gamePath)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable, WorkingDirectory = directory, UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new InvalidOperationException("The PackFrame assembly could not be located.");
            start.ArgumentList.Add(assemblyPath);
        }
        start.ArgumentList.Add(DownloadStateHelperArg);
        start.ArgumentList.Add(gamePath);
        start.ArgumentList.Add(Path.Combine(directory, "result.json"));
        start.ArgumentList.Add(Path.Combine(directory, "request.json"));
        return start;
    }

    internal static bool IsCompleteDownloadSnapshot(WorkshopDownloadSnapshot? snapshot, IReadOnlyCollection<ulong> requested)
    {
        if (snapshot?.Items is null || snapshot.Items.Count != requested.Count) return false;
        var expected = new HashSet<ulong>(requested);
        return snapshot.Items.All(item => expected.Contains(item.Key) && item.Value is not null &&
            item.Key == item.Value.WorkshopId);
    }

    private static bool TryRunDownloadStateHelper(string[] args)
    {
        if (args.Length == 0 || args[0] != DownloadStateHelperArg) return false;
        Environment.ExitCode = 1;
        try
        {
            if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || args.Length != 4) return true;
            var result = Path.GetFullPath(args[2]);
            var requestPath = Path.GetFullPath(args[3]);
            var directory = Path.GetDirectoryName(result)!;
            if (!IsDownloadQueryDirectory(directory) ||
                !Path.GetFileName(result).Equals("result.json", StringComparison.Ordinal) ||
                !Path.GetFileName(requestPath).Equals("request.json", StringComparison.Ordinal) ||
                !string.Equals(directory, Path.GetDirectoryName(requestPath), StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(requestPath) || new FileInfo(requestPath).Length > 512 * 1024) return true;
            var request = JsonSerializer.Deserialize(File.ReadAllText(requestPath),
                WorkshopStateJsonContext.Default.WorkshopDownloadRequest);
            if (request?.WorkshopIds is null || request.WorkshopIds.Count is < 1 or > MaximumStateItems ||
                request.WorkshopIds.Any(id => id == 0) || request.WorkshopIds.Distinct().Count() != request.WorkshopIds.Count)
                return true;
            var snapshot = QueryDownloadStateCore(args[1], request.WorkshopIds);
            if (snapshot is null) return true;
            FileTransactions.WriteAtomic(result, temporary => File.WriteAllText(temporary,
                JsonSerializer.Serialize(snapshot, WorkshopStateJsonContext.Default.WorkshopDownloadSnapshot)));
            Environment.ExitCode = 0;
        }
        catch (Exception) { }
        return true;
    }

    private static WorkshopDownloadSnapshot? QueryDownloadStateCore(string gamePath, IReadOnlyList<ulong> ids)
    {
        var path = FindSteamApi64Dll(gamePath);
        if (path is null) return null;
        EnsureAppId();
        var library = IntPtr.Zero;
        try
        {
            library = NativeLibrary.Load(path);
            var init = GetDelegate<SteamApiInit>(library, "SteamAPI_Init");
            var shutdown = GetDelegate<SteamApiShutdown>(library, "SteamAPI_Shutdown");
            var callbacks = GetDelegate<SteamApiRunCallbacks>(library, "SteamAPI_RunCallbacks");
            var ugc = GetDelegate<SteamUgc>(library, "SteamAPI_SteamUGC_v016");
            var state = GetDelegate<SteamUgcGetItemState>(library, "SteamAPI_ISteamUGC_GetItemState");
            var downloadInfo = NativeLibrary.TryGetExport(library, "SteamAPI_ISteamUGC_GetItemDownloadInfo", out var info)
                ? Marshal.GetDelegateForFunctionPointer<SteamUgcGetItemDownloadInfo>(info) : null;
            if (!init()) return null;
            try
            {
                var handle = ugc();
                if (handle == IntPtr.Zero) return null;
                // Pump the isolated client's initial callbacks, without issuing
                // subscription or download operations.
                for (var i = 0; i < 4; i++) { callbacks(); Thread.Sleep(50); }
                var items = new Dictionary<ulong, WorkshopDownloadItem>();
                foreach (var id in ids)
                {
                    var itemState = (WorkshopItemState)state(handle, id);
                    ulong downloaded = 0, total = 0;
                    if (downloadInfo is not null && !downloadInfo(handle, id, out downloaded, out total))
                        downloaded = total = 0;
                    items[id] = new WorkshopDownloadItem(id, itemState, downloaded, total);
                }
                return new WorkshopDownloadSnapshot(items);
            }
            finally { shutdown(); }
        }
        catch (Exception) { return null; }
        finally { if (library != IntPtr.Zero) NativeLibrary.Free(library); }
    }

    private static bool IsDownloadQueryDirectory(string directory)
    {
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PackFrame"));
        return string.Equals(Path.GetDirectoryName(directory), parent, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(directory).StartsWith("steam-state-", StringComparison.Ordinal) &&
            Guid.TryParseExact(Path.GetFileName(directory)["steam-state-".Length..], "N", out _);
    }

    private static void DeleteDownloadQueryDirectory(string directory)
    {
        try
        {
            directory = Path.GetFullPath(directory);
            if (IsDownloadQueryDirectory(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using PackFrame;

internal static class AppUpdateChecks
{
    private const string LegacyPrefix = "Rimworld PackFrame/app/PackFrame/bin/Release/net10.0/";
    private static readonly byte[] OldProgram = Executable("old-program");
    private static readonly byte[] NewProgram = Executable("new-program");

    public static async Task Run(string root)
    {
        var legacy = Fixture(root, "legacy");
        var userFiles = UserFiles(legacy);
        var archive = Zip(
            Entry("Rimworld PackFrame/instance/", null),
            Entry("Rimworld PackFrame/Input profile/", null),
            Entry("Rimworld PackFrame/Output profile/", null),
            Entry(LegacyPrefix + "packframe.exe", NewProgram),
            Entry(LegacyPrefix + "packframe.json", Bytes("publisher-settings")),
            Entry(LegacyPrefix + "instance/profile1/Config/ModsConfig.xml", Bytes("publisher-order")),
            Entry(LegacyPrefix + "runtime/catalog/custom.json", Bytes("publisher-cache")));
        using var legacyHttp = new UpdateHttp(archive);
        using var legacyClient = new HttpClient(legacyHttp);
        var info = Release(archive);
        var phases = new List<string>();
        var prepared = await AppUpdateService.PrepareAsync(info,
            new InlineProgress(value => phases.Add(value.Phase)), client: legacyClient, programDirectory: legacy);
        Require(prepared.Files.Select(file => file.Path).SequenceEqual(new[] { "packframe.exe" }) &&
                prepared.Files.Single().Length == NewProgram.Length && File.ReadAllBytes(ProgramPath(legacy)).SequenceEqual(OldProgram),
            "legacy release archives stage only the program without changing the running installation");
        Require(phases.Contains("download") && phases.Contains("verify") && phases.Contains("extract"),
            "update preparation reports download, verification and extraction phases");
        Require(Unchanged(userFiles), "download and staging preserve settings, instances, saves, mods and existing caches");
        var cached = await AppUpdateService.PrepareAsync(info, client: legacyClient, programDirectory: legacy);
        Require(legacyHttp.Requests == 1 && cached.StageDirectory == prepared.StageDirectory &&
                AppUpdateService.FindPrepared(info, legacy) is not null,
            "a verified prepared update is reused without downloading again");

        var backup = BackupPath(legacy, "success");
        AppUpdateService.ApplyPrepared(prepared, backup);
        Require(File.ReadAllBytes(ProgramPath(legacy)).SequenceEqual(NewProgram) &&
                File.ReadAllBytes(Path.Combine(backup, "packframe.exe")).SequenceEqual(OldProgram) && Unchanged(userFiles),
            "installation replaces program files, retains a backup and preserves every user-data file");
        File.WriteAllBytes(Path.Combine(prepared.StageDirectory, "payload", "packframe.exe"), Executable("tampered"));
        Require(AppUpdateService.FindPrepared(info, legacy) is null,
            "changed staged payloads are rejected instead of trusted as a cached update");
        var recovered = await AppUpdateService.PrepareAsync(info, client: legacyClient, programDirectory: legacy);
        Require(legacyHttp.Requests == 2 && AppUpdateService.FindPrepared(info, legacy) is not null &&
                File.ReadAllBytes(Path.Combine(recovered.StageDirectory, "payload", "packframe.exe")).SequenceEqual(NewProgram) &&
                Unchanged(userFiles),
            "a corrupt prepared cache can be replaced by a newly verified download without changing user files");

        var flat = Fixture(root, "flat");
        var flatZip = Zip(Entry("packframe.exe", NewProgram), Entry("support.dll", Bytes("support")));
        var flatPrepared = await Prepare(flat, flatZip);
        Require(flatPrepared.Files.Select(file => file.Path).Order().SequenceEqual(new[] { "packframe.exe", "support.dll" }),
            "flat program archives retain companion program files for installation");

        var rollback = Fixture(root, "rollback");
        var rollbackZip = Zip(Entry("packframe.exe", NewProgram), Entry("new.dll", Bytes("new-support")),
            Entry("existing.dll", Bytes("changed-support")));
        File.WriteAllText(Path.Combine(rollback, "existing.dll"), "original-support");
        var rollbackPrepared = await Prepare(rollback, rollbackZip);
        var rollbackBackup = BackupPath(rollback, "rollback");
        ExpectFailure(() => AppUpdateService.ApplyPrepared(rollbackPrepared, rollbackBackup,
            index => { if (index == 2) throw new IOException("Injected replacement failure"); }));
        Require(File.ReadAllBytes(ProgramPath(rollback)).SequenceEqual(OldProgram) &&
                !File.Exists(Path.Combine(rollback, "new.dll")) &&
                File.ReadAllText(Path.Combine(rollback, "existing.dll")) == "original-support" &&
                File.ReadAllBytes(Path.Combine(rollbackBackup, "packframe.exe")).SequenceEqual(OldProgram),
            "failed installation restores overwritten files, removes newly added files and keeps backups");

        var protectedManifest = flatPrepared with
        {
            Files = new List<AppUpdateFile>(flatPrepared.Files)
            { new("packframe.json", 1, new string('0', 64)) }
        };
        ExpectFailure(() => AppUpdateService.ApplyPrepared(protectedManifest, BackupPath(flat, "protected")));
        var traversalManifest = flatPrepared with
        {
            Files = new List<AppUpdateFile>(flatPrepared.Files)
            { new("../escaped.dll", 1, new string('0', 64)) }
        };
        ExpectFailure(() => AppUpdateService.ApplyPrepared(traversalManifest, BackupPath(flat, "manifest-traversal")));
        Require(File.ReadAllBytes(ProgramPath(flat)).SequenceEqual(OldProgram) &&
                File.ReadAllText(Path.Combine(flat, "packframe.json")) == "private-settings" &&
                !File.Exists(Path.Combine(Path.GetDirectoryName(flat)!, "escaped.dll")),
            "protected and escaping manifest paths are rejected before application files change");

        var badHash = Fixture(root, "bad-hash");
        using (var handler = new UpdateHttp(flatZip))
        using (var client = new HttpClient(handler))
        {
            var wrongHash = Release(flatZip) with { Asset = Release(flatZip).Asset! with { Sha256 = new string('0', 64) } };
            await ExpectFailure(() => AppUpdateService.PrepareAsync(wrongHash, client: client, programDirectory: badHash));
        }
        Require(InstallationUntouched(badHash) && NoPreparedArtifacts(badHash),
            "checksum failures leave the installed program and update cache unchanged");

        foreach (var delta in new[] { -1L, 1L })
        {
            var folder = Fixture(root, delta < 0 ? "oversized" : "truncated");
            using var handler = new UpdateHttp(flatZip, unknownLength: true);
            using var client = new HttpClient(handler);
            var wrongSize = Release(flatZip) with { Asset = Release(flatZip).Asset! with { Size = flatZip.LongLength + delta } };
            await ExpectFailure(() => AppUpdateService.PrepareAsync(wrongSize, client: client, programDirectory: folder));
            Require(InstallationUntouched(folder) && NoPreparedArtifacts(folder),
                delta < 0 ? "oversized download streams cannot change installed files" :
                "truncated download streams cannot publish a prepared update");
        }

        foreach (var (name, unsafeZip) in new[]
        {
            ("zip-traversal", Zip(Entry("packframe.exe", NewProgram), Entry("../outside.txt", Bytes("bad")))),
            ("zip-absolute", Zip(Entry("packframe.exe", NewProgram), Entry("/outside.txt", Bytes("bad")))),
            ("zip-drive", Zip(Entry("packframe.exe", NewProgram), Entry("C:/outside.txt", Bytes("bad")))),
            ("zip-duplicate", Zip(Entry("packframe.exe", NewProgram), Entry("PACKFRAME.EXE", NewProgram))),
            ("zip-symlink", Zip(Entry("packframe.exe", NewProgram), Entry("linked.dll", Bytes("target"), unchecked((int)0xA1FF0000))))
        })
        {
            var folder = Fixture(root, name);
            await ExpectFailure(() => Prepare(folder, unsafeZip));
            Require(InstallationUntouched(folder) && NoPreparedArtifacts(folder),
                name + " archive is rejected without publishing or installing files");
        }

        var cancelled = Fixture(root, "cancelled-download");
        var bigProgram = new byte[220000];
        NewProgram.CopyTo(bigProgram, 0);
        var bigZip = Zip(Entry("packframe.exe", bigProgram));
        using (var handler = new UpdateHttp(bigZip, unknownLength: true))
        using (var client = new HttpClient(handler))
        using (var cancellation = new CancellationTokenSource())
        {
            await ExpectCancellation(() => AppUpdateService.PrepareAsync(Release(bigZip),
                new InlineProgress(value => { if (value.Phase == "download") cancellation.Cancel(); }),
                cancellation.Token, client, cancelled));
        }
        Require(InstallationUntouched(cancelled) && NoPreparedArtifacts(cancelled),
            "cancelling an in-progress download removes its incomplete staging and preserves the installation");

        var gateFolder = Fixture(root, "handoff-gate");
        var gatePrepared = await Prepare(gateFolder, flatZip);
        var readyPlan = Plan(gatePrepared, "ready-handshake");
        ExpectFailure(() => AppUpdateService.WaitForHelperReady(readyPlan, () => false, TimeSpan.Zero));
        File.WriteAllText(Path.Combine(readyPlan.HandoffDirectory, "ready"), "unrelated-token");
        ExpectFailure(() => AppUpdateService.WaitForHelperReady(readyPlan, () => false, TimeSpan.Zero));
        File.WriteAllText(Path.Combine(readyPlan.HandoffDirectory, "ready"), readyPlan.CommitToken);
        ExpectFailure(() => AppUpdateService.WaitForHelperReady(readyPlan, () => true, TimeSpan.Zero));
        AppUpdateService.WaitForHelperReady(readyPlan, () => false, TimeSpan.Zero);
        Require(InstallationUntouched(gateFolder),
            "helper handoff requires a matching ready token from a live helper and does not install anything");
        var plan = Plan(gatePrepared, "gate");
        var restarts = 0;
        var noConsent = await AppUpdateService.ApplyWhenReadyAsync(plan, () => true, () => restarts++, TimeSpan.Zero);
        Require(!noConsent && InstallationUntouched(gateFolder) && restarts == 0,
            "a helper with no committed installation choice cannot modify files even after the parent exits");
        File.WriteAllText(Path.Combine(plan.HandoffDirectory, "commit"), plan.CommitToken);
        var parentAlive = await AppUpdateService.ApplyWhenReadyAsync(plan, () => false, () => restarts++, TimeSpan.Zero);
        Require(!parentAlive && InstallationUntouched(gateFolder) && restarts == 0,
            "a committed helper waits for parent exit without replacing or killing the running application");
        File.WriteAllText(Path.Combine(plan.HandoffDirectory, "cancel"), plan.CommitToken);
        var cancelledHandoff = await AppUpdateService.ApplyWhenReadyAsync(plan, () => true, () => restarts++, TimeSpan.Zero);
        Require(!cancelledHandoff && InstallationUntouched(gateFolder) && restarts == 0,
            "a cancelled handoff overrides a commit and leaves the application running unchanged");

        var wrongTokenPlan = Plan(gatePrepared, "wrong-token");
        File.WriteAllText(Path.Combine(wrongTokenPlan.HandoffDirectory, "commit"), "different-token");
        Require(!await AppUpdateService.ApplyWhenReadyAsync(wrongTokenPlan, () => true, () => restarts++, TimeSpan.Zero) &&
                InstallationUntouched(gateFolder) && restarts == 0,
            "an unrelated commit marker cannot authorize installation");

        var allowedPlan = Plan(gatePrepared, "confirmed");
        File.WriteAllText(Path.Combine(allowedPlan.HandoffDirectory, "commit"), allowedPlan.CommitToken);
        var installed = await AppUpdateService.ApplyWhenReadyAsync(allowedPlan, () => true, () => restarts++, TimeSpan.Zero);
        var receipt = AppUpdateService.TakeLastResult(gateFolder);
        Require(installed && restarts == 1 && File.ReadAllBytes(ProgramPath(gateFolder)).SequenceEqual(NewProgram) &&
                receipt is { Success: true, Error: null } && receipt.Tag == gatePrepared.Tag &&
                receipt.BackupDirectory is not null && File.Exists(Path.Combine(receipt.BackupDirectory, "packframe.exe")) &&
                AppUpdateService.TakeLastResult(gateFolder) is null,
            "an explicitly committed update installs only after parent exit, restarts once and reports its result once");

        var receiptFailure = Fixture(root, "receipt-write-failure");
        var receiptPrepared = await Prepare(receiptFailure, flatZip);
        var receiptPlan = Plan(receiptPrepared, "receipt-write-failure");
        File.WriteAllText(Path.Combine(receiptPlan.HandoffDirectory, "commit"), receiptPlan.CommitToken);
        Directory.CreateDirectory(Path.Combine(receiptFailure, "runtime", "app-updates", "result.json"));
        var receiptRestarts = 0;
        var appliedWithoutReceipt = await AppUpdateService.ApplyWhenReadyAsync(receiptPlan, () => true,
            () => receiptRestarts++, TimeSpan.Zero);
        Require(appliedWithoutReceipt && receiptRestarts == 1 &&
                File.ReadAllBytes(ProgramPath(receiptFailure)).SequenceEqual(NewProgram) &&
                File.Exists(Path.Combine(receiptPlan.HandoffDirectory, "update-error.log")),
            "a receipt write failure cannot prevent restarting a successfully updated application");
    }

    private static string Fixture(string root, string name)
    {
        var path = Path.Combine(root, "app-update-" + name);
        Directory.CreateDirectory(path);
        File.WriteAllBytes(ProgramPath(path), OldProgram);
        foreach (var (relative, text) in new[]
        {
            ("packframe.json", "private-settings"), ("packframe.log", "private-log"),
            ("instance/profile1/Config/ModsConfig.xml", "private-order"),
            ("saves/Colony.rws", "private-save"), ("mods/LocalMod/About.xml", "private-mod"),
            ("runtime/catalog/custom.json", "private-cache")
        })
        {
            var target = Path.Combine(path, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, text);
        }
        return path;
    }

    private static Dictionary<string, byte[]> UserFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(file => !file.Equals(ProgramPath(folder), StringComparison.OrdinalIgnoreCase))
            .ToDictionary(file => file, File.ReadAllBytes);
    private static bool Unchanged(Dictionary<string, byte[]> files) =>
        files.All(item => File.Exists(item.Key) && File.ReadAllBytes(item.Key).SequenceEqual(item.Value));
    private static bool InstallationUntouched(string folder) =>
        File.ReadAllBytes(ProgramPath(folder)).SequenceEqual(OldProgram) &&
        File.ReadAllText(Path.Combine(folder, "packframe.json")) == "private-settings";
    private static bool NoPreparedArtifacts(string folder)
    {
        var updates = Path.Combine(folder, "runtime", "app-updates");
        return !Directory.Exists(updates) ||
            (!Directory.EnumerateDirectories(updates, "*.preparing-*", SearchOption.AllDirectories).Any() &&
             !Directory.EnumerateFiles(updates, "prepared.json", SearchOption.AllDirectories).Any());
    }
    private static string ProgramPath(string folder) => Path.Combine(folder, "packframe.exe");
    private static string BackupPath(string folder, string name) =>
        Path.Combine(folder, "runtime", "app-updates", "test-" + name, "backup");
    private static AppUpdatePlan Plan(PreparedAppUpdate update, string name)
    {
        var handoff = Path.Combine(update.ProgramDirectory, "runtime", "app-updates", "handoff-" + name);
        Directory.CreateDirectory(handoff);
        return new AppUpdatePlan(update, 123, 456, handoff, Guid.NewGuid().ToString("N"),
            ProgramPath(update.ProgramDirectory), null);
    }
    private static async Task<PreparedAppUpdate> Prepare(string folder, byte[] archive)
    {
        using var handler = new UpdateHttp(archive);
        using var client = new HttpClient(handler);
        return await AppUpdateService.PrepareAsync(Release(archive), client: client, programDirectory: folder);
    }
    private static UpdateInfo Release(byte[] archive) => new("v9.0.0",
        "https://github.com/ZXUW/Rimworld-PackFrame/releases/tag/v9.0.0", Asset: new UpdateAsset(
            "packframe-win-x64.zip", "https://github.com/ZXUW/Rimworld-PackFrame/releases/download/v9.0.0/packframe-win-x64.zip",
            archive.LongLength, Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant()));
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static byte[] Executable(string text) => Bytes("MZ" + text);
    private static ArchiveEntry Entry(string name, byte[]? bytes, int attributes = 0) => new(name, bytes, attributes);
    private static byte[] Zip(params ArchiveEntry[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var source in entries)
            {
                var entry = zip.CreateEntry(source.Name, CompressionLevel.NoCompression);
                entry.ExternalAttributes = source.Attributes;
                if (source.Bytes is null) continue;
                using var output = entry.Open();
                output.Write(source.Bytes);
            }
        }
        return buffer.ToArray();
    }
    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException) { return; }
        throw new Exception("FAIL: expected update rejection");
    }
    private static async Task ExpectFailure(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException) { return; }
        throw new Exception("FAIL: expected update rejection");
    }
    private static async Task ExpectCancellation(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { return; }
        throw new Exception("FAIL: expected update cancellation");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
    private sealed record ArchiveEntry(string Name, byte[]? Bytes, int Attributes);
    private sealed class InlineProgress(Action<AppUpdateProgress> report) : IProgress<AppUpdateProgress>
    { public void Report(AppUpdateProgress value) => report(value); }
    private sealed class UpdateHttp(byte[] payload, bool unknownLength = false) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = unknownLength ? new UnknownLengthContent(payload) : new ByteArrayContent(payload)
            });
        }
    }
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}

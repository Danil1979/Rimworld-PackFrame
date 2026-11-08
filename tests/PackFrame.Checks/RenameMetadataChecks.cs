using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PackFrame;

internal static class RenameMetadataChecks
{
    internal static void Run(string root)
    {
        PreserveCurrentMetadata(Path.Combine(root, "rename-metadata-preservation"));
        PreserveFailedRename(Path.Combine(root, "rename-metadata-failures"));
        RenameLegacyMetadata(Path.Combine(root, "rename-metadata-legacy"));
    }

    private static void PreserveCurrentMetadata(string root)
    {
        using var fixture = new Fixture(root);
        var target = fixture.Target;
        var originalMetadata = target.Metadata;
        var current = JsonNode.Parse(CurrentMetadata)!.AsObject();
        File.WriteAllText(fixture.MetadataPath, current.ToJsonString());
        Require(target.Metadata?.Source != "external-source", "rename fixture starts with stale live metadata");

        InstanceStore.RenameInstance(target, "  新しい名前 / New name  ");
        var renamed = ReadDocument(fixture.MetadataPath);
        Require(renamed["name"]?.GetValue<string>() == "新しい名前 / New name" &&
            SameExceptName(current, renamed),
            "rename changes only the name and preserves unknown fields and nested metadata");
        Require(!ReferenceEquals(target.Metadata, originalMetadata) &&
            target.Metadata?.Source == "external-source" && target.Metadata.PackId == "external-pack" &&
            target.Metadata.InstalledRevision == 12 && target.Metadata.SteamCmd &&
            target.Metadata.ConfigFiles.SequenceEqual(new[] { "ModsConfig.xml", "Preferences.xml" }) &&
            target.Metadata.WorkshopMods.Single().WorkshopId == 123456789,
            "rename refreshes external source, binding and other known fields instead of restoring stale values");
        var reloaded = InstanceStore.LoadInstance(target.DirectoryPath);
        Require(reloaded.Metadata?.Name == "新しい名前 / New name" &&
            Serialize(reloaded.Metadata) == Serialize(target.Metadata) &&
            reloaded.ActivePackageIds.SequenceEqual(fixture.Draft),
            "renamed metadata reloads with the same known fields and retained unsaved order");
        fixture.RequireUnchangedListsAndOtherFiles("successful rename and reload");

        // The current disk name already matches. A read-only persistence seam must
        // never be invoked, but newer disk metadata still needs to reach the view.
        renamed["source"] = "newer-source";
        renamed["installedRevision"] = 13;
        var noOpBytes = Encoding.UTF8.GetBytes(renamed.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllBytes(fixture.MetadataPath, noOpBytes);
        var timestamp = DateTime.UtcNow.AddDays(-2);
        File.SetLastWriteTimeUtc(fixture.MetadataPath, timestamp);
        timestamp = File.GetLastWriteTimeUtc(fixture.MetadataPath);
        InstanceStore.RenameInstance(target, "  新しい名前 / New name ", (_, _) =>
            throw new IOException("A matching name must not attempt a write."));
        Require(File.ReadAllBytes(fixture.MetadataPath).SequenceEqual(noOpBytes) &&
            File.GetLastWriteTimeUtc(fixture.MetadataPath) == timestamp &&
            target.Metadata?.Source == "newer-source" && target.Metadata.InstalledRevision == 13,
            "unchanged trimmed name skips persistence while refreshing validated current metadata");
        fixture.RequireUnchangedListsAndOtherFiles("unchanged-name rename");

        InstanceStore.RenameInstance(target, " \t ");
        var emptyName = ReadDocument(fixture.MetadataPath);
        Require(target.Metadata?.Name == string.Empty && emptyName["name"]?.GetValue<string>() == string.Empty &&
            SameExceptName(renamed, emptyName), "clearing a name preserves every other current metadata field");
        var clearedBytes = File.ReadAllBytes(fixture.MetadataPath);
        InstanceStore.RenameInstance(target, null, (_, _) => throw new IOException("An empty name is unchanged."));
        Require(File.ReadAllBytes(fixture.MetadataPath).SequenceEqual(clearedBytes),
            "repeating an empty-name rename does not write again");
        fixture.RequireUnchangedListsAndOtherFiles("repeated and empty-name renames");
    }

    private static void PreserveFailedRename(string root)
    {
        using var fixture = new Fixture(root);
        var target = fixture.Target;
        var liveMetadata = target.Metadata;
        var liveSnapshot = Serialize(liveMetadata);
        var validBytes = Encoding.UTF8.GetBytes(CurrentMetadata);
        var requestedName = "Retried name";

        void RequireLiveUnchanged(string reason)
        {
            Require(ReferenceEquals(target.Metadata, liveMetadata) && Serialize(target.Metadata) == liveSnapshot,
                reason + " retains the exact live metadata object and contents");
            fixture.RequireUnchangedListsAndOtherFiles(reason);
            Require(Directory.GetFiles(target.DirectoryPath, "*.tmp-*").Length == 0,
                reason + " leaves no temporary metadata file");
        }

        foreach (var invalid in new[]
        {
            "{broken", "null", "[]", "{\"name\":42}",
            "{\"steamCmd\":\"not-a-boolean\"}", "{\"managedFiles\":{\"file\":42}}"
        })
        {
            var bytes = Encoding.UTF8.GetBytes(invalid);
            File.WriteAllBytes(fixture.MetadataPath, bytes);
            ExpectFailure(() => InstanceStore.RenameInstance(target, requestedName),
                "rename rejects malformed or incompatible current metadata");
            Require(File.ReadAllBytes(fixture.MetadataPath).SequenceEqual(bytes),
                "rejected metadata is retained byte-for-byte for repair");
            RequireLiveUnchanged("metadata validation failure");
        }

        // A directory is unreadable metadata, not an absent optional file.
        File.WriteAllBytes(fixture.MetadataPath, validBytes);
        var backup = fixture.MetadataPath + ".saved";
        File.Move(fixture.MetadataPath, backup);
        Directory.CreateDirectory(fixture.MetadataPath);
        var marker = Path.Combine(fixture.MetadataPath, "keep.txt");
        File.WriteAllText(marker, "unrelated directory content");
        try
        {
            ExpectFailure(() => InstanceStore.RenameInstance(target, requestedName),
                "rename rejects a directory occupying the metadata path");
            Require(File.ReadAllBytes(backup).SequenceEqual(validBytes) &&
                File.ReadAllText(marker) == "unrelated directory content",
                "directory failure preserves saved metadata bytes and directory contents");
            RequireLiveUnchanged("metadata directory failure");
        }
        finally
        {
            File.Delete(marker);
            Directory.Delete(fixture.MetadataPath);
            File.Move(backup, fixture.MetadataPath);
        }

        var moved = target.DirectoryPath + ".moved";
        Directory.Move(target.DirectoryPath, moved);
        try
        {
            ExpectFailure(() => InstanceStore.RenameInstance(target, requestedName),
                "rename rejects a vanished instance directory");
            Require(!Directory.Exists(target.DirectoryPath) &&
                File.ReadAllBytes(Path.Combine(moved, InstanceStore.MetadataFileName)).SequenceEqual(validBytes),
                "rename does not recreate a vanished instance or replace its retained metadata");
        }
        finally { Directory.Move(moved, target.DirectoryPath); }
        RequireLiveUnchanged("missing instance directory failure");

        // Interrupt after a complete temporary write but before replacement. This
        // exercises the real atomic writer without platform-specific permission tricks.
        var wroteTemporary = false;
        ExpectFailure(() => InstanceStore.RenameInstance(target, requestedName, (path, write) =>
            FileTransactions.WriteAtomic(path, temporary =>
            {
                write(temporary);
                wroteTemporary = ReadDocument(temporary)["name"]?.GetValue<string>() == requestedName;
                Require(ReferenceEquals(target.Metadata, liveMetadata) &&
                    File.ReadAllBytes(path).SequenceEqual(validBytes),
                    "rename does not publish its new name before atomic replacement");
                throw new IOException("Simulated replacement failure");
            })), "rename reports an interrupted atomic replacement");
        Require(wroteTemporary && File.ReadAllBytes(fixture.MetadataPath).SequenceEqual(validBytes),
            "failed atomic rename preserves all original metadata bytes");
        RequireLiveUnchanged("atomic persistence failure");

        // A repair/retry must retain an existing recovery block until a successful
        // instance reload validates the configuration as well as metadata.
        File.WriteAllText(fixture.MetadataPath, "{broken");
        var damaged = InstanceStore.Discover(root).Single(instance => instance.FolderName == target.FolderName);
        var blocked = InstanceStore.BlockedPendingConfigDirectories.ToArray();
        Require(damaged.IsDamaged && blocked.Contains(Path.GetFullPath(target.ConfigDirectoryPath)),
            "discovery retains the pending rename target when metadata becomes damaged");
        ExpectFailure(() => InstanceStore.RenameInstance(damaged, requestedName),
            "rename refuses an already-damaged instance");
        File.WriteAllBytes(fixture.MetadataPath, validBytes);
        InstanceStore.RenameInstance(target, requestedName);
        Require(target.Metadata?.Name == requestedName && target.Metadata.Source == "external-source" &&
            SameExceptName(JsonNode.Parse(CurrentMetadata)!.AsObject(), ReadDocument(fixture.MetadataPath)) &&
            InstanceStore.BlockedPendingConfigDirectories.SequenceEqual(blocked),
            "repair and retry persist the name without releasing unrelated pending-recovery state");
        var reloaded = InstanceStore.LoadInstance(target.DirectoryPath);
        Require(!reloaded.IsDamaged && reloaded.Metadata?.Name == requestedName &&
            reloaded.ActivePackageIds.SequenceEqual(fixture.Draft),
            "successful reload after repair retains the renamed metadata and complete pending list");
        fixture.RequireUnchangedListsAndOtherFiles("repaired rename and reload");
    }

    private static void RenameLegacyMetadata(string root)
    {
        using var fixture = new Fixture(root);
        File.WriteAllText(fixture.MetadataPath, "{\"source\":null,\"future\":{\"keep\":[true,null,42]}}");
        var before = ReadDocument(fixture.MetadataPath);
        InstanceStore.RenameInstance(fixture.Target, "Legacy");
        Require(fixture.Target.Metadata?.Name == "Legacy" && fixture.Target.Metadata.Source is null &&
            SameExceptName(before, ReadDocument(fixture.MetadataPath)),
            "rename adds an absent name without fabricating source or dropping unknown data");
        File.Delete(fixture.MetadataPath);
        InstanceStore.RenameInstance(fixture.Target, "Without metadata");
        var created = ReadDocument(fixture.MetadataPath);
        Require(created.Count == 1 && created["name"]?.GetValue<string>() == "Without metadata" &&
            fixture.Target.Metadata?.Source is null &&
            InstanceStore.LoadInstance(fixture.Target.DirectoryPath).Metadata?.Name == "Without metadata",
            "an existing legacy instance can acquire a name when optional metadata is absent");
        fixture.RequireUnchangedListsAndOtherFiles("legacy instance rename");
    }

    private static JsonObject ReadDocument(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static bool SameExceptName(JsonObject before, JsonObject after)
    {
        var expected = before.DeepClone().AsObject();
        var actual = after.DeepClone().AsObject();
        expected.Remove("name");
        actual.Remove("name");
        return JsonNode.DeepEquals(expected, actual);
    }

    private static string Serialize(InstanceMetadata? metadata) =>
        JsonSerializer.Serialize(metadata, PackFrameJsonContext.Default.InstanceMetadata);

    private static void ExpectFailure(Action action, string message)
    {
        try { action(); }
        catch (IOException) { Require(true, message); return; }
        throw new Exception("FAIL: " + message);
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    private sealed class Fixture : IDisposable
    {
        internal ManagedInstance Target { get; }
        internal ManagedInstance Other { get; }
        internal string MetadataPath => Path.Combine(Target.DirectoryPath, InstanceStore.MetadataFileName);
        internal string[] Draft { get; } = new[] { "missing.mod", "new.mod", "saved.mod" };
        private readonly string[] otherDraft = { "other.new", "other.saved" };
        private readonly Dictionary<string, byte[]> untouchedFiles;
        private readonly string[] pending;
        private readonly string[] blocked;
        private readonly List<string> liveOrder;
        private readonly List<string> otherLiveOrder;
        private readonly InstanceMetadata? otherMetadata;
        private readonly string otherMetadataSnapshot;

        internal Fixture(string root)
        {
            Target = InstanceStore.CreateNew(root, null, "1.6", new[] { "saved.mod" });
            Other = InstanceStore.CreateNew(root, null, "1.7", new[] { "other.saved" });
            File.WriteAllText(Path.Combine(Target.ConfigDirectoryPath, "Preferences.xml"), "unrelated preferences");
            untouchedFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => path != MetadataPath).ToDictionary(path => path, File.ReadAllBytes);
            InstanceStore.StageActiveMods(Target, Draft);
            InstanceStore.StageActiveMods(Other, otherDraft);
            liveOrder = Target.ActivePackageIds;
            otherLiveOrder = Other.ActivePackageIds;
            otherMetadata = Other.Metadata;
            otherMetadataSnapshot = Serialize(Other.Metadata);
            pending = InstanceStore.PendingConfigDirectories.ToArray();
            blocked = InstanceStore.BlockedPendingConfigDirectories.ToArray();
        }

        internal void RequireUnchangedListsAndOtherFiles(string reason)
        {
            Require(ReferenceEquals(Target.ActivePackageIds, liveOrder) && Target.ActivePackageIds.SequenceEqual(Draft) &&
                ReferenceEquals(Other.ActivePackageIds, otherLiveOrder) && Other.ActivePackageIds.SequenceEqual(otherDraft) &&
                InstanceStore.PendingConfigDirectories.SequenceEqual(pending) &&
                InstanceStore.BlockedPendingConfigDirectories.SequenceEqual(blocked),
                reason + " preserves live orders and every pending/blocked entry");
            Require(untouchedFiles.All(file => File.ReadAllBytes(file.Key).SequenceEqual(file.Value)) &&
                ReferenceEquals(Other.Metadata, otherMetadata) && Serialize(Other.Metadata) == otherMetadataSnapshot,
                reason + " leaves ModsConfig, other config files and the other instance unchanged");
        }

        public void Dispose()
        {
            InstanceStore.RemovePending(Target);
            InstanceStore.RemovePending(Other);
        }
    }

    private const string CurrentMetadata = """
        {
          "name": "External old name",
          "source": "external-source",
          "packId": "external-pack",
          "configBaseUrl": "https://example.invalid/current",
          "configFiles": ["ModsConfig.xml", "Preferences.xml"],
          "steamCmd": true,
          "workshopCollectionId": "987654321",
          "packSource": "external-repository",
          "installedRevision": 12,
          "installedVersion": "2.3",
          "managedFiles": {"Config/Preferences.xml": "retained-hash"},
          "managedModFolders": ["Local Mod"],
          "installedOrder": ["saved.mod", "missing.mod"],
          "modsArchiveName": "existing.zip",
          "appliedOverlays": {"extra": "retained-overlay"},
          "workshopMods": [{"workshopId": 123456789, "packageId": "saved.mod", "name": "Saved", "futureModField": {"keep": true}}],
          "futureSetting": {"nested": [{"keep": true}, [null, "値", 123456789012345678901234567890]], "value": 1.234567890123456789},
          "futureNull": null,
          "futureBoolean": false
        }
        """;
}

using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PackFrame;

internal static class ListImportTransactionChecks
{
    private static readonly string[] PendingIds = { "draft.first", "missing.pending", "draft.last" };
    private static readonly string[] IncomingIds = { "incoming.first", "incoming.second" };

    internal static void Run(string root)
    {
        var area = Path.Combine(root, "list-import-transactions");
        Directory.CreateDirectory(area);
        try
        {
            CheckCorruptMetadata(area);
            CheckMetadataDirectory(area);
            CheckCommitRollback(area, missingConfig: false);
            CheckCommitRollback(area, missingConfig: true);
            CheckConfigRecovery(area);
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void CheckCorruptMetadata(string area)
    {
        var fixture = CreateFixture(area, "corrupt-metadata");
        var metadataPath = MetadataPath(fixture.Target);
        var originalMetadata = File.ReadAllBytes(metadataPath);
        var cases = new[] { "{broken", "null", "[]", "{\"steamCmd\":\"not-a-boolean\"}" };
        foreach (var contents in cases)
        {
            File.WriteAllText(metadataPath, contents);
            InstanceStore.Discover(fixture.PackRoot);
            var before = Snapshot(fixture.PackRoot);
            var reachedCommit = false;
            ExpectRejection(() => InstanceStore.ImportFileOverwrite(fixture.Target, fixture.Source, "1.6",
                (_, _) => reachedCommit = true));
            Require(!reachedCommit && before.SequenceEqual(Snapshot(fixture.PackRoot)) &&
                HasPending(fixture.Target) && HasPending(fixture.Other) &&
                InstanceStore.BlockedPendingConfigDirectories.Contains(Path.GetFullPath(fixture.Target.ConfigDirectoryPath)),
                "invalid metadata rejects file import before commit and preserves exact files and blocked drafts: " + contents);
            ExpectRejection(() => InstanceStore.ImportFolderOverwrite(
                fixture.PackRoot, fixture.Target, Path.GetDirectoryName(fixture.Source)!, "1.6"));
            Require(before.SequenceEqual(Snapshot(fixture.PackRoot)) && HasPending(fixture.Target),
                "invalid metadata rejects folder-list import without partial writes: " + contents);
            File.WriteAllBytes(metadataPath, originalMetadata);
            Require(InstanceStore.LoadInstance(fixture.Target.DirectoryPath).ActivePackageIds.SequenceEqual(PendingIds) &&
                InstanceStore.LoadInstance(fixture.Other.DirectoryPath).ActivePackageIds.SequenceEqual(new[] { "other.pending" }),
                "repairing metadata restores both exact pending lists after failed imports");
        }
        var otherBefore = Snapshot(fixture.Other.DirectoryPath);
        InstanceStore.ImportFolderOverwrite(fixture.PackRoot, fixture.Target, Path.GetDirectoryName(fixture.Source)!, "1.6");
        CheckSuccessfulImport(fixture, originalMetadata, otherBefore, preservesXml: true);
    }

    private static void CheckMetadataDirectory(string area)
    {
        var fixture = CreateFixture(area, "metadata-directory");
        var metadataPath = MetadataPath(fixture.Target);
        var originalMetadata = File.ReadAllBytes(metadataPath);
        File.Delete(metadataPath);
        Directory.CreateDirectory(metadataPath);
        File.WriteAllText(Path.Combine(metadataPath, "keep.txt"), "unrelated directory contents");
        var before = Snapshot(fixture.PackRoot);
        ExpectRejection(() => InstanceStore.ImportFileOverwrite(fixture.Target, fixture.Source, null));
        Require(before.SequenceEqual(Snapshot(fixture.PackRoot)) && HasPending(fixture.Target) && HasPending(fixture.Other),
            "metadata path that is a directory is rejected without touching its contents, configuration or drafts");
        Directory.Delete(metadataPath, recursive: true);
        File.WriteAllBytes(metadataPath, originalMetadata);
        Require(InstanceStore.LoadInstance(fixture.Target.DirectoryPath).ActivePackageIds.SequenceEqual(PendingIds),
            "metadata directory repair preserves the exact original pending order");
        var otherBefore = Snapshot(fixture.Other.DirectoryPath);
        Require(InstanceStore.ImportFileOverwrite(fixture.Target, fixture.Source, null),
            "file import can retry after repairing the metadata path");
        CheckSuccessfulImport(fixture, originalMetadata, otherBefore, preservesXml: true);
    }

    private static void CheckCommitRollback(string area, bool missingConfig)
    {
        var fixture = CreateFixture(area, missingConfig ? "rollback-new-config" : "rollback-existing-config");
        var metadataPath = MetadataPath(fixture.Target);
        var originalMetadata = File.ReadAllBytes(metadataPath);
        var savedMetadata = metadataPath + ".external-backup";
        if (missingConfig) Directory.Delete(fixture.Target.ConfigDirectoryPath, recursive: true);
        var otherBefore = Snapshot(fixture.Other.DirectoryPath);
        string[]? beforeCommit = null;
        string? stagingDirectory = null;
        ExpectRejection(() => InstanceStore.ImportFileOverwrite(fixture.Target, fixture.Source, "1.6",
            (staging, target) =>
            {
                stagingDirectory = staging;
                var stagedFiles = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(staging, path)).Order(StringComparer.Ordinal).ToArray();
                Require(stagedFiles.SequenceEqual(new[] { Path.Combine("Config", "ModsConfig.xml"), InstanceStore.MetadataFileName }) &&
                    InstanceStore.LoadInstance(staging).ActivePackageIds.SequenceEqual(IncomingIds),
                    "list import stages and validates both intended files before commit");
                // Simulate a destination changing after validation. Config sorts before
                // metadata, so the real transaction must roll it back on this failure.
                File.Move(metadataPath, savedMetadata);
                Directory.CreateDirectory(metadataPath);
                File.WriteAllText(Path.Combine(metadataPath, "keep.txt"), "external change");
                beforeCommit = Snapshot(target);
                FileTransactions.ApplyFiles(staging, target, includeSubdirectories: true);
            }));
        Require(beforeCommit is not null && beforeCommit.SequenceEqual(Snapshot(fixture.Target.DirectoryPath)) &&
            originalMetadata.SequenceEqual(File.ReadAllBytes(savedMetadata)) &&
            otherBefore.SequenceEqual(Snapshot(fixture.Other.DirectoryPath)) &&
            HasPending(fixture.Target) && HasPending(fixture.Other) &&
            stagingDirectory is not null && !Directory.Exists(stagingDirectory),
            missingConfig
                ? "failed metadata commit removes newly created Config and all staged files while preserving drafts and unrelated files"
                : "failed metadata commit restores exact original ModsConfig, preserves metadata and unrelated files, and retains drafts");
        Directory.Delete(metadataPath, recursive: true);
        File.Move(savedMetadata, metadataPath);
        if (!missingConfig)
            Require(InstanceStore.LoadInstance(fixture.Target.DirectoryPath).ActivePackageIds.SequenceEqual(PendingIds),
                "rollback retains the exact pending order after metadata repair");
        Require(InstanceStore.ImportFileOverwrite(fixture.Target, fixture.Source, "1.6"),
            "list import can retry successfully after a mid-commit metadata failure");
        CheckSuccessfulImport(fixture, originalMetadata, otherBefore, preservesXml: !missingConfig);
    }

    private static void CheckConfigRecovery(string area)
    {
        var fixture = CreateFixture(area, "recover-config");
        var metadata = File.ReadAllBytes(MetadataPath(fixture.Target));
        var otherBefore = Snapshot(fixture.Other.DirectoryPath);
        File.WriteAllText(Path.Combine(fixture.Target.ConfigDirectoryPath, "ModsConfig.xml"), "<broken>");
        Require(InstanceStore.ImportFileOverwrite(fixture.Target, fixture.Source, "1.6"),
            "explicit list overwrite still recovers malformed ModsConfig when metadata is valid");
        CheckSuccessfulImport(fixture, metadata, otherBefore, preservesXml: false);
    }

    private static void CheckSuccessfulImport(Fixture fixture, byte[] originalMetadata, string[] otherBefore, bool preservesXml)
    {
        var loaded = InstanceStore.LoadInstance(fixture.Target.DirectoryPath);
        Require(loaded.ActivePackageIds.SequenceEqual(IncomingIds) && loaded.GameVersion == "1.6" &&
            loaded.KnownExpansions.Contains("incoming.expansion") &&
            !HasPending(fixture.Target) && HasPending(fixture.Other) &&
            !InstanceStore.BlockedPendingConfigDirectories.Contains(Path.GetFullPath(fixture.Target.ConfigDirectoryPath)) &&
            InstanceStore.LoadInstance(fixture.Other.DirectoryPath).ActivePackageIds.SequenceEqual(new[] { "other.pending" }) &&
            otherBefore.SequenceEqual(Snapshot(fixture.Other.DirectoryPath)),
            "successful import commits the intended list/version/expansions and clears only its target's draft");
        var expectedMetadata = JsonNode.Parse(originalMetadata)!.AsObject();
        expectedMetadata["source"] = Path.GetFileNameWithoutExtension(fixture.Source);
        Require(JsonNode.DeepEquals(expectedMetadata, JsonNode.Parse(File.ReadAllText(MetadataPath(fixture.Target)))) &&
            loaded.Metadata?.PackId == "retained-pack" && loaded.Metadata.ConfigBaseUrl == "https://example.test/config" &&
            loaded.Metadata.ConfigFiles.SequenceEqual(new[] { "ModsConfig.xml", "Preferences.xml" }) && loaded.IsSteamCmd,
            "successful import retains bindings, display name and unknown metadata and changes only the source");
        Require(File.ReadAllText(Path.Combine(fixture.Target.DirectoryPath, "Saves", "keep.rws")) == "keep saved game" &&
            File.ReadAllText(Path.Combine(fixture.Target.DirectoryPath, "unrelated.bin")) == "keep unrelated bytes" &&
            !Directory.EnumerateDirectories(fixture.Target.DirectoryPath, ".packframe-backup-*").Any() &&
            !Directory.EnumerateDirectories(Path.GetDirectoryName(fixture.Target.DirectoryPath)!, "*.import-*").Any(),
            "successful list import preserves unrelated files and removes staging and transaction backups");
        if (!preservesXml) return;
        var document = XDocument.Load(Path.Combine(fixture.Target.ConfigDirectoryPath, "ModsConfig.xml"));
        var root = document.Root!;
        var ns = root.Name.Namespace;
        Require(ns.NamespaceName == "urn:packframe:checks" && root.Attribute("custom")?.Value == "keep" &&
            root.Element(ns + "futureSetting")?.Element(ns + "child")?.Attribute("enabled")?.Value == "true" &&
            root.Element(ns + "futureSetting")?.Value == "retained" &&
            root.Element(ns + "activeMods")?.Attribute("mode")?.Value == "keep" &&
            root.Element(ns + "knownExpansions")?.Attribute("extra")?.Value == "keep" &&
            root.Element(ns + "version")?.Attribute("flag")?.Value == "keep" &&
            root.Nodes().OfType<XComment>().Any(comment => comment.Value.Contains("untouched")) &&
            File.ReadAllText(Path.Combine(fixture.Target.ConfigDirectoryPath, "Preferences.xml")) == "<preferences>keep</preferences>",
            "list overwrite preserves XML namespace, attributes, comments, extension fields and unrelated configuration");
    }

    private static Fixture CreateFixture(string area, string name)
    {
        InstanceStore.DiscardPendingMods();
        var packRoot = Path.Combine(area, name);
        var target = InstanceStore.CreateNew(packRoot, null, "1.5", new[] { "old.mod" });
        InstanceStore.SetPackBinding(target, "retained-pack", "https://example.test/config",
            new[] { "ModsConfig.xml", "Preferences.xml" }, true);
        InstanceStore.RenameInstance(target, "Retained display name");
        var metadataPath = MetadataPath(target);
        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))!.AsObject();
        metadata["futureMetadata"] = JsonNode.Parse("{\"nested\":[1,\"keep\",null],\"enabled\":true}");
        File.WriteAllText(metadataPath, metadata.ToJsonString());
        File.WriteAllText(Path.Combine(target.ConfigDirectoryPath, "ModsConfig.xml"),
            "<?xml version='1.0' encoding='utf-8'?>\n" +
            "<ModsConfigData xmlns='urn:packframe:checks' custom='keep'>\n<!-- untouched -->\n" +
            "<version flag='keep'>1.5</version><activeMods mode='keep'><li>old.mod</li></activeMods>" +
            "<knownExpansions extra='keep'><li>legacy.expansion</li></knownExpansions>" +
            "<futureSetting><child enabled='true'>retained</child></futureSetting></ModsConfigData>");
        File.WriteAllText(Path.Combine(target.ConfigDirectoryPath, "Preferences.xml"), "<preferences>keep</preferences>");
        File.WriteAllText(Path.Combine(target.DirectoryPath, "Saves", "keep.rws"), "keep saved game");
        File.WriteAllText(Path.Combine(target.DirectoryPath, "unrelated.bin"), "keep unrelated bytes");
        target = InstanceStore.LoadInstance(target.DirectoryPath);
        var other = InstanceStore.CreateNew(packRoot, null, "1.5", new[] { "other.mod" });
        InstanceStore.StageActiveMods(target, PendingIds);
        InstanceStore.StageActiveMods(other, new[] { "other.pending" });
        var sources = Path.Combine(packRoot, "incoming");
        Directory.CreateDirectory(sources);
        var source = Path.Combine(sources, "replacement.xml");
        File.WriteAllText(source,
            "<ModsConfigData><version>1.6</version><activeMods><li>Incoming.First</li><li>incoming.second</li></activeMods>" +
            "<knownExpansions><li>incoming.expansion</li></knownExpansions></ModsConfigData>");
        return new Fixture(packRoot, target, other, source);
    }

    private sealed record Fixture(string PackRoot, ManagedInstance Target, ManagedInstance Other, string Source);

    private static string MetadataPath(ManagedInstance instance) => Path.Combine(instance.DirectoryPath, InstanceStore.MetadataFileName);

    private static string[] Snapshot(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => "file:" + Path.GetRelativePath(directory, path) + ":" +
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))
            .Concat(Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                .Select(path => "dir:" + Path.GetRelativePath(directory, path)))
            .Order(StringComparer.Ordinal).ToArray();

    private static bool HasPending(ManagedInstance instance) =>
        InstanceStore.PendingConfigDirectories.Contains(Path.GetFullPath(instance.ConfigDirectoryPath));

    private static void ExpectRejection(Action action)
    {
        try { action(); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        throw new Exception("Expected rejected list import");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

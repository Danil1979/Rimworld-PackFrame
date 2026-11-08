using System.Text.Json.Nodes;
using System.Xml.Linq;
using PackFrame;

internal static class InstanceCreationChecks
{
    private enum CreationKind { New, Copy, Official }

    public static void Run(string root)
    {
        var checksRoot = Path.Combine(root, "instance-creation");
        foreach (var kind in Enum.GetValues<CreationKind>())
        {
            CheckSuccess(checksRoot, kind);
            CheckInterruptedWrites(checksRoot, kind);
            CheckLateValidation(checksRoot, kind);
            CheckPublicationFailures(checksRoot, kind);
            CheckPreparedReturnModel(checksRoot, kind);
            CheckAllocation(checksRoot, kind);
        }
        CheckInterruptedCopies(checksRoot);
        CheckMissingSources(checksRoot);
        CheckSourceCorruption(checksRoot);
        CheckExclusivePublication(checksRoot);
        CheckCleanupFailure(checksRoot);
    }

    private static void CheckSuccess(string root, CreationKind kind)
    {
        using var fixture = new Fixture(root, kind, "success");
        string? staging = null;
        var created = fixture.Create(new InstanceCreationOperations
        {
            BeforeValidation = path =>
            {
                staging = path;
                Require(Path.GetDirectoryName(path) == fixture.InstancesRoot &&
                    Path.GetFileName(path).StartsWith(".create-", StringComparison.Ordinal),
                    kind + " prepares a non-profile GUID sibling");
                Require(InstanceStore.Discover(fixture.Root).Count == 0,
                    kind + " is invisible to discovery before publication");
            }
        });
        Require(created.FolderName == "profile1" && created.DirectoryPath == Path.Combine(fixture.InstancesRoot, "profile1") &&
            created.ConfigDirectoryPath == Path.Combine(created.DirectoryPath, "Config") && !created.IsDamaged &&
            !created.IsOfficial && !created.IsScratch,
            kind + " returns a healthy ordinary instance rebased to its published path");
        Require(created.ActivePackageIds.SequenceEqual(new[] { "last", "core", "new.mod", "missing.mod" }) &&
            created.GameVersion == (kind == CreationKind.Official ? "1.6" : "1.7") &&
            created.Metadata?.Name == "profile1" && created.Metadata.Source ==
                (kind == CreationKind.Official ? "official" : fixture.Source.FolderName),
            kind + " returns the persisted list, version and identity");
        Require(staging is not null && !Directory.Exists(staging), kind + " removes its staging path after success");
        if (kind != CreationKind.Copy)
            Require(Directory.Exists(Path.Combine(created.DirectoryPath, "Saves")), kind + " retains Saves creation");
        if (kind != CreationKind.New)
        {
            var config = XDocument.Load(Path.Combine(created.ConfigDirectoryPath, "ModsConfig.xml"));
            Require(config.Root!.Attribute("futureAttribute")?.Value == "keep" &&
                config.Root.Element("activeMods")?.Attribute("custom")?.Value == "keep" &&
                config.Root.Element("futureConfig")?.Value == "keep" &&
                created.KnownExpansions.SequenceEqual(new[] { "custom.expansion" }),
                kind + " preserves extra XML fields, attributes and expansion order");
            Require(File.ReadAllText(Path.Combine(created.ConfigDirectoryPath, "Preferences.xml")) == "unrelated preferences" &&
                File.ReadAllText(Path.Combine(created.ConfigDirectoryPath, "Nested", "settings.txt")) == "nested settings",
                kind + " preserves unrelated and nested configuration files");
        }
        if (kind == CreationKind.Copy)
        {
            var metadata = JsonNode.Parse(File.ReadAllText(Path.Combine(created.DirectoryPath, InstanceStore.MetadataFileName)))!;
            Require(metadata["futureMetadata"]?["keep"]?.GetValue<bool>() == true &&
                created.Metadata?.PackId == "bound-pack" && created.Metadata.ConfigBaseUrl == "https://example.com/config" &&
                created.Metadata.ManagedFiles["settings.xml"] == "hash" && created.IsSteamCmd,
                "copy preserves typed bindings and unknown metadata");
            Require(File.ReadAllText(Path.Combine(created.DirectoryPath, "Saves", "save.rws")) == "saved game" &&
                File.ReadAllText(Path.Combine(created.DirectoryPath, "Mods", "nested", "mod.txt")) == "mod data" &&
                !File.Exists(Path.Combine(created.DirectoryPath, "mod-links.txt")),
                "copy preserves saves/mods and removes only the copied link manifest");
        }
        fixture.AssertSourceUnchanged();
        var loaded = InstanceStore.LoadInstance(created.DirectoryPath);
        Require(loaded.ActivePackageIds.SequenceEqual(created.ActivePackageIds) && loaded.GameVersion == created.GameVersion &&
            loaded.Metadata?.Name == created.Metadata?.Name, kind + " returned model agrees with independently loaded output");
    }

    private static void CheckInterruptedWrites(string root, CreationKind kind)
    {
        // Both managed writes have separate interruption points, including a
        // metadata failure after the complete XML has already been written.
        for (var failAt = 1; failAt <= 2; failAt++)
        {
            using var fixture = new Fixture(root, kind, "write-" + failAt);
            var writes = 0;
            ExpectFailure(() => fixture.Create(new InstanceCreationOperations
            {
                WriteAtomic = (path, write) =>
                {
                    if (++writes != failAt) { FileTransactions.WriteAtomic(path, write); return; }
                    FileTransactions.WriteAtomic(path, temporary =>
                    {
                        write(temporary);
                        File.WriteAllText(temporary, "partial write");
                        throw new IOException("injected write interruption");
                    });
                }
            }), kind + " rejects partial write " + failAt);
            Require(writes == failAt, "write failure reached the intended checkpoint");
            fixture.AssertNoPublishedOrStaged();
            fixture.AssertSourceUnchanged();
            fixture.AssertRetry();
        }
    }

    private static void CheckLateValidation(string root, CreationKind kind)
    {
        foreach (var corruption in new[] { "bad-xml", "wrong-xml-root", "missing-xml", "bad-json", "typed-json", "null-json", "missing-json" })
        {
            using var fixture = new Fixture(root, kind, corruption);
            var publishes = 0;
            ExpectFailure(() => fixture.Create(new InstanceCreationOperations
            {
                BeforeValidation = path =>
                {
                    var config = Path.Combine(path, "Config", "ModsConfig.xml");
                    var metadata = Path.Combine(path, InstanceStore.MetadataFileName);
                    switch (corruption)
                    {
                        case "bad-xml": File.WriteAllText(config, "<ModsConfigData>"); break;
                        case "wrong-xml-root": File.WriteAllText(config, "<Wrong><activeMods /></Wrong>"); break;
                        case "missing-xml": File.Delete(config); break;
                        case "bad-json": File.WriteAllText(metadata, "{"); break;
                        case "typed-json": File.WriteAllText(metadata, "{\"steamCmd\":\"not-a-boolean\"}"); break;
                        case "null-json": File.WriteAllText(metadata, "null"); break;
                        case "missing-json": File.Delete(metadata); break;
                    }
                },
                PublishDirectory = (_, _) => publishes++
            }), kind + " validates staged " + corruption + " before publishing");
            Require(publishes == 0, "invalid staged data never reaches publication");
            fixture.AssertNoPublishedOrStaged();
            fixture.AssertSourceUnchanged();
            fixture.AssertRetry();
        }
    }

    private static void CheckPublicationFailures(string root, CreationKind kind)
    {
        foreach (var collision in new[] { "failure", "file", "empty-directory", "sentinel-directory" })
        {
            using var fixture = new Fixture(root, kind, collision);
            var destination = Path.Combine(fixture.InstancesRoot, "profile1");
            ExpectFailure(() => fixture.Create(new InstanceCreationOperations
            {
                PublishDirectory = (staging, target) =>
                {
                    if (collision == "failure") throw new IOException("injected publish failure");
                    if (collision == "file") File.WriteAllText(target, "competing file");
                    else
                    {
                        Directory.CreateDirectory(target);
                        if (collision == "sentinel-directory")
                            File.WriteAllText(Path.Combine(target, "sentinel.txt"), "competing data");
                    }
                    ExclusiveDirectoryPublication.Move(staging, target);
                }
            }), kind + " refuses publication over " + collision);
            if (collision == "file")
                Require(File.ReadAllText(destination) == "competing file", "failed publication preserves competing file");
            else if (collision == "empty-directory")
                Require(Directory.Exists(destination) && !Directory.EnumerateFileSystemEntries(destination).Any(),
                    "failed publication preserves even an empty competing directory");
            else if (collision == "sentinel-directory")
                Require(Directory.GetFileSystemEntries(destination).Length == 1 &&
                    File.ReadAllText(Path.Combine(destination, "sentinel.txt")) == "competing data",
                    "failed publication never merges into or deletes a competing directory");
            else Require(!Directory.Exists(destination), "publish failure creates no final profile");
            fixture.AssertNoStaged();
            fixture.AssertSourceUnchanged();
            var retried = fixture.Create();
            Require(retried.FolderName == (collision == "failure" ? "profile1" : "profile2"),
                kind + " retries cleanly without stealing the competing path");
        }
    }

    private static void CheckPreparedReturnModel(string root, CreationKind kind)
    {
        using var fixture = new Fixture(root, kind, "prepared-return");
        var created = fixture.Create(new InstanceCreationOperations
        {
            PublishDirectory = (staging, target) =>
            {
                ExclusiveDirectoryPublication.Move(staging, target);
                // A read after commit would throw and report a successful creation
                // as a failure. Publication must return the already validated model.
                File.WriteAllText(Path.Combine(target, "Config", "ModsConfig.xml"), "externally changed after commit");
                File.WriteAllText(Path.Combine(target, InstanceStore.MetadataFileName), "externally changed after commit");
            }
        });
        Require(created.DirectoryPath == Path.Combine(fixture.InstancesRoot, "profile1") &&
            created.ActivePackageIds.SequenceEqual(new[] { "last", "core", "new.mod", "missing.mod" }) &&
            created.Metadata?.Name == "profile1" && !created.IsDamaged,
            kind + " performs no failure-prone final reload after commit");
        fixture.AssertSourceUnchanged();
        fixture.AssertNoStaged();
    }

    private static void CheckAllocation(string root, CreationKind kind)
    {
        using (var fixture = new Fixture(root, kind, "allocated-paths"))
        {
            Directory.CreateDirectory(Path.Combine(fixture.InstancesRoot, "PROFILE2"));
            File.WriteAllText(Path.Combine(fixture.InstancesRoot, "profile4"), "existing profile-shaped file");
            Directory.CreateDirectory(Path.Combine(fixture.InstancesRoot, ".create-unowned"));
            File.WriteAllText(Path.Combine(fixture.InstancesRoot, ".create-unowned", "sentinel"), "unowned staging");
            Require(fixture.Create().FolderName == "profile5", kind + " allocates after existing directory and file names");
            Require(File.ReadAllText(Path.Combine(fixture.InstancesRoot, "profile4")) == "existing profile-shaped file" &&
                File.ReadAllText(Path.Combine(fixture.InstancesRoot, ".create-unowned", "sentinel")) == "unowned staging",
                kind + " leaves unrelated files and stale staging alone");
        }
        foreach (var blocked in new[] { false, true })
        {
            using var fixture = new Fixture(root, kind, "reserved-" + blocked);
            // FullPath must resolve the dot segment before comparing paths.
            var missing = new ManagedInstance
            {
                FolderName = "profile8", DirectoryPath = Path.Combine(fixture.InstancesRoot, "other", "..", "profile8"),
                GameVersion = "1.6"
            };
            InstanceStore.StageActiveMods(missing, new[] { "reserved.pending.mod" });
            if (blocked) ExpectFailure(InstanceStore.EnsurePendingWritable, "missing pending profile is blocked for recovery");
            var pendingBefore = InstanceStore.PendingConfigDirectories.Order().ToArray();
            var blockedBefore = InstanceStore.BlockedPendingConfigDirectories.Order().ToArray();
            var created = fixture.Create();
            Require(created.FolderName == "profile9" && !Directory.Exists(Path.Combine(fixture.InstancesRoot, "profile8")),
                kind + " reserves canonical missing " + (blocked ? "blocked" : "pending") + " profile paths");
            Require(InstanceStore.PendingConfigDirectories.Order().SequenceEqual(pendingBefore) &&
                InstanceStore.BlockedPendingConfigDirectories.Order().SequenceEqual(blockedBefore) &&
                created.ActivePackageIds.SequenceEqual(new[] { "last", "core", "new.mod", "missing.mod" }),
                kind + " never inherits, clears or unblocks another path's edits");
        }
        using (var fixture = new Fixture(root, kind, "overflow"))
        {
            File.WriteAllText(Path.Combine(fixture.InstancesRoot, "profile2147483647"), "number exhausted");
            ExpectFailure(() => fixture.Create(), kind + " guards profile index overflow");
            Require(Directory.GetFileSystemEntries(fixture.InstancesRoot).Length == 1 &&
                File.ReadAllText(Path.Combine(fixture.InstancesRoot, "profile2147483647")) == "number exhausted",
                "overflow leaves existing data untouched and never creates a negative profile");
        }
    }

    private static void CheckInterruptedCopies(string root)
    {
        foreach (var kind in new[] { CreationKind.Copy, CreationKind.Official })
        {
            using var fixture = new Fixture(root, kind, "interrupted-copy");
            var copiedFiles = 0;
            ExpectFailure(() => fixture.Create(new InstanceCreationOperations
            {
                CopyFile = (source, target, overwrite) =>
                {
                    if (++copiedFiles != 2) { File.Copy(source, target, overwrite); return; }
                    File.WriteAllText(target, "partial copied file");
                    throw new IOException("injected copy interruption");
                }
            }), kind + " rejects a copy interrupted after partial output");
            Require(copiedFiles == 2, "copy interruption occurs after a completed file");
            fixture.AssertNoPublishedOrStaged();
            fixture.AssertSourceUnchanged();
            fixture.AssertRetry();
        }
    }

    private static void CheckMissingSources(string root)
    {
        foreach (var kind in new[] { CreationKind.Copy, CreationKind.Official })
        {
            foreach (var missing in new[] { "root", "config" })
            {
                using var fixture = new Fixture(root, kind, "missing-" + missing);
                Directory.Delete(missing == "root" ? fixture.Source.DirectoryPath : fixture.Source.ConfigDirectoryPath, true);
                var pendingBefore = InstanceStore.PendingConfigDirectories.Order().ToArray();
                ExpectFailure(() => fixture.Create(), kind + " rejects a missing source " + missing + " folder");
                fixture.AssertNoPublishedOrStaged();
                Require(InstanceStore.PendingConfigDirectories.Order().SequenceEqual(pendingBefore) &&
                    fixture.Source.ActivePackageIds.SequenceEqual(new[] { "last", "core", "new.mod", "missing.mod" }),
                    kind + " retains pending edits after source disappears");
            }
        }
        using (var fixture = new Fixture(root, CreationKind.Official, "first-save"))
        {
            File.Delete(Path.Combine(fixture.Source.ConfigDirectoryPath, "ModsConfig.xml"));
            var created = fixture.Create();
            Require(created.ActivePackageIds.SequenceEqual(fixture.Source.ActivePackageIds),
                "official copy still supports an existing config directory before its first saved XML");
        }
    }

    private static void CheckSourceCorruption(string root)
    {
        using var fixture = new Fixture(root, CreationKind.Copy, "late-source-metadata");
        // The in-memory model remains healthy while its source metadata changes.
        File.WriteAllText(Path.Combine(fixture.Source.DirectoryPath, InstanceStore.MetadataFileName),
            "{\"name\":\"old\",\"steamCmd\":\"invalid\",\"futureMetadata\":true}");
        var badMetadata = File.ReadAllBytes(Path.Combine(fixture.Source.DirectoryPath, InstanceStore.MetadataFileName));
        ExpectFailure(() => fixture.Create(), "copy rejects typed metadata corruption after source was loaded");
        fixture.AssertNoPublishedOrStaged();
        Require(badMetadata.SequenceEqual(File.ReadAllBytes(Path.Combine(fixture.Source.DirectoryPath, InstanceStore.MetadataFileName))) &&
            InstanceStore.HasPendingMods, "source corruption is not rewritten and its edits remain pending");
    }

    private static void CheckExclusivePublication(string root)
    {
        var directory = Path.Combine(root, "native-发布-rename");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "prepared");
        var destination = Path.Combine(directory, "published");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "complete.txt"), "complete");
        Directory.CreateDirectory(destination);
        ExpectFailure(() => ExclusiveDirectoryPublication.Move(source, destination),
            "native exclusive publication rejects a pre-existing empty directory");
        Require(Directory.Exists(source) && !Directory.EnumerateFileSystemEntries(destination).Any(),
            "native collision retains both source and empty destination");
        Directory.Delete(destination);
        ExclusiveDirectoryPublication.Move(source, destination);
        Require(!Directory.Exists(source) && File.ReadAllText(Path.Combine(destination, "complete.txt")) == "complete",
            "native exclusive publication moves the complete tree using Unicode paths");
    }

    private static void CheckCleanupFailure(string root)
    {
        using var fixture = new Fixture(root, CreationKind.New, "cleanup-failure");
        string? retained = null;
        try
        {
            fixture.Create(new InstanceCreationOperations
            {
                BeforeValidation = staging =>
                {
                    retained = staging;
                    throw new IOException("original validation failure");
                },
                DeleteStagingDirectory = _ => throw new IOException("injected cleanup failure")
            });
            throw new Exception("FAIL: expected interrupted creation");
        }
        catch (IOException exception)
        {
            Require(exception.Message == "original validation failure", "cleanup failure never masks the original creation failure");
        }
        Require(retained is not null && Directory.Exists(retained) &&
            File.ReadAllText(AppLog.LogPath).Contains("retained path: " + retained, StringComparison.Ordinal),
            "cleanup failure logs the owned hidden staging path for recovery");
        Require(InstanceStore.Discover(fixture.Root).Count == 0 && fixture.Create().FolderName == "profile1" &&
            Directory.Exists(retained), "retained staging is undiscovered and not reused or deleted by retry");
        fixture.AssertSourceUnchanged();
        Directory.Delete(retained!, true);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CreationKind kind;
        private readonly Dictionary<string, byte[]> sourceFiles;
        private readonly string[] pendingBefore;
        internal string Root { get; }
        internal string InstancesRoot => Path.Combine(Root, InstanceStore.InstancesFolderName);
        internal ManagedInstance Source { get; }

        internal Fixture(string root, CreationKind kind, string name)
        {
            InstanceStore.DiscardPendingMods();
            this.kind = kind;
            Root = Path.GetFullPath(Path.Combine(root, kind + "-" + name));
            Directory.CreateDirectory(InstancesRoot);
            var sourceDirectory = Path.Combine(Root, "source");
            var config = kind == CreationKind.Official ? sourceDirectory : Path.Combine(sourceDirectory, "Config");
            Directory.CreateDirectory(Path.Combine(config, "Nested"));
            File.WriteAllText(Path.Combine(config, "ModsConfig.xml"),
                "<ModsConfigData futureAttribute=\"keep\"><version>1.6</version>" +
                "<activeMods custom=\"keep\"><li>core</li><li>old.mod</li><li>last</li></activeMods>" +
                "<knownExpansions><li>Custom.Expansion</li></knownExpansions><futureConfig>keep</futureConfig></ModsConfigData>");
            File.WriteAllText(Path.Combine(config, "Preferences.xml"), "unrelated preferences");
            File.WriteAllText(Path.Combine(config, "Nested", "settings.txt"), "nested settings");
            if (kind != CreationKind.Official)
            {
                Directory.CreateDirectory(Path.Combine(sourceDirectory, "Saves"));
                Directory.CreateDirectory(Path.Combine(sourceDirectory, "Mods", "nested"));
                File.WriteAllText(Path.Combine(sourceDirectory, "Saves", "save.rws"), "saved game");
                File.WriteAllText(Path.Combine(sourceDirectory, "Mods", "nested", "mod.txt"), "mod data");
                File.WriteAllText(Path.Combine(sourceDirectory, "mod-links.txt"), "original link manifest");
                File.WriteAllText(Path.Combine(sourceDirectory, InstanceStore.MetadataFileName),
                    "{\"name\":\"Original\",\"packId\":\"bound-pack\",\"configBaseUrl\":\"https://example.com/config\"," +
                    "\"steamCmd\":true,\"managedFiles\":{\"settings.xml\":\"hash\"},\"futureMetadata\":{\"keep\":true}}");
            }
            Source = kind == CreationKind.Official
                ? InstanceStore.LoadOfficial(sourceDirectory, "Official") : InstanceStore.LoadInstance(sourceDirectory);
            InstanceStore.StageActiveMods(Source, new[] { "last", "core", "new.mod", "missing.mod" });
            sourceFiles = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes);
            pendingBefore = InstanceStore.PendingConfigDirectories.Order().ToArray();
        }

        internal ManagedInstance Create(InstanceCreationOperations? operations = null)
        {
            operations ??= new InstanceCreationOperations();
            return kind switch
            {
                CreationKind.New => InstanceStore.CreateNew(Root, Source, "1.7", Source.ActivePackageIds, operations),
                CreationKind.Copy => InstanceStore.CopyInstance(Root, Source, "1.7", operations),
                _ => InstanceStore.CopyOfficial(Root, Source, operations)
            };
        }

        internal void AssertSourceUnchanged()
        {
            Require(sourceFiles.Count == Directory.EnumerateFiles(Source.DirectoryPath, "*", SearchOption.AllDirectories).Count() &&
                sourceFiles.All(entry => File.Exists(entry.Key) && entry.Value.SequenceEqual(File.ReadAllBytes(entry.Key))) &&
                Source.ActivePackageIds.SequenceEqual(new[] { "last", "core", "new.mod", "missing.mod" }) &&
                InstanceStore.PendingConfigDirectories.Order().SequenceEqual(pendingBefore),
                kind + " preserves source files, live edits and pending identities");
        }

        internal void AssertNoPublishedOrStaged() => Require(!Directory.EnumerateFileSystemEntries(InstancesRoot).Any(),
            kind + " failure leaves no published profile or owned temporary files");

        internal void AssertNoStaged() => Require(!Directory.EnumerateDirectories(InstancesRoot, ".create-*").Any(),
            kind + " cleans only its staging directory after publication failure");

        internal void AssertRetry()
        {
            Require(Create().FolderName == "profile1", kind + " retry reuses the uncommitted profile number");
            AssertNoStaged();
            AssertSourceUnchanged();
        }

        public void Dispose() => InstanceStore.DiscardPendingMods();
    }

    private static void ExpectFailure(Action action, string message)
    {
        try { action(); }
        catch (IOException) { Require(true, message); return; }
        throw new Exception("FAIL: expected IOException: " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

using PackFrame;

internal static class PendingRecoveryChecks
{
    internal static void Run(string root)
    {
        var area = Path.Combine(root, "pending-recovery");
        var first = InstanceStore.CreateNew(area, null, "1.6", new[] { "saved.first", "missing.saved" });
        var second = InstanceStore.CreateNew(area, null, "1.6", new[] { "saved.second" });
        var path = Path.Combine(first.ConfigDirectoryPath, "ModsConfig.xml");
        var other = Path.Combine(second.ConfigDirectoryPath, "ModsConfig.xml");
        var metadata = Path.Combine(first.DirectoryPath, InstanceStore.MetadataFileName);
        var valid = File.ReadAllBytes(path);
        var otherValid = File.ReadAllBytes(other);
        var validMetadata = File.ReadAllBytes(metadata);
        var draft = new[] { "saved.first", "missing.saved", "new.first" };
        bool Pending(ManagedInstance instance) => InstanceStore.PendingConfigDirectories.Contains(Path.GetFullPath(instance.ConfigDirectoryPath));
        bool Blocked(ManagedInstance instance) => InstanceStore.BlockedPendingConfigDirectories.Contains(Path.GetFullPath(instance.ConfigDirectoryPath));
        void StageBoth()
        {
            InstanceStore.StageActiveMods(first, draft);
            InstanceStore.StageActiveMods(second, new[] { "saved.second", "new.second" });
        }
        try
        {
            StageBoth();
            File.WriteAllText(path, "<broken>");
            ExpectBlocked(InstanceStore.SavePendingMods, "Save rejects newly corrupted configuration before Refresh");
            Require(File.ReadAllText(path) == "<broken>" && File.ReadAllBytes(other).SequenceEqual(otherValid),
                "preflight checks all instances before writing even a healthy target");
            Require(Pending(first) && Pending(second) && Blocked(first) && !Blocked(second),
                "failed Save retains each staged list and identifies only the unreadable target");
            ExpectBlocked(() => InstanceStore.StageActiveMods(first, new[] { "replacement" }),
                "a stale healthy instance cannot replace a retained blocked draft");
            File.WriteAllBytes(path, valid);
            ExpectBlocked(InstanceStore.SavePendingMods, "repair requires a successful reload before saving the retained draft");
            first = InstanceStore.Discover(area).Single(x => x.FolderName == first.FolderName);
            Require(!first.IsDamaged && !Blocked(first) && first.ActivePackageIds.SequenceEqual(draft),
                "repair and reload restore the complete staged order including unresolved IDs");
            InstanceStore.SavePendingMods();
            Require(!InstanceStore.HasPendingMods && InstanceStore.BlockedPendingConfigDirectories.Count == 0 &&
                InstanceStore.Discover(area).Single(x => x.FolderName == first.FolderName).ActivePackageIds.SequenceEqual(draft),
                "recovered Save persists staged changes and clears both pending and blocked states");

            File.WriteAllBytes(path, valid);
            File.WriteAllBytes(other, otherValid);
            StageBoth();
            File.WriteAllText(metadata, "{broken");
            var damaged = InstanceStore.Discover(area).Single(x => x.FolderName == first.FolderName);
            Require(damaged.IsDamaged && Pending(damaged) && Blocked(damaged),
                "discovery isolates malformed metadata without dropping the staged list");
            ExpectBlocked(InstanceStore.SavePendingMods, "discovered metadata damage blocks all pending writes");
            Require(File.ReadAllBytes(path).SequenceEqual(valid) && File.ReadAllBytes(other).SequenceEqual(otherValid),
                "metadata recovery leaves both configuration files untouched");
            File.WriteAllText(path, "<broken>");
            File.WriteAllBytes(metadata, validMetadata);
            damaged = InstanceStore.Discover(area).Single(x => x.FolderName == first.FolderName);
            Require(damaged.IsDamaged && Blocked(damaged) && Pending(damaged),
                "repairing metadata alone does not release a still-corrupt mod list");
            File.Delete(path);
            damaged = InstanceStore.Discover(area).Single(x => x.FolderName == first.FolderName);
            Require(damaged.IsDamaged && Blocked(damaged) && !File.Exists(path),
                "a missing pending configuration is not accepted as a repaired empty instance");
            Directory.CreateDirectory(path);
            ExpectBlocked(InstanceStore.SavePendingMods, "an unreadable directory target remains blocked");
            Directory.Delete(path);
            File.WriteAllBytes(path, valid);
            first = InstanceStore.Discover(area).Single(x => x.FolderName == first.FolderName);
            Require(first.ActivePackageIds.SequenceEqual(draft) && !Blocked(first) && Pending(second),
                "repeated failed recovery retains the original draft and independent instance work");
            InstanceStore.DiscardPendingMods();
            StageBoth();
            var moved = first.DirectoryPath + ".temporarily-moved";
            Directory.Move(first.DirectoryPath, moved);
            ExpectBlocked(InstanceStore.SavePendingMods, "a vanished edited instance is not silently recreated by Save");
            Require(!Directory.Exists(first.DirectoryPath) && File.ReadAllBytes(other).SequenceEqual(otherValid),
                "missing-instance preflight preserves directory absence and unrelated saved files");
            Directory.Move(moved, first.DirectoryPath);
            first = InstanceStore.Discover(area).Single(x => x.FolderName == first.FolderName);
            Require(first.ActivePackageIds.SequenceEqual(draft) && !Blocked(first),
                "restoring a vanished instance and reloading recovers its staged list");
            InstanceStore.DiscardPendingMods();

            var official = InstanceStore.LoadOfficial(first.ConfigDirectoryPath, "Official fixture");
            InstanceStore.StageActiveMods(official, draft);
            File.WriteAllText(path, "<broken>");
            var damagedOfficial = InstanceStore.LoadOfficial(first.ConfigDirectoryPath, "Official fixture");
            Require(damagedOfficial.IsDamaged && Pending(official) && Blocked(official),
                "official reload also retains unreadable staged edits");
            File.WriteAllBytes(path, valid);
            official = InstanceStore.LoadOfficial(first.ConfigDirectoryPath, "Official fixture");
            Require(!official.IsDamaged && !Blocked(official) && official.ActivePackageIds.SequenceEqual(draft),
                "official repair and reload recover the same draft");
            InstanceStore.RemovePending(official);
            Require(!InstanceStore.HasPendingMods && InstanceStore.BlockedPendingConfigDirectories.Count == 0,
                "explicit removal clears only the intended recovered draft");
            InstanceStore.StageActiveMods(official, draft);
            File.WriteAllText(path, "<broken>");
            InstanceStore.LoadOfficial(first.ConfigDirectoryPath, "Official fixture");
            InstanceStore.RemovePending(official);
            Require(!InstanceStore.HasPendingMods && InstanceStore.BlockedPendingConfigDirectories.Count == 0,
                "explicit removal clears a blocked draft without a stale save lock");
            File.WriteAllBytes(path, valid);
            StageBoth();
            File.WriteAllText(metadata, "{broken");
            ExpectBlocked(InstanceStore.SavePendingMods, "Save detects metadata corruption even before discovery");
            InstanceStore.DiscardPendingMods();
            Require(!InstanceStore.HasPendingMods && InstanceStore.BlockedPendingConfigDirectories.Count == 0,
                "explicit discard clears blocked and healthy drafts without writing");
            File.WriteAllBytes(metadata, validMetadata);
            StageBoth();
            File.Delete(metadata);
            Directory.CreateDirectory(metadata);
            ExpectBlocked(InstanceStore.SavePendingMods, "a directory at the metadata path cannot be treated as optional missing metadata");
            Directory.Delete(metadata);
            File.WriteAllBytes(metadata, validMetadata);
            InstanceStore.DiscardPendingMods();

            var newOfficial = InstanceStore.LoadOfficial(Path.Combine(area, "new-official"), "New official");
            InstanceStore.StageActiveMods(newOfficial, new[] { "first.mod" });
            InstanceStore.SavePendingMods();
            Require(InstanceStore.LoadOfficial(newOfficial.DirectoryPath, "New official").ActivePackageIds.SequenceEqual(new[] { "first.mod" }),
                "a new official configuration still supports its intentional first Save");
            foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
                Require(PendingInstanceSummary.BlockedCaption(language, 2, 1).Contains("2") &&
                    PendingInstanceSummary.BlockedCaption(language, 2, 1).Contains("1") &&
                    !string.IsNullOrWhiteSpace(PendingInstanceSummary.RecoveryHint(language)) &&
                    !string.IsNullOrWhiteSpace(PendingInstanceSummary.SaveBlocked(language)),
                    "blocked draft recovery has count, persistent instructions and save feedback in " + language);
            var rows = PendingInstanceSummary.Rows(new[] { first.ConfigDirectoryPath, second.ConfigDirectoryPath },
                new[] { first, second }, new[] { first.ConfigDirectoryPath });
            Require(rows[0].Name.StartsWith("⚠ ") && !rows[1].Name.StartsWith("⚠ ") && rows[0].ConfigDirectoryPath == first.ConfigDirectoryPath,
                "pending review identifies blocked targets even before instance reload");
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void ExpectBlocked(Action action, string message)
    {
        try { action(); throw new Exception("FAIL: " + message); }
        catch (PendingRecoveryException) { Require(true, message); }
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

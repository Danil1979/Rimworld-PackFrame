using PackFrame;

internal static class PendingEditIdentityChecks
{
    internal static void Run(string root)
    {
        var area = Path.Combine(root, "pending-edit-identity");
        var first = InstanceStore.CreateNew(area, null, "1.6", new[] { "saved.one", "missing.saved" });
        var second = InstanceStore.CreateNew(area, null, "1.6", new[] { "saved.two" });
        var original = first.ActivePackageIds.ToArray();
        var path = Path.Combine(first.ConfigDirectoryPath, "ModsConfig.xml");
        var bytes = File.ReadAllBytes(path);
        bool Pending(ManagedInstance instance) => InstanceStore.PendingConfigDirectories.Contains(Path.GetFullPath(instance.ConfigDirectoryPath));
        try
        {
            InstanceStore.StageActiveMods(first, original);
            Require(!Pending(first) && File.ReadAllBytes(path).SequenceEqual(bytes),
                "staging an unchanged saved list creates no unsaved state or disk write");
            InstanceStore.StageActiveMods(first, original.Reverse().ToArray());
            Require(Pending(first), "saved-state comparison retains load-order changes with identical membership");
            InstanceStore.StageActiveMods(first, original.Append(original[0]).ToArray());
            Require(Pending(first), "saved-state comparison preserves duplicate occurrence differences");
            InstanceStore.StageActiveMods(first, original.Append("new.first").ToArray());
            InstanceStore.StageActiveMods(second, new[] { "saved.two", "new.second" });
            Require(Pending(first) && Pending(second), "distinct instance edits are independently pending");
            InstanceStore.StageActiveMods(first, original);
            Require(!Pending(first) && Pending(second) && first.ActivePackageIds.SequenceEqual(original),
                "returning to saved order clears only that instance while retaining unresolved IDs");
            InstanceStore.StageActiveMods(first, original.Append("new.first").ToArray());
            Require(Pending(first), "redo after returning to saved order marks the instance edited again");
            InstanceStore.SavePendingMods();
            var savedAgain = File.ReadAllBytes(path);
            InstanceStore.StageActiveMods(first, original);
            Require(Pending(first), "undo after Save compares against the new disk state, not an obsolete baseline");
            InstanceStore.StageActiveMods(first, original.Append("new.first").ToArray());
            Require(!Pending(first) && File.ReadAllBytes(path).SequenceEqual(savedAgain),
                "returning to the new saved state clears pending without rewriting the file");

            InstanceStore.UpdateActiveMods(first, new[] { "external.change" });
            InstanceStore.StageActiveMods(first, original);
            Require(Pending(first), "external disk changes are not confused with an earlier undo baseline");
            InstanceStore.StageActiveMods(first, new[] { "external.change" });
            Require(!Pending(first), "an edit matching the current disk contents is clean");
            var version = first.GameVersion;
            first.GameVersion = "1.7";
            InstanceStore.StageActiveMods(first, new[] { "external.change" });
            Require(Pending(first), "unchanged mod IDs do not hide staged game-version changes");
            first.GameVersion = version;
            var known = first.KnownExpansions.ToList();
            first.KnownExpansions.Add("additional.expansion");
            InstanceStore.StageActiveMods(first, new[] { "external.change" });
            Require(Pending(first), "unchanged mod IDs do not hide staged expansion changes");
            first.KnownExpansions = known;
            InstanceStore.StageActiveMods(first, new[] { " EXTERNAL.CHANGE " });
            Require(!Pending(first), "saved-state comparison follows package-ID normalization");

            var valid = File.ReadAllBytes(path);
            File.Delete(path);
            InstanceStore.StageActiveMods(first, Array.Empty<string>());
            Require(Pending(first) && !File.Exists(path), "a missing file is not a confirmed saved empty list");
            File.WriteAllText(path, "{broken");
            InstanceStore.StageActiveMods(first, Array.Empty<string>());
            Require(Pending(first) && File.ReadAllText(path) == "{broken",
                "unreadable configuration cannot clear pending state or be replaced by comparison");
            File.Delete(path);
            Directory.CreateDirectory(path);
            InstanceStore.StageActiveMods(first, Array.Empty<string>());
            Require(Pending(first) && Directory.Exists(path), "an unreadable directory target cannot look like a saved empty list");
            Directory.Delete(path);
            File.WriteAllBytes(path, valid);
            InstanceStore.StageActiveMods(first, new[] { "external.change" });
            Require(!Pending(first), "saved-state comparison recovers after the config file is repaired");
            var official = InstanceStore.LoadOfficial(first.ConfigDirectoryPath, "Official fixture");
            InstanceStore.StageActiveMods(official, official.ActivePackageIds.ToArray());
            Require(!Pending(official), "official configuration uses the same clean-state comparison");
            var scratch = new ManagedInstance { FolderName = "scratch", DirectoryPath = string.Empty, IsScratch = true };
            InstanceStore.StageActiveMods(scratch, new[] { "scratch.mod" });
            Require(scratch.ActivePackageIds.SequenceEqual(new[] { "scratch.mod" }) && !InstanceStore.HasPendingMods,
                "scratch edits remain in memory and never become saved-file work");
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

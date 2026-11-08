using PackFrame;

internal static class InstanceCreationHistoryChecks
{
    internal static void Run(string root)
    {
        foreach (var kind in new[] { "new", "copy", "official-copy" })
            PreserveExistingHistory(Path.Combine(root, "creation-history-" + kind), kind);
        CheckScratch(Path.Combine(root, "creation-scratch-history"));
        Check(InstanceCreationHistory.ClearScratchAfterCreate(null).Count == 0,
            "creation without a selected source introduces no list undo entry");
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
            Check(InstanceCreationHistory.ScratchCreated(language, "profile9").Contains("profile9") &&
                !string.IsNullOrWhiteSpace(InstanceCreationHistory.ScratchName(language)) &&
                InstanceCreationHistory.ScratchHint(language).Contains(InstanceCreationHistory.CreateFromScratch(language)),
                $"scratch creation feedback names the saved instance ({language})");
        var unnamed = new ManagedInstance { FolderName = "__scratch__", DirectoryPath = string.Empty, IsScratch = true };
        var named = new ManagedInstance { FolderName = "profile9", DirectoryPath = "unused", Metadata = new InstanceMetadata { Name = "Colony", SteamCmd = true } };
        Check(InstanceCreationHistory.SelectorName(unnamed, "en") == "Scratch list" &&
            InstanceCreationHistory.SelectorName(named, "en") == "Colony (cmd)",
            "every selector refresh can label scratch without changing ordinary names or status suffixes");
        Check(InstanceCreationHistory.ScratchCreated("en", "profile9").Contains("keeps the new instance"),
            "scratch Undo guidance does not imply deleting the created profile");
    }

    private static void PreserveExistingHistory(string root, string kind)
    {
        var first = InstanceStore.CreateNew(root, null, "1.6", new[] { "first.saved" });
        var other = InstanceStore.CreateNew(root, null, "1.6", new[] { "other.saved" });
        var before = new[] { new InstanceSnapshot(first.FolderName, first.ActivePackageIds.ToList()),
            new InstanceSnapshot(other.FolderName, other.ActivePackageIds.ToList()) };
        InstanceStore.StageActiveMods(first, new[] { "first.edited", "unresolved.mod" });
        InstanceStore.StageActiveMods(other, new[] { "other.edited" });
        var officialPath = Path.Combine(root, "official");
        Directory.CreateDirectory(officialPath);
        File.Copy(Path.Combine(first.ConfigDirectoryPath, "ModsConfig.xml"), Path.Combine(officialPath, "ModsConfig.xml"));
        var official = InstanceStore.LoadOfficial(officialPath, "Official");
        InstanceStore.StageActiveMods(official, new[] { "official.edited" });
        var created = kind switch
        {
            "new" => InstanceStore.CreateNew(root, first, "1.6"),
            "copy" => InstanceStore.CopyInstance(root, first),
            _ => InstanceStore.CopyOfficial(root, official)
        };
        var createdBytes = Snapshot(created.DirectoryPath);
        var sourceBytes = Snapshot(first.DirectoryPath);
        Check(InstanceCreationHistory.ClearScratchAfterCreate(first).Count == 0 &&
            InstanceCreationHistory.ClearScratchAfterCreate(official).Count == 0,
            $"{kind} does not introduce a list history entry or clear managed/official drafts");
        var loaded = InstanceStore.Discover(root);
        var redo = ModListOperations.Restore(before, loaded);
        Check(loaded.Single(x => x.FolderName == first.FolderName).ActivePackageIds.SequenceEqual(new[] { "first.saved" }) &&
            loaded.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.saved" }),
            $"{kind} leaves previous target identities valid for Undo");
        ModListOperations.Restore(redo, loaded);
        Check(loaded.Single(x => x.FolderName == first.FolderName).ActivePackageIds.SequenceEqual(new[] { "first.edited", "unresolved.mod" }) &&
            loaded.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.edited" }) &&
            Same(createdBytes, Snapshot(created.DirectoryPath)) && Same(sourceBytes, Snapshot(first.DirectoryPath)),
            $"{kind} keeps previous Redo independent from new and source files");
        InstanceStore.DiscardPendingMods();
    }

    private static void CheckScratch(string root)
    {
        var backing = new List<string> { "missing.scratch", "scratch.last" };
        var scratch = new ManagedInstance { FolderName = "__scratch__", DirectoryPath = string.Empty,
            IsScratch = true, ActivePackageIds = backing };
        var other = InstanceStore.CreateNew(root, null, "1.6", new[] { "other.saved" });
        // Earlier cross-profile operation moved one item from scratch to another profile.
        var earlier = new[] { new InstanceSnapshot(scratch.FolderName, new() { "missing.scratch", "moved.mod", "scratch.last" }),
            new InstanceSnapshot(other.FolderName, new() { "other.saved" }) };
        InstanceStore.StageActiveMods(other, new[] { "other.saved", "moved.mod" });
        var created = InstanceStore.CreateNew(root, scratch, "1.6", backing.ToList());
        var bytes = Snapshot(created.DirectoryPath);
        var conversion = InstanceCreationHistory.ClearScratchAfterCreate(scratch);
        Check(conversion.Count == 1 && conversion[0].FolderName == scratch.FolderName &&
            conversion[0].ActivePackageIds.SequenceEqual(new[] { "missing.scratch", "scratch.last" }) &&
            ReferenceEquals(backing, scratch.ActivePackageIds) && backing.Count == 0,
            "scratch conversion captures the old list and clears the shared scratch object");
        Check(created.ActivePackageIds.SequenceEqual(new[] { "missing.scratch", "scratch.last" }) &&
            InstanceCreationHistory.ClearScratchAfterCreate(scratch).Count == 0,
            "new instance retains missing scratch IDs and empty scratch introduces no extra history entry");
        var instances = InstanceStore.Discover(root);
        instances.Add(new ManagedInstance { FolderName = scratch.FolderName, DirectoryPath = string.Empty,
            IsScratch = true, ActivePackageIds = backing });
        var conversionRedo = ModListOperations.Restore(conversion, instances);
        Check(backing.SequenceEqual(new[] { "missing.scratch", "scratch.last" }) &&
            Same(bytes, Snapshot(created.DirectoryPath)),
            "Undo restores unresolved scratch IDs without removing or modifying the created instance");
        var earlierRedo = ModListOperations.Restore(earlier, instances);
        Check(backing.SequenceEqual(new[] { "missing.scratch", "moved.mod", "scratch.last" }) &&
            instances.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.saved" }),
            "earlier cross-profile Undo remains valid after undoing scratch conversion");
        ModListOperations.Restore(earlierRedo, instances);
        ModListOperations.Restore(conversionRedo, instances);
        Check(backing.Count == 0 &&
            instances.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.saved", "moved.mod" }) &&
            Same(bytes, Snapshot(created.DirectoryPath)),
            "cross-profile Redo followed by scratch-clear Redo keeps the independently saved instance unchanged");
        InstanceStore.DiscardPendingMods();
    }

    private static Dictionary<string, byte[]> Snapshot(string path) => Directory.GetFiles(path, "*", SearchOption.AllDirectories)
        .ToDictionary(p => Path.GetRelativePath(path, p), File.ReadAllBytes);
    private static bool Same(Dictionary<string, byte[]> a, Dictionary<string, byte[]> b) => a.Count == b.Count &&
        a.All(p => b.TryGetValue(p.Key, out var bytes) && p.Value.SequenceEqual(bytes));
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }
}

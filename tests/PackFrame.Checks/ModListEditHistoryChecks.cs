using PackFrame;
using System.Reflection;

internal static class ModListEditHistoryChecks
{
    private sealed record Entry(string Name, List<InstanceSnapshot> Instances);

    internal static void Run(string root)
    {
        CheckDependencies();
        CheckImport(Path.Combine(root, "selective-import-history"));
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UiWiring.MainWindow.axaml.cs")!;
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();
        var start = source.IndexOf("var overwritten = await RunInstanceWorkAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private void OpenInstance", start, StringComparison.Ordinal);
        var overwrite = source[start..end];
        Check(!overwrite.Contains("ClearUndoHistory()") &&
            overwrite.IndexOf("if (!overwritten)", StringComparison.Ordinal) < overwrite.IndexOf("ModListEditHistory.InvalidateInstance", StringComparison.Ordinal) &&
            overwrite.Contains("InvalidateInstance(_undoHistory, target.FolderName, entry => entry.Instances)") &&
            overwrite.Contains("InvalidateInstance(_redoHistory, target.FolderName, entry => entry.Instances)"),
            "overwrite invalidates both stacks only after successful replacement, without globally clearing history");
    }

    private static void CheckDependencies()
    {
        static Entry E(string name, params string[] folders) => new(name,
            folders.Select(folder => new InstanceSnapshot(folder, new() { name })).ToList());
        // End is next to replay for both Undo and Redo. An affected multi-list
        // command blocks earlier replay for all of its lists, transitively.
        foreach (var stack in new[] { "Undo", "Redo" })
        {
            var color = E("color"); // Pack-wide colors have no list snapshots.
            var independent = E("independent", "D");
            var stillValid = E("before-boundary", "B");
            var history = new List<Entry> { E("old-C", "C"), color, E("B-to-C", "B", "C"),
                independent, E("A-to-B", "A", "B"), stillValid, E("target", "a") };
            ModListEditHistory.InvalidateInstance(history, "A", entry => entry.Instances);
            Check(history.SequenceEqual(new[] { color, independent, stillValid }),
                $"{stack} preserves independent order and colors, removes whole affected moves and transitive stale snapshots");
            ModListEditHistory.InvalidateInstance(history, "A", entry => entry.Instances);
            Check(history.SequenceEqual(new[] { color, independent, stillValid }),
                $"{stack} repeated invalidation is stable and compares instance identities case-insensitively");
            ModListEditHistory.InvalidateInstance(history, "D", entry => entry.Instances);
            Check(history.SequenceEqual(new[] { color, stillValid }), $"{stack} later overwrite invalidates only its own remaining dependencies");
        }
        var empty = new List<Entry>();
        ModListEditHistory.InvalidateInstance(empty, "A", entry => entry.Instances);
        Check(empty.Count == 0, "empty history is safe to invalidate");
    }

    private static void CheckImport(string root)
    {
        var target = InstanceStore.CreateNew(root, null, "1.6", new[] { "target.saved" });
        var other = InstanceStore.CreateNew(root, null, "1.6", new[] { "other.saved" });
        var untouched = File.ReadAllBytes(Path.Combine(other.ConfigDirectoryPath, "ModsConfig.xml"));
        var history = new List<Entry>();
        Entry Capture(string name, params ManagedInstance[] instances) => new(name,
            instances.Select(instance => new InstanceSnapshot(instance.FolderName, instance.ActivePackageIds.ToList())).ToList());
        history.Add(Capture("other-first", other));
        InstanceStore.StageActiveMods(other, new[] { "other.first", "missing.mod" });
        history.Add(Capture("target-edit", target));
        InstanceStore.StageActiveMods(target, new[] { "target.draft" });
        history.Add(Capture("other-second", other));
        InstanceStore.StageActiveMods(other, new[] { "other.second", "missing.mod" });
        var instances = new[] { target, other };
        // Undo latest independent edit, leaving it on Redo while replacing target.
        var redo = new List<Entry> { new("other-second", ModListOperations.Restore(history[^1].Instances, instances)) };
        history.RemoveAt(history.Count - 1);
        var invalid = Path.Combine(root, "invalid.xml");
        File.WriteAllText(invalid, "<unrelated />");
        Check(!InstanceStore.ImportFileOverwrite(target, invalid, "1.6") && history.Count == 2 && redo.Count == 1 &&
            target.ActivePackageIds.SequenceEqual(new[] { "target.draft" }) &&
            other.ActivePackageIds.SequenceEqual(new[] { "other.first", "missing.mod" }),
            "failed import leaves target and unrelated drafts and both history stacks intact");
        var imported = Path.Combine(root, "import.xml");
        File.WriteAllText(imported, "<ModsConfigData><version>1.6</version><activeMods><li>replacement.mod</li></activeMods></ModsConfigData>");
        Check(InstanceStore.ImportFileOverwrite(target, imported, "1.6"), "list overwrite succeeds before selective invalidation");
        ModListEditHistory.InvalidateInstance(history, target.FolderName, entry => entry.Instances);
        ModListEditHistory.InvalidateInstance(redo, target.FolderName, entry => entry.Instances);
        Check(history.Count == 1 && history[0].Name == "other-first" && redo.Count == 1,
            "successful overwrite preserves unrelated pending Undo and Redo");
        var targetBytes = File.ReadAllBytes(Path.Combine(target.ConfigDirectoryPath, "ModsConfig.xml"));
        var loaded = InstanceStore.Discover(root);
        ModListOperations.Restore(redo[0].Instances, loaded);
        Check(loaded.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.second", "missing.mod" }),
            "preserved Redo restores unrelated draft including unresolved package IDs");
        var inverse = ModListOperations.Restore(history[0].Instances, loaded);
        Check(loaded.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.saved" }),
            "preserved Undo restores unrelated saved list");
        ModListOperations.Restore(inverse, loaded);
        Check(targetBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(target.ConfigDirectoryPath, "ModsConfig.xml"))) &&
            untouched.SequenceEqual(File.ReadAllBytes(Path.Combine(other.ConfigDirectoryPath, "ModsConfig.xml"))) &&
            loaded.Single(x => x.FolderName == target.FolderName).ActivePackageIds.SequenceEqual(new[] { "replacement.mod" }),
            "retained history never reverts replacement or writes unrelated list files");
        InstanceStore.DiscardPendingMods();
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }
}

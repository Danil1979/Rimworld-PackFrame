using PackFrame;
using System.Reflection;

internal static class ModpackInstanceSafetyChecks
{
    internal static void Run(string root)
    {
        CheckExistingHistory(Path.Combine(root, "history"));
        CheckCompletionWiring();
        CheckConfigReplacementHistory(Path.Combine(root, "config-history"));
        CheckPendingIdentity(Path.Combine(root, "pending"));
        CheckCollisionAndRetry(Path.Combine(root, "collision"));
        CheckRecoveryFailure(Path.Combine(root, "recovery"), recreateSource: false);
        CheckRecoveryFailure(Path.Combine(root, "recreated"), recreateSource: true);
        CheckReservedNames(Path.Combine(root, "names"));
    }

    private static void CheckExistingHistory(string root)
    {
        InstanceStore.DiscardPendingMods();
        try
        {
            var first = InstanceStore.CreateNew(root, null, "1.6", new[] { "first.saved" });
            var other = InstanceStore.CreateNew(root, null, "1.6", new[] { "other.saved" });
            var before = new[] { new InstanceSnapshot(first.FolderName, first.ActivePackageIds.ToList()),
                new InstanceSnapshot(other.FolderName, other.ActivePackageIds.ToList()) };
            InstanceStore.StageActiveMods(first, new[] { "first.edited", "missing.mod" });
            InstanceStore.StageActiveMods(other, new[] { "other.edited" });
            var existing = new[] { first, other };
            var redo = ModListOperations.Restore(before, existing);
            Require(first.ActivePackageIds.SequenceEqual(new[] { "first.saved" }) &&
                other.ActivePackageIds.SequenceEqual(new[] { "other.saved" }),
                "existing cross-list Undo works before installation and leaves a valid Redo snapshot");
            var undo = ModListOperations.Restore(redo, existing);
            Require(first.ActivePackageIds.SequenceEqual(new[] { "first.edited", "missing.mod" }) &&
                other.ActivePackageIds.SequenceEqual(new[] { "other.edited" }),
                "existing cross-list Redo restores unresolved draft IDs before installation");
            // Leave Redo available at the point the new profile is installed.
            redo = ModListOperations.Restore(undo, existing);
            var installed = ModpackInstanceInstaller.Create(root, "1.6", Fixture(root));
            var installedBytes = Directory.GetFiles(installed.DirectoryPath, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(installed.DirectoryPath, path), File.ReadAllBytes);
            var loaded = InstanceStore.Discover(root);
            undo = ModListOperations.Restore(redo, loaded);
            Require(loaded.Single(x => x.FolderName == first.FolderName).ActivePackageIds.SequenceEqual(new[] { "first.edited", "missing.mod" }) &&
                loaded.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.edited" }),
                "installing a distinct profile preserves existing cross-list Redo after rediscovery");
            redo = ModListOperations.Restore(undo, loaded);
            Require(loaded.Single(x => x.FolderName == first.FolderName).ActivePackageIds.SequenceEqual(new[] { "first.saved" }) &&
                loaded.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.saved" }),
                "installing a distinct profile preserves existing cross-list Undo after rediscovery");
            ModListOperations.Restore(redo, loaded);
            InstanceStore.SavePendingMods();
            var finalFiles = Directory.GetFiles(installed.DirectoryPath, "*", SearchOption.AllDirectories);
            Require(finalFiles.Length == installedBytes.Count && finalFiles.All(path =>
                installedBytes.TryGetValue(Path.GetRelativePath(installed.DirectoryPath, path), out var bytes) &&
                bytes.SequenceEqual(File.ReadAllBytes(path))) &&
                InstanceStore.LoadInstance(installed.DirectoryPath).ActivePackageIds.SequenceEqual(new[] { "installed.mod" }),
                "old Undo/Redo and subsequent Save leave every installed profile byte unchanged");
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void CheckConfigReplacementHistory(string root)
    {
        InstanceStore.DiscardPendingMods();
        try
        {
            var target = InstanceStore.CreateNew(root, null, "1.6", new[] { "target.saved" });
            var other = InstanceStore.CreateNew(root, null, "1.6", new[] { "other.saved" });
            var undo = new List<List<InstanceSnapshot>>
            {
                new() { new(target.FolderName, new() { "target.old" }) },
                new() { new(other.FolderName, new() { "other.old" }) }
            };
            var redo = new List<List<InstanceSnapshot>>
            {
                new() { new(target.FolderName, new() { "target.future" }) },
                new() { new(other.FolderName, new() { "other.future", "missing.mod" }) }
            };
            var staging = Path.Combine(root, "config-staging");
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, "ModsConfig.xml"),
                "<ModsConfigData><version>1.6</version><activeMods><li>replacement.mod</li></activeMods></ModsConfigData>");
            FileTransactions.ApplyFiles(staging, target.ConfigDirectoryPath);
            ModListEditHistory.InvalidateInstance(undo, target.FolderName, entry => entry);
            ModListEditHistory.InvalidateInstance(redo, target.FolderName, entry => entry);
            Require(undo.Count == 1 && redo.Count == 1 && undo[0][0].FolderName == other.FolderName &&
                redo[0][0].FolderName == other.FolderName,
                "committed configuration replacement removes target Undo and Redo but preserves unrelated history");
            var replacementBytes = File.ReadAllBytes(Path.Combine(target.ConfigDirectoryPath, "ModsConfig.xml"));
            var loaded = InstanceStore.Discover(root);
            ModListOperations.Restore(undo[0], loaded);
            Require(loaded.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.old" }),
                "unrelated Undo still works after committed configuration replacement");
            ModListOperations.Restore(redo[0], loaded);
            InstanceStore.SavePendingMods();
            Require(loaded.Single(x => x.FolderName == other.FolderName).ActivePackageIds.SequenceEqual(new[] { "other.future", "missing.mod" }) &&
                InstanceStore.LoadInstance(target.DirectoryPath).ActivePackageIds.SequenceEqual(new[] { "replacement.mod" }) &&
                replacementBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(target.ConfigDirectoryPath, "ModsConfig.xml"))),
                "unrelated Redo and Save preserve replacement configuration bytes and list");
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void CheckCompletionWiring()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UiWiring.MainWindow.axaml.cs")!;
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();
        var appliedStart = source.IndexOf("if (window.ConfigApplied && currentInstance is not null)", StringComparison.Ordinal);
        var appliedEnd = source.IndexOf("if (result is null)", appliedStart, StringComparison.Ordinal);
        var applied = source[appliedStart..appliedEnd];
        var undoInvalidation = applied.IndexOf("InvalidateInstance(_undoHistory, currentInstance.FolderName, entry => entry.Instances)", StringComparison.Ordinal);
        var redoInvalidation = applied.IndexOf("InvalidateInstance(_redoHistory, currentInstance.FolderName, entry => entry.Instances)", StringComparison.Ordinal);
        var reload = applied.IndexOf("await LoadModsAsync", StringComparison.Ordinal);
        Require(undoInvalidation >= 0 && redoInvalidation >= 0 && reload > undoInvalidation && reload > redoInvalidation &&
            applied.Contains("UpdateUndoButton()") && !applied.Contains("ClearUndoHistory()"),
            "committed configuration invalidates only target history in both stacks before fallible catalog reload");
        var start = source.IndexOf("var instance = await Task.Run(() => ModpackInstanceInstaller.Create", StringComparison.Ordinal);
        var end = source.IndexOf("var completion = new CompletionWindow(", start, StringComparison.Ordinal);
        var completion = source[start..end];
        Require(completion.Contains("ReloadInstances(created.FolderName, column)") &&
            completion.Contains("UpdateUndoButton()") && !completion.Contains("ClearUndoHistory()") &&
            !completion.Contains("_undoHistory.Clear()") && !completion.Contains("_redoHistory.Clear()"),
            "successful installer completion refreshes history availability without globally clearing either stack");
    }

    private static ModpackInstallResult Fixture(string root)
    {
        var mods = Path.Combine(root, "runtime", "install-test", "Mods");
        Directory.CreateDirectory(mods);
        File.WriteAllText(Path.Combine(mods, "download.txt"), "downloaded fixture");
        return new ModpackInstallResult("Fixture pack", new[] { "installed.mod" }, 0, 0, 1, 0,
            mods, null, "fixture", null, Array.Empty<string>(), false);
    }

    private static void CheckPendingIdentity(string root)
    {
        InstanceStore.DiscardPendingMods();
        try
        {
            var previous = InstanceStore.CreateNew(root, null, "1.6", new[] { "original.mod" });
            InstanceStore.StageActiveMods(previous, new[] { "unsaved.mod" });
            Directory.Delete(previous.DirectoryPath, true);
            var installed = ModpackInstanceInstaller.Create(root, "1.6", Fixture(root));
            Require(installed.FolderName == "profile2", "installer reserves missing profile identities with pending edits");
            Require(InstanceStore.PendingConfigDirectories.Contains(previous.ConfigDirectoryPath) &&
                InstanceStore.LoadInstance(installed.DirectoryPath).ActivePackageIds.SequenceEqual(new[] { "installed.mod" }),
                "install keeps unrelated pending edits and cannot apply them to the new profile");
            ExpectFailure(() => InstanceStore.SavePendingMods());
            Require(!Directory.Exists(previous.DirectoryPath), "save cannot resurrect missing profile through installer identity reuse");
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void CheckCollisionAndRetry(string root)
    {
        var result = Fixture(root);
        string? competing = null;
        ExpectFailure(() => ModpackInstanceInstaller.Create(root, "1.6", result, new ModpackInstanceOperations
        {
            PublishDirectory = (source, destination) =>
            {
                competing = destination;
                Directory.CreateDirectory(destination);
                ExclusiveDirectoryPublication.Move(source, destination);
            }
        }));
        Require(competing is not null && Directory.Exists(competing) && !Directory.EnumerateFileSystemEntries(competing).Any(),
            "installer refuses even an empty competing publication target");
        Require(File.ReadAllText(Path.Combine(result.ModsSourcePath!, "download.txt")) == "downloaded fixture" &&
            !Directory.EnumerateDirectories(Path.Combine(root, "instance"), ".install-*").Any(),
            "failed publication restores downloaded mods and removes temporary profile");
        var installed = ModpackInstanceInstaller.Create(root, "1.6", result);
        Require(installed.FolderName == "profile2" &&
            File.ReadAllText(Path.Combine(installed.DirectoryPath, "Mods", "download.txt")) == "downloaded fixture" &&
            !Directory.Exists(Path.GetDirectoryName(result.ModsSourcePath)),
            "retry publishes a distinct complete profile and cleans original staging");
    }

    private static void CheckRecoveryFailure(string root, bool recreateSource)
    {
        var result = Fixture(root);
        var failure = ExpectFailure(() => ModpackInstanceInstaller.Create(root, "1.6", result,
            new ModpackInstanceOperations
            {
                PublishDirectory = (_, _) =>
                {
                    if (recreateSource)
                    {
                        Directory.CreateDirectory(result.ModsSourcePath!);
                        File.WriteAllText(Path.Combine(result.ModsSourcePath!, "new.txt"), "new owner");
                    }
                    throw new IOException("synthetic publish failure");
                },
                RestoreMods = (_, _) => throw new IOException("synthetic restore failure")
            }));
        var retained = Directory.EnumerateDirectories(Path.Combine(root, "instance"), ".install-*").Single();
        var retainedMods = Directory.EnumerateDirectories(retained, "Mods", SearchOption.AllDirectories).Single();
        Require(File.ReadAllText(Path.Combine(retainedMods, "download.txt")) == "downloaded fixture" &&
            failure.Message.Contains(retainedMods) && failure.InnerException is AggregateException &&
            InstanceStore.Discover(root).Count == 0,
            "failed installer recovery retains sole mod copy outside discovery and reports exact recovery path");
        if (recreateSource)
            Require(File.ReadAllText(Path.Combine(result.ModsSourcePath!, "new.txt")) == "new owner",
                "installer recovery never deletes recreated source owned by another operation");
    }

    private static void CheckReservedNames(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "instance"));
        var reserved = Path.Combine(root, "instance", "PROFILE7");
        File.WriteAllText(reserved, "reserved");
        var installed = ModpackInstanceInstaller.Create(root, "1.6", Fixture(root));
        Require(installed.FolderName == "profile8" && File.ReadAllText(reserved) == "reserved",
            "installer reserves case-insensitive profile names occupied by files");
    }

    private static IOException ExpectFailure(Action action)
    {
        try { action(); }
        catch (IOException exception) { return exception; }
        throw new Exception("Expected synthetic installer failure");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

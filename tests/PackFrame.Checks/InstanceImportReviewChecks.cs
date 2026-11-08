using PackFrame;

internal static class InstanceImportReviewChecks
{
    internal static void Run(string root)
    {
        var folder = Path.Combine(root, "import-review");
        var target = new ManagedInstance { FolderName = "profile1", DirectoryPath = Path.Combine(folder, "profile1"),
            Metadata = new InstanceMetadata { Name = "Repeated name" } };
        var other = new ManagedInstance { FolderName = "profile2", DirectoryPath = Path.Combine(folder, "profile2"),
            Metadata = new InstanceMetadata { Name = "Repeated name" } };
        var official = new ManagedInstance { FolderName = "__official__", DirectoryPath = Path.Combine(folder, "official-config"),
            IsOfficial = true, DisplayNameOverride = "Default" };
        var list = InstanceImportReview.Create(Path.Combine(folder, "source.xml"), target, true);
        var archive = InstanceImportReview.Create(Path.Combine(folder, "source.ZIP"), target, false);
        var officialArchive = InstanceImportReview.Create(Path.Combine(folder, "source.zip"), official, true);
        Check(list.DestinationPath == Path.GetFullPath(Path.Combine(target.ConfigDirectoryPath, "ModsConfig.xml")) && !list.IsArchive,
            "list review identifies the exact configuration file");
        Check(archive.IsArchive && archive.DestinationPath == Path.GetFullPath(target.DirectoryPath) && !archive.IsOfficial,
            "managed archive review identifies the whole replaced instance folder");
        Check(officialArchive.IsOfficial && officialArchive.DestinationPath == Path.GetFullPath(official.ConfigDirectoryPath),
            "official archive review identifies the actual official configuration folder");
        Check(InstanceImportReview.Create(list.SourcePath, other, false).DestinationPath != list.DestinationPath,
            "duplicate display names remain distinguishable by import destination");
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var confirmation = list.Confirmation(language);
            Check(confirmation.Contains(list.SourcePath) && confirmation.Contains(list.DestinationPath) && confirmation.Contains(target.DisplayName),
                $"overwrite confirmation includes exact source, target name and destination ({language})");
            Check(new[] { list.Scope(language), archive.Scope(language), officialArchive.Scope(language) }.Distinct().Count() == 3,
                $"list, managed archive and official archive have distinct scope explanations ({language})");
            Check(!string.IsNullOrWhiteSpace(list.PendingWarning(language)) && archive.PendingWarning(language) == string.Empty &&
                confirmation.Contains(list.PendingWarning(language)), $"pending edit loss is explicit only when relevant ({language})");
            Check(!string.IsNullOrWhiteSpace(InstanceImportReview.ReviewLabel(language)) && !string.IsNullOrWhiteSpace(InstanceImportReview.Hint(language)),
                $"import review action and guidance are localized ({language})");
        }
        Check(archive.Scope("en").Contains("entire instance") && archive.Scope("en").Contains("removed") &&
            officialArchive.Scope("en").Contains("Mods and Saves are not imported"),
            "archive review does not promise list-only replacement or importing official saves");
        // The dialog keeps this snapshot even when the selected object is subsequently changed.
        target.Metadata!.Name = "Changed";
        Check(list.TargetName == "Repeated name", "review target name is captured rather than read from mutable selection");
        CheckHistory(Path.Combine(folder, "history"));
    }

    private static void CheckHistory(string root)
    {
        var first = InstanceStore.CreateNew(root, null, "1.6", new[] { "first.saved" });
        var second = InstanceStore.CreateNew(root, null, "1.6", new[] { "second.saved" });
        var before = new[] { new InstanceSnapshot(first.FolderName, first.ActivePackageIds.ToList()),
            new InstanceSnapshot(second.FolderName, second.ActivePackageIds.ToList()) };
        InstanceStore.StageActiveMods(first, new[] { "first.edited" });
        InstanceStore.StageActiveMods(second, new[] { "second.edited" });
        var source = Path.Combine(root, "new.xml");
        File.WriteAllText(source, "<ModsConfigData><activeMods><li>imported.mod</li></activeMods></ModsConfigData>");
        var imported = InstanceStore.ImportFileNew(root, source, "1.6")!;
        var bytes = File.ReadAllBytes(Path.Combine(imported.ConfigDirectoryPath, "ModsConfig.xml"));
        var reloaded = InstanceStore.Discover(root);
        var redo = ModListOperations.Restore(before, reloaded);
        Check(reloaded.Single(x => x.FolderName == first.FolderName).ActivePackageIds.SequenceEqual(new[] { "first.saved" }) &&
            reloaded.Single(x => x.FolderName == second.FolderName).ActivePackageIds.SequenceEqual(new[] { "second.saved" }),
            "undo of existing instances remains valid after importing a separate instance");
        ModListOperations.Restore(redo, reloaded);
        Check(reloaded.Single(x => x.FolderName == first.FolderName).ActivePackageIds.SequenceEqual(new[] { "first.edited" }) &&
            reloaded.Single(x => x.FolderName == second.FolderName).ActivePackageIds.SequenceEqual(new[] { "second.edited" }) &&
            File.ReadAllBytes(Path.Combine(imported.ConfigDirectoryPath, "ModsConfig.xml")).SequenceEqual(bytes),
            "redo restores only previous targets and leaves the newly imported instance unchanged");
        InstanceStore.DiscardPendingMods();
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }
}

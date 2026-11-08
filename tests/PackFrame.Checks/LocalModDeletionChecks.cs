using PackFrame;

internal static class LocalModDeletionChecks
{
    internal static async Task Run(string root)
    {
        var area = Path.Combine(root, "local-deletion");
        Directory.CreateDirectory(area);
        var selected = Mod(Path.Combine(area, "selected"), "shared.mod");
        var duplicate = Mod(Path.Combine(area, "duplicate"), "shared.mod");
        var failed = Mod(Path.Combine(area, "not-a-folder"), "failed.mod", create: false);
        File.WriteAllText(failed.Path, "This is not a mod directory and must be kept.");
        var selectedBefore = File.ReadAllText(Path.Combine(selected.Path, "payload.txt"));

        var cancelled = await MainWindow.ConfirmAndDeleteLocalModsAsync(new[] { selected },
            _ => Task.FromResult(false), _ => throw new Exception("Cancelled deletion must not start work"));
        Require(cancelled is null && File.ReadAllText(Path.Combine(selected.Path, "payload.txt")) == selectedBefore,
            "cancelled deletion does not start disk work or change selected files");
        var empty = await MainWindow.ConfirmAndDeleteLocalModsAsync(Array.Empty<ModInfo>(),
            _ => throw new Exception("Empty targets must not prompt"),
            _ => throw new Exception("Empty targets must not delete"));
        Require(empty is null, "empty deletion does not prompt or start work");

        var instance = InstanceStore.CreateNew(Path.Combine(area, "instances"), null, "1.6",
            new[] { "saved.mod" });
        InstanceStore.StageActiveMods(instance, new[] { "unsaved.mod", "shared.mod", "failed.mod" });
        var savedConfig = File.ReadAllText(Path.Combine(instance.ConfigDirectoryPath, "ModsConfig.xml"));
        var pendingBefore = instance.ActivePackageIds.ToArray();
        try
        {
            var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var targets = new List<ModInfo> { selected, failed };
            IReadOnlyList<ModInfo>? shown = null;
            var deleting = MainWindow.ConfirmAndDeleteLocalModsAsync(targets, captured =>
            {
                shown = captured;
                Require(captured is IList<ModInfo> list && list.IsReadOnly,
                    "confirmed deletion snapshot cannot be mutated through its exposed collection");
                return confirmation.Task;
            }, captured => Task.Run(() => MainWindow.DeleteLocalModFolders(captured)));
            Require(!deleting.IsCompleted && Directory.Exists(selected.Path),
                "deletion waits for confirmation before touching files");
            targets.Clear();
            targets.Add(duplicate);
            confirmation.SetResult(true);
            var result = (await deleting)!;
            Require(shown!.Select(mod => mod.Path).SequenceEqual(new[] { selected.Path, failed.Path }) &&
                result.Deleted.Single().Path == selected.Path && result.Failed.Single().Mod.Path == failed.Path,
                "confirmed target snapshot survives caller selection changes and reports mixed outcomes");
            Require(!Directory.Exists(selected.Path) && Directory.Exists(duplicate.Path) && File.Exists(failed.Path),
                "mixed deletion touches only successfully deleted captured paths");

            var catalog = new[] { selected, duplicate, failed };
            var remaining = MainWindow.WithoutDeletedModPaths(catalog, result.Deleted);
            Require(remaining.SequenceEqual(new[] { duplicate, failed }) &&
                ReferenceEquals(remaining[0], duplicate) && ReferenceEquals(remaining[1], failed),
                "catalog reconciliation preserves duplicate-package locations and failed row identities");
            Require(instance.ActivePackageIds.SequenceEqual(pendingBefore) &&
                InstanceStore.LoadInstance(instance.DirectoryPath).ActivePackageIds.SequenceEqual(pendingBefore) &&
                File.ReadAllText(Path.Combine(instance.ConfigDirectoryPath, "ModsConfig.xml")) == savedConfig,
                "mixed deletion preserves pending orders and saved configuration without deactivating IDs");

            var malformed = failed with { Path = "invalid\0path" };
            var emptyPath = failed with { Path = "" };
            var aliasedSelected = selected with { Path = selected.Path + Path.DirectorySeparatorChar };
            Require(MainWindow.WithoutDeletedModPaths(new[] { aliasedSelected, duplicate, malformed, emptyPath },
                    result.Deleted).SequenceEqual(new[] { duplicate, malformed, emptyPath }),
                "path reconciliation handles trailing separators and preserves malformed surviving rows");
            var invalidResult = MainWindow.DeleteLocalModFolders(new[] { malformed });
            Require(invalidResult.Deleted.Count == 0 && invalidResult.Failed.Count == 1,
                "malformed deletion paths are reported as failures rather than escaping the batch");

            var partial = Mod(Path.Combine(area, "partial"), "partial.mod");
            File.WriteAllText(Path.Combine(partial.Path, "remaining.txt"), "retained");
            var partialResult = MainWindow.DeleteLocalModFolders(new[] { partial }, path =>
            {
                File.Delete(Path.Combine(path, "payload.txt"));
                throw new IOException("Simulated failure after removing one file");
            });
            Require(partialResult.Deleted.Count == 0 && partialResult.Failed.Count == 1 &&
                !File.Exists(Path.Combine(partial.Path, "payload.txt")) &&
                File.Exists(Path.Combine(partial.Path, "remaining.txt")) &&
                MainWindow.WithoutDeletedModPaths(new[] { partial }, partialResult.Deleted).Single() == partial &&
                instance.ActivePackageIds.SequenceEqual(pendingBefore),
                "partial folder failure retains its catalog row and pending order for recovery");

            var allFailed = MainWindow.DeleteLocalModFolders(new[] { failed });
            Require(allFailed.Deleted.Count == 0 && allFailed.Failed.Count == 1 &&
                MainWindow.WithoutDeletedModPaths(remaining, allFailed.Deleted).SequenceEqual(remaining),
                "all-failed deletion keeps every catalog row available for retry");
            var retry = MainWindow.DeleteLocalModFolders(new[] { duplicate });
            Require(retry.Deleted.Single() == duplicate && retry.Failed.Count == 0 &&
                instance.ActivePackageIds.SequenceEqual(pendingBefore),
                "successful deletion can follow a failure without changing pending load orders");

            Require(MainWindow.WithoutDeletedModPaths(new[] { duplicate }, retry.Deleted).Count == 0 &&
                instance.ActivePackageIds.SequenceEqual(pendingBefore),
                "deleting the final catalog entry preserves the instance pending order");

            var workshop = Mod(Path.Combine(area, "workshop"), "workshop.mod") with { Source = ModSource.Workshop };
            var rejected = MainWindow.DeleteLocalModFolders(new[] { workshop });
            Require(rejected.Deleted.Count == 0 && rejected.Failed.Count == 1 && Directory.Exists(workshop.Path),
                "local file deletion rejects non-local mod targets without removing files");

            if (!OperatingSystem.IsWindows())
            {
                var external = Mod(Path.Combine(area, "external"), "external.mod");
                var link = external with { Path = Path.Combine(area, "mod-link") };
                Directory.CreateSymbolicLink(link.Path, external.Path);
                var linkResult = MainWindow.DeleteLocalModFolders(new[] { link });
                Require(linkResult.Deleted.Count == 1 && linkResult.Failed.Count == 0 &&
                    !Directory.Exists(link.Path) && File.Exists(Path.Combine(external.Path, "payload.txt")),
                    "deleting a directory symlink removes only the selected link and preserves target files");
                var container = Mod(Path.Combine(area, "linked-child"), "container.mod");
                Directory.CreateSymbolicLink(Path.Combine(container.Path, "outside"), external.Path);
                var nestedResult = MainWindow.DeleteLocalModFolders(new[] { container });
                Require(nestedResult.Deleted.Count == 1 && nestedResult.Failed.Count == 0 &&
                    File.Exists(Path.Combine(external.Path, "payload.txt")),
                    "recursive deletion does not follow nested directory symlinks outside the selected folder");
            }
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static ModInfo Mod(string path, string packageId, bool create = true)
    {
        if (create)
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "payload.txt"), packageId);
        }
        return new ModInfo(packageId, "", path, packageId, "", null, ModSource.Local, "1.6", DateTime.UtcNow);
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS " + description);
    }
}

using PackFrame;

internal static class LocalModCopyChecks
{
    internal static async Task Run(string root)
    {
        var area = Path.Combine(root, "local-copy");
        var source = InstanceStore.CreateNew(area, null, "1.6", new[] { "copy.mod" });
        var target = InstanceStore.CreateNew(area, null, "1.6", new[] { "saved.mod" });
        var folder = Path.Combine(source.DirectoryPath, "Mods", "Example");
        Directory.CreateDirectory(Path.Combine(folder, "About"));
        File.WriteAllText(Path.Combine(folder, "About", "About.xml"),
            "<ModMetaData><name>Example</name><packageId>copy.mod</packageId></ModMetaData>");
        var mod = new ModInfo("Example", "", folder, "copy.mod", "", null, ModSource.Local, "1.6", DateTime.UtcNow);
        InstanceStore.StageActiveMods(target, new[] { "pending.mod" });
        try
        {
            // A disappeared source must not turn a failed drop into activated missing IDs.
            try
            {
                await MainWindow.CopyAndStageModsAsync(target, new[] { "copy.mod" }, 1,
                    () => Task.Run(() => MainWindow.CopyLocalModToTarget(
                        mod with { Path = folder + "-missing" }, source, target, null)));
                throw new Exception("Expected missing copy source failure");
            }
            catch (DirectoryNotFoundException) { }
            Require(target.ActivePackageIds.SequenceEqual(new[] { "pending.mod" }) &&
                InstanceStore.LoadInstance(target.DirectoryPath).ActivePackageIds.SequenceEqual(new[] { "pending.mod" }),
                "copy failure preserves pending IDs and does not activate the requested mod");

            try
            {
                await MainWindow.CopyAndStageModsAsync(target, new[] { "copy.mod", "missing.mod" }, 1,
                    () => Task.Run(() =>
                    {
                        MainWindow.CopyLocalModToTarget(mod, source, target, null);
                        MainWindow.CopyLocalModToTarget(mod with { Path = folder + "-missing" }, source, target, null);
                    }));
                throw new Exception("Expected later batch copy failure");
            }
            catch (DirectoryNotFoundException) { }
            Require(target.ActivePackageIds.SequenceEqual(new[] { "pending.mod" }),
                "later batch failure activates neither completed nor failed copied IDs");

            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var copying = MainWindow.CopyAndStageModsAsync(target, new[] { "copy.mod" }, 1, async () =>
            {
                await release.Task;
                MainWindow.CopyLocalModToTarget(mod, source, target, null);
            });
            Require(!copying.IsCompleted && target.ActivePackageIds.SequenceEqual(new[] { "pending.mod" }),
                "activation waits until asynchronous file copying completes");
            release.SetResult();
            var original = await copying;
            Require(original.SequenceEqual(new[] { "pending.mod" }) &&
                target.ActivePackageIds.SequenceEqual(new[] { "pending.mod", "copy.mod" }),
                "successful copy stages IDs and preserves the original list for undo");
            var destination = Path.Combine(target.DirectoryPath, "Mods");
            Require(File.Exists(Path.Combine(destination, "Example", "About", "About.xml")),
                "copy publishes complete local mod metadata");
            MainWindow.CopyLocalModToTarget(mod, source, target, null);
            Require(Directory.GetDirectories(destination).Length == 1,
                "repeated copy reuses an installed package without duplicate folders");

            if (!OperatingSystem.IsWindows())
            {
                // CopyTree rejects links after it has created staging and copied normal files.
                File.WriteAllText(Path.Combine(folder, "data.txt"), "complete");
                File.CreateSymbolicLink(Path.Combine(folder, "unsafe-link"), Path.Combine(folder, "data.txt"));
                var other = InstanceStore.CreateNew(area, null, "1.6", new[] { "keep.mod" });
                try
                {
                    await MainWindow.CopyAndStageModsAsync(other, new[] { "copy.mod" }, 1,
                        () => Task.Run(() => MainWindow.CopyLocalModToTarget(mod, source, other, null)));
                    throw new Exception("Expected linked-file rejection");
                }
                catch (IOException) { }
                Require(other.ActivePackageIds.SequenceEqual(new[] { "keep.mod" }) &&
                    !Directory.EnumerateFileSystemEntries(Path.Combine(other.DirectoryPath, "Mods")).Any(),
                    "mid-copy failure neither activates IDs nor leaves partial or staging folders");
            }
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS " + description);
    }
}

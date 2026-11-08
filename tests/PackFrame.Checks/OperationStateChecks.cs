using System.IO.Compression;
using System.Text.Json;
using PackFrame;

internal static class OperationStateChecks
{
    internal static void Run(string root)
    {
        var area = Path.Combine(root, "operation-state");
        Directory.CreateDirectory(area);
        CheckConfigSaving(area);
        CheckPendingChanges(area);
    }

    private static void CheckConfigSaving(string root)
    {
        var path = Path.Combine(root, "settings.json");
        var config = new AppConfig
        {
            Language = "ja", GamePath = "keep-game", OssEnabled = false,
            OssEndpoint = "old-endpoint", OssBucket = "old-bucket", OssPrefix = "old-prefix",
            OssAccessKeyId = "test-old-id", OssAccessKeySecret = "test-old-placeholder"
        };
        Require(config.TrySave(path), "settings save reports successful persistence");
        var before = File.ReadAllText(path);
        var blocked = Path.Combine(root, "blocked-config");
        Directory.CreateDirectory(blocked);
        var draft = new OssSettingsDraft(true, "new-endpoint", "new-bucket", "new-prefix",
            "test-new-id", "test-new-placeholder");
        Require(!draft.TrySave(config, candidate => candidate.TrySave(blocked)),
            "OSS save reports a failed destination write");
        Require(config.OssEndpoint == "old-endpoint" && config.OssBucket == "old-bucket" &&
            config.OssPrefix == "old-prefix" && config.OssAccessKeyId == "test-old-id" &&
            config.OssAccessKeySecret == "test-old-placeholder" && !config.OssEnabled &&
            File.ReadAllText(path) == before,
            "failed OSS save preserves previous live settings and saved file");
        Require(!Directory.EnumerateFiles(root, "blocked-config.tmp-*").Any(),
            "failed config replacement cleans temporary credential-bearing files");
        Require(draft.TrySave(config, candidate => candidate.TrySave(path)),
            "OSS draft can retry successfully after a save failure");
        var saved = JsonSerializer.Deserialize(File.ReadAllText(path), PackFrameJsonContext.Default.AppConfig)!;
        Require(config.OssEnabled && config.OssEndpoint == "new-endpoint" &&
            saved.OssAccessKeySecret == "test-new-placeholder" && saved.Language == "ja" &&
            saved.GamePath == "keep-game", "successful OSS save updates live settings and preserves unrelated preferences");
    }

    private static void CheckPendingChanges(string root)
    {
        var packRoot = Path.Combine(root, "instances");
        var target = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "old.mod" });
        var other = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "other.mod" });
        var source = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "imported.mod" });
        var profile = Path.Combine(source.ConfigDirectoryPath, "ModsConfig.xml");
        var invalid = Path.Combine(root, "invalid.xml");
        File.WriteAllText(invalid, "invalid profile");
        try
        {
            InstanceStore.StageActiveMods(target, new[] { "target.pending" });
            InstanceStore.StageActiveMods(other, new[] { "other.pending" });
            Require(!InstanceStore.ImportFileOverwrite(target, invalid, "1.6") && HasPending(target),
                "invalid import retains target pending changes");
            Require(InstanceStore.ImportFileOverwrite(target, profile, "1.6") &&
                !HasPending(target) && HasPending(other), "successful list overwrite clears only its target pending changes");
            Require(InstanceStore.LoadInstance(target.DirectoryPath).ActivePackageIds.SequenceEqual(new[] { "imported.mod" }),
                "reloading an imported list cannot restore stale pending edits");
            InstanceStore.SavePendingMods();
            Require(InstanceStore.LoadInstance(target.DirectoryPath).ActivePackageIds.SequenceEqual(new[] { "imported.mod" }),
                "saving other instance edits cannot overwrite the imported list");

            var zip = Path.Combine(root, "instance.zip");
            ZipFile.CreateFromDirectory(source.DirectoryPath, zip);
            InstanceStore.StageActiveMods(target, new[] { "archive.pending" });
            // The earlier Save persisted other.pending; stage a genuinely new edit.
            InstanceStore.StageActiveMods(other, new[] { "other.archive.pending" });
            Require(InstanceStore.ImportArchiveOverwrite(target, zip) && !HasPending(target) && HasPending(other),
                "successful archive overwrite clears only its target pending changes");

            var officialPath = Path.Combine(root, "official");
            Directory.CreateDirectory(officialPath);
            File.Copy(profile, Path.Combine(officialPath, "ModsConfig.xml"));
            var official = new ManagedInstance { FolderName = InstanceStore.OfficialFolderName,
                DirectoryPath = officialPath, IsOfficial = true };
            InstanceStore.StageActiveMods(official, new[] { "official.pending" });
            Require(InstanceStore.ImportOfficialOverwrite(official, profile, "1.6") &&
                !HasPending(official) && HasPending(other), "official list overwrite clears only its target pending changes");
            InstanceStore.StageActiveMods(official, new[] { "official.archive.pending" });
            Require(InstanceStore.ImportOfficialOverwrite(official, zip, "1.6") &&
                !HasPending(official) && HasPending(other), "official archive overwrite clears only its target pending changes");

            InstanceStore.StageActiveMods(target, new[] { "delete.pending" });
            try
            {
                InstanceStore.DeleteInstance(target, _ => throw new IOException("Simulated locked instance"));
                throw new Exception("Expected delete failure");
            }
            catch (IOException) { }
            Require(Directory.Exists(target.DirectoryPath) && HasPending(target) && HasPending(other),
                "failed deletion keeps instance and all pending changes");
            InstanceStore.DeleteInstance(target);
            Require(!Directory.Exists(target.DirectoryPath) && !HasPending(target) && HasPending(other),
                "successful deletion removes only the deleted instance pending changes");
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static bool HasPending(ManagedInstance instance) =>
        InstanceStore.PendingConfigDirectories.Contains(Path.GetFullPath(instance.ConfigDirectoryPath));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using PackFrame;

internal static class ArchiveImportChecks
{
    private const string Config = "<ModsConfigData><version>1.6</version><activeMods><li>incoming.mod</li></activeMods></ModsConfigData>";

    internal static void Run(string root)
    {
        var area = Path.Combine(root, "archive-import-safety");
        Directory.CreateDirectory(area);
        try
        {
            CheckArchiveErrorRecovery(area);
            CheckInvalidArchives(area);
            CheckOfficialTransaction(area);
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void CheckArchiveErrorRecovery(string area)
    {
        var zip = Path.Combine(area, "malformed.ZIP");
        File.WriteAllText(zip, "not a ZIP archive");
        ExpectArchiveFailure(() => MainWindow.IsSupportedInstanceImportFile(zip),
            "initial malformed ZIP receives archive-specific recovery");

        File.Delete(zip);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("Config/ModsConfig.xml").Open()))
            writer.Write(Config);
        Require(MainWindow.IsSupportedInstanceImportFile(zip), "valid ZIP passes import preflight");
        // Simulate the source changing while the overwrite review is open.
        File.WriteAllText(zip, "incomplete replacement");
        var target = InstanceStore.CreateNew(Path.Combine(area, "late-archive"), null, "1.6", new[] { "old.mod" });
        var official = Official(Path.Combine(area, "late-official"));
        ExpectArchiveFailure(() => MainWindow.ReadInstanceImport(zip,
            () => InstanceStore.ImportOfficialOverwrite(official, zip, "1.6")),
            "late malformed ZIP receives archive recovery for official overwrite");
        ExpectArchiveFailure(() => MainWindow.ReadInstanceImport(zip,
            () => InstanceStore.ImportArchiveOverwrite(target, zip)),
            "late malformed ZIP receives archive recovery for managed overwrite");
        ExpectArchiveFailure(() => MainWindow.ReadInstanceImport(zip,
            () => InstanceStore.ImportArchiveNew(Path.Combine(area, "late-new"), zip)),
            "late malformed ZIP receives archive recovery for new instance import");

        var xml = Path.Combine(area, "malformed.xml");
        File.WriteAllText(xml, "<broken>");
        Require(!MainWindow.IsSupportedInstanceImportFile(xml), "malformed XML retains unsupported-file recovery");
        var emptyZip = MakeArchive(area, new() { ["readme.txt"] = "Not an instance" });
        Require(!MainWindow.IsSupportedInstanceImportFile(emptyZip), "valid ZIP without an instance retains unsupported-file recovery");
        var invalidData = new InvalidDataException("Non-archive data failure");
        try
        {
            MainWindow.ReadInstanceImport<bool>(xml, () => throw invalidData);
            throw new Exception("Expected invalid data failure");
        }
        catch (InvalidDataException exception)
        {
            Require(ReferenceEquals(exception, invalidData), "non-ZIP invalid data is not reclassified as archive corruption");
        }
        var configFailure = new IOException("Could not read imported configuration", new System.Xml.XmlException("Malformed XML"));
        try
        {
            MainWindow.ReadInstanceImport<bool>(zip, () => throw configFailure);
            throw new Exception("Expected configuration failure");
        }
        catch (IOException exception)
        {
            Require(ReferenceEquals(exception, configFailure), "ZIP configuration errors retain original recovery classification");
        }
        var languages = new[] { "en", "zh-Hans", "zh-Hant", "ja" };
        var messages = languages.Select(OperationText.InvalidInstanceArchive).ToArray();
        Require(messages.Distinct().Count() == 4 && messages.All(message => message.Contains("ZIP") &&
            message.Contains("Config/ModsConfig.xml")), "archive recovery is localized in all four languages");
        Require(messages[0].Contains("invalid or incomplete") && messages[0].Contains("download") &&
            !messages[0].Contains("not been replaced") && !messages[0].Contains("unchanged"),
            "archive recovery offers a next action without promising destination state");
        Require(OperationText.InvalidInstanceArchive("unknown") == messages[0], "archive recovery has English fallback");
    }

    private static void ExpectArchiveFailure<T>(Func<T> action, string label)
    {
        try { action(); }
        catch (MainWindow.InvalidInstanceArchiveException exception)
        {
            Require(exception.InnerException is InvalidDataException, label + " and preserves diagnostic cause");
            return;
        }
        throw new Exception("FAIL: " + label);
    }

    private static void CheckInvalidArchives(string area)
    {
        var packRoot = Path.Combine(area, "managed");
        var target = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "old.mod" });
        File.WriteAllText(Path.Combine(target.DirectoryPath, "Saves", "keep.rws"), "saved game");
        var other = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "other.mod" });
        var official = Official(Path.Combine(area, "official-invalid"));
        var cases = new (string Name, Dictionary<string, string> Files)[]
        {
            ("malformed ModsConfig", new() { ["Config/ModsConfig.xml"] = "<broken>" }),
            ("invalid ModsConfig structure", new() { ["Config/ModsConfig.xml"] = "<wrongRoot />" }),
            ("missing ModsConfig", new() { ["Config/Preferences.xml"] = "<preferences />" }),
            ("malformed secondary XML", new() { ["Config/ModsConfig.xml"] = Config,
                ["Config/Preferences.xml"] = "<broken>" }),
            ("malformed nested XML", new() { ["Config/ModsConfig.xml"] = Config,
                ["Config/Nested/Settings.XML"] = "<broken>" }),
            ("malformed instance metadata", new() { ["Config/ModsConfig.xml"] = Config,
                [InstanceStore.MetadataFileName] = "{broken" })
        };
        foreach (var (name, files) in cases)
        {
            var zip = MakeArchive(area, files);
            InstanceStore.StageActiveMods(target, new[] { "managed.pending" });
            InstanceStore.StageActiveMods(official, new[] { "official.pending" });
            InstanceStore.StageActiveMods(other, new[] { "other.pending" });
            var beforeManaged = Snapshot(target.DirectoryPath);
            var beforeOfficial = Snapshot(official.DirectoryPath);
            var beforeOther = Snapshot(other.DirectoryPath);
            var beforeProfiles = Directory.GetDirectories(Path.Combine(packRoot, "instance")).Order().ToArray();
            ExpectRejection(() => InstanceStore.ImportArchiveOverwrite(target, zip));
            Require(beforeManaged.SequenceEqual(Snapshot(target.DirectoryPath)) && HasPending(target),
                name + " cannot replace a managed instance or clear its pending edits");
            ExpectRejection(() => InstanceStore.ImportOfficialOverwrite(official, zip, "1.6"));
            Require(beforeOfficial.SequenceEqual(Snapshot(official.DirectoryPath)) && HasPending(official),
                name + " cannot change official configuration or clear its pending edits");
            ExpectRejection(() => InstanceStore.ImportArchiveNew(packRoot, zip) is not null);
            Require(beforeProfiles.SequenceEqual(Directory.GetDirectories(Path.Combine(packRoot, "instance")).Order()) &&
                beforeOther.SequenceEqual(Snapshot(other.DirectoryPath)) && HasPending(other),
                name + " publishes no new profile and preserves unrelated instance files and edits");
        }
    }

    private static void CheckOfficialTransaction(string area)
    {
        var official = Official(Path.Combine(area, "official-transaction"));
        // Preserve the public API's support for a selected directory with a trailing separator.
        official = new ManagedInstance { FolderName = official.FolderName, IsOfficial = true,
            DirectoryPath = official.DirectoryPath + Path.DirectorySeparatorChar };
        var config = official.ConfigDirectoryPath;
        File.WriteAllText(Path.Combine(config, "AFirst.xml"), "<value>old</value>");
        File.WriteAllText(Path.Combine(config, "Unrelated.xml"), "<value>keep</value>");
        Directory.CreateDirectory(Path.Combine(config, "Nested"));
        File.WriteAllText(Path.Combine(config, "Nested", "Keep.txt"), "nested unrelated");
        var siblingSave = Path.Combine(area, "official-transaction", "Saves", "keep.rws");
        Directory.CreateDirectory(Path.GetDirectoryName(siblingSave)!);
        File.WriteAllText(siblingSave, "untouched save");
        var incoming = new Dictionary<string, string>
        {
            ["Config/AFirst.xml"] = "<value>new</value>",
            ["Config/ANew/Created.xml"] = "<new />",
            ["Config/ModsConfig.xml"] = Config,
            ["Config/Nested/Imported.xml"] = "<nested />",
            ["Config/ZFailure/Blocked.xml"] = "<incoming />",
            ["Saves/do-not-import.rws"] = "not official config"
        };
        var zip = MakeArchive(area, incoming);
        Directory.CreateDirectory(Path.Combine(config, "ZFailure", "Blocked.xml"));
        InstanceStore.StageActiveMods(official, new[] { "official.pending" });
        var before = Snapshot(config);
        ExpectRejection(() => InstanceStore.ImportOfficialOverwrite(official, zip, "1.6"));
        Require(before.SequenceEqual(Snapshot(config)) && HasPending(official),
            "failed official archive commit restores overwritten files, removes new files/directories and retains pending edits");
        Require(File.ReadAllText(siblingSave) == "untouched save", "failed official import preserves saves outside Config");
        Directory.Delete(Path.Combine(config, "ZFailure", "Blocked.xml"));
        Require(InstanceStore.ImportOfficialOverwrite(official, zip, "1.6") && !HasPending(official),
            "valid official archive can retry after a failed commit");
        Require(File.ReadAllText(Path.Combine(config, "AFirst.xml")) == "<value>new</value>" &&
            File.ReadAllText(Path.Combine(config, "ModsConfig.xml")) == Config &&
            File.Exists(Path.Combine(config, "ANew", "Created.xml")) &&
            File.Exists(Path.Combine(config, "Nested", "Imported.xml")),
            "official archive commits both root and nested configuration files");
        Require(File.ReadAllText(Path.Combine(config, "Unrelated.xml")) == "<value>keep</value>" &&
            File.ReadAllText(Path.Combine(config, "Nested", "Keep.txt")) == "nested unrelated" &&
            File.ReadAllText(siblingSave) == "untouched save" && !Directory.Exists(Path.Combine(config, "Saves")) &&
            !Directory.EnumerateDirectories(config, ".packframe-backup-*").Any(),
            "successful official import preserves unrelated configuration and saves and removes its transaction backup");
    }

    private static ManagedInstance Official(string root)
    {
        var config = Path.Combine(root, "Config");
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, "ModsConfig.xml"),
            "<ModsConfigData><activeMods><li>old.official</li></activeMods></ModsConfigData>");
        return new ManagedInstance { FolderName = InstanceStore.OfficialFolderName, IsOfficial = true, DirectoryPath = config };
    }

    private static string MakeArchive(string area, Dictionary<string, string> files)
    {
        var path = Path.Combine(area, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in files)
        {
            using var writer = new StreamWriter(archive.CreateEntry("profile99/" + name).Open());
            writer.Write(content);
        }
        return path;
    }

    private static string[] Snapshot(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => "file:" + Path.GetRelativePath(directory, path) + ":" +
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))
            .Concat(Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                .Select(path => "dir:" + Path.GetRelativePath(directory, path)))
            .Order(StringComparer.Ordinal).ToArray();

    private static bool HasPending(ManagedInstance instance) =>
        InstanceStore.PendingConfigDirectories.Contains(Path.GetFullPath(instance.ConfigDirectoryPath));

    private static void ExpectRejection(Func<bool> action)
    {
        try { if (!action()) return; }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        throw new Exception("Expected rejected archive import");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

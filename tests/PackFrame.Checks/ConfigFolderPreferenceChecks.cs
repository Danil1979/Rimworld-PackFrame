using System.Text;
using System.Text.Json;
using PackFrame;

internal static class ConfigFolderPreferenceChecks
{
    internal static void Run(string root)
    {
        root = Path.Combine(root, "config-folder-preferences");
        Directory.CreateDirectory(root);
        CheckPersistence(Path.Combine(root, "persistence"));
        CheckInvalidPaths(Path.Combine(root, "invalid"));
        CheckUnchangedSelection(Path.Combine(root, "unchanged"));
    }

    private static void CheckPersistence(string root)
    {
        var originalFolder = Path.Combine(root, "original");
        var selectedFolder = Path.Combine(root, "chosen 中文 folder");
        Directory.CreateDirectory(originalFolder);
        Directory.CreateDirectory(selectedFolder);
        var originalContents = Encoding.UTF8.GetBytes("<ModsConfigData><custom>original</custom></ModsConfigData>");
        var selectedContents = Encoding.UTF8.GetBytes("<ModsConfigData><custom>chosen</custom></ModsConfigData>");
        var originalMods = Path.Combine(originalFolder, "ModsConfig.xml");
        var selectedMods = Path.Combine(selectedFolder, "ModsConfig.xml");
        File.WriteAllBytes(originalMods, originalContents);
        File.WriteAllBytes(selectedMods, selectedContents);
        var config = Fixture(originalFolder);
        var profileOrder = config.ProfileOrder;
        var before = Snapshot(config);
        var savedPath = Path.Combine(root, "packframe.json");
        Check(config.TrySave(savedPath), "configuration-folder fixture stores original preferences");
        var savedBytes = File.ReadAllBytes(savedPath);

        // A nonempty directory reliably blocks the final atomic replacement on all
        // supported platforms, unlike permissions when the test process is root.
        var blockedPath = Path.Combine(root, "blocked.json");
        Directory.CreateDirectory(blockedPath);
        var marker = Path.Combine(blockedPath, "keep.json");
        File.WriteAllBytes(marker, savedBytes);
        Check(!config.TrySetOfficialConfigPath(selectedFolder, blockedPath) && Snapshot(config) == before &&
            ReferenceEquals(config.ProfileOrder, profileOrder),
            "failed configuration-folder persistence preserves the live path and every unrelated setting");
        Check(File.ReadAllBytes(savedPath).SequenceEqual(savedBytes) &&
            File.ReadAllBytes(marker).SequenceEqual(savedBytes) &&
            !Directory.EnumerateFileSystemEntries(root, "blocked.json.tmp-*").Any(),
            "failed configuration-folder persistence preserves destination bytes and removes its temporary file");
        var firstRun = new AppConfig { Language = "en" };
        Check(!firstRun.TrySetOfficialConfigPath(selectedFolder, blockedPath) && firstRun.OfficialConfigPath is null,
            "failed first-run configuration-folder persistence leaves the destination unset");

        Directory.Delete(blockedPath, recursive: true);
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, selectedFolder);
        var selection = Path.Combine(relative, ".") + Path.DirectorySeparatorChar;
        Check(config.TrySetOfficialConfigPath(selection, blockedPath) &&
            config.OfficialConfigPath == Path.GetFullPath(selectedFolder),
            "configuration-folder persistence retries successfully and stores the canonical absolute path");
        var persisted = JsonSerializer.Deserialize(File.ReadAllText(blockedPath), PackFrameJsonContext.Default.AppConfig)!;
        var expected = Fixture(Path.GetFullPath(selectedFolder));
        Check(Snapshot(persisted) == Snapshot(expected) && Snapshot(config) == Snapshot(expected) &&
            ReferenceEquals(config.ProfileOrder, profileOrder),
            "configuration-folder restart state matches live state and retains all unrelated settings");
        Check(File.ReadAllBytes(originalMods).SequenceEqual(originalContents) &&
            File.ReadAllBytes(selectedMods).SequenceEqual(selectedContents),
            "configuration-folder preference changes never modify either folder's game configuration");

        Check(config.TrySetOfficialConfigPath(originalFolder, blockedPath) &&
            JsonSerializer.Deserialize(File.ReadAllText(blockedPath), PackFrameJsonContext.Default.AppConfig)!
                .OfficialConfigPath == Path.GetFullPath(originalFolder),
            "configuration-folder preference can atomically replace an existing settings file");
    }

    private static void CheckInvalidPaths(string root)
    {
        var current = Path.Combine(root, "current");
        Directory.CreateDirectory(current);
        var config = Fixture(current);
        var before = Snapshot(config);
        var settingsPath = Path.Combine(root, "packframe.json");
        Check(config.TrySave(settingsPath), "invalid-path fixture stores original preferences");
        var savedBytes = File.ReadAllBytes(settingsPath);
        var filePath = Path.Combine(root, "ordinary-file");
        File.WriteAllText(filePath, "not a directory");
        var missing = Path.Combine(root, "does-not-exist");
        foreach (var invalid in new[] { null, string.Empty, "   ", "\0", missing, filePath })
        {
            Check(!config.TrySetOfficialConfigPath(invalid!, settingsPath) && Snapshot(config) == before &&
                File.ReadAllBytes(settingsPath).SequenceEqual(savedBytes),
                "invalid or missing configuration folders leave live and persisted preferences unchanged");
            Check(!config.IsCurrentOfficialConfigPath(invalid!),
                "invalid or different configuration paths cannot match the current folder");
        }
        var absentSettings = Path.Combine(root, "absent-settings", "packframe.json");
        Check(!config.TrySetOfficialConfigPath(missing, absentSettings) &&
            !Directory.Exists(missing) && !Directory.Exists(Path.GetDirectoryName(absentSettings)),
            "invalid configuration folders never create directories or settings files");

        // A disappeared current directory must not pass through the no-op shortcut.
        Directory.Delete(current);
        Check(config.IsCurrentOfficialConfigPath(Path.Combine(current, ".")) &&
            !config.TrySetOfficialConfigPath(current, settingsPath) && Snapshot(config) == before &&
            File.ReadAllBytes(settingsPath).SequenceEqual(savedBytes),
            "missing current folders retain lexical identity but cannot be selected or persisted");
        var replacement = Path.Combine(root, "replacement");
        Directory.CreateDirectory(replacement);
        config.OfficialConfigPath = "\0";
        Check(!config.IsCurrentOfficialConfigPath(replacement),
            "malformed stored configuration paths never match a valid replacement");
        Check(config.TrySetOfficialConfigPath(replacement, settingsPath) &&
            config.OfficialConfigPath == Path.GetFullPath(replacement),
            "a valid folder can replace a malformed stored preference");
    }

    private static void CheckUnchangedSelection(string root)
    {
        var directory = Path.Combine(root, "current");
        Directory.CreateDirectory(directory);
        var originalSpelling = Path.Combine(directory, ".") + Path.DirectorySeparatorChar;
        var config = Fixture(originalSpelling);
        var before = Snapshot(config);
        var settingsPath = Path.Combine(root, "packframe.json");
        var bytes = Encoding.UTF8.GetBytes(before + Environment.NewLine + "  ");
        File.WriteAllBytes(settingsPath, bytes);
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(settingsPath, timestamp);
        var originalTimestamp = File.GetLastWriteTimeUtc(settingsPath);
        Check(config.IsCurrentOfficialConfigPath(directory) &&
            config.IsCurrentOfficialConfigPath(Path.GetRelativePath(Environment.CurrentDirectory, directory)) &&
            config.TrySetOfficialConfigPath(directory, settingsPath) && Snapshot(config) == before &&
            File.ReadAllBytes(settingsPath).SequenceEqual(bytes) &&
            File.GetLastWriteTimeUtc(settingsPath) == originalTimestamp,
            "unchanged canonical configuration-folder selection does not rewrite settings or normalize live spelling");

        var absentSettings = Path.Combine(root, "must-not-create", "packframe.json");
        Check(config.TrySetOfficialConfigPath(directory, absentSettings) && Snapshot(config) == before &&
            !Directory.Exists(Path.GetDirectoryName(absentSettings)),
            "unchanged configuration-folder selection succeeds without requiring a writable settings location");
        if (OperatingSystem.IsWindows())
            Check(config.IsCurrentOfficialConfigPath(directory.ToUpperInvariant()) &&
                config.TrySetOfficialConfigPath(directory.ToUpperInvariant(), absentSettings) && Snapshot(config) == before,
                "Windows configuration-folder identity ignores path letter case");
        else
            Check(!config.IsCurrentOfficialConfigPath(directory.ToUpperInvariant()),
                "non-Windows configuration-folder identity preserves path letter case");
    }

    private static AppConfig Fixture(string directory) => new()
    {
        Language = "ja",
        GamePath = "synthetic-game",
        OfficialConfigPath = directory,
        ProfileFolder1 = "profile-one",
        ProfileFolder2 = "profile-two",
        ShowSecondaryPane = true,
        ProfileOrder = new List<string> { "profile-two", "profile-one" },
        ModpackIndexUrl = "https://example.invalid/catalog",
        BackgroundColor = "#20354A",
        OssEndpoint = "synthetic.example.invalid",
        OssBucket = "synthetic-bucket",
        OssPrefix = "synthetic-prefix/",
        OssAccessKeyId = "synthetic-id",
        OssAccessKeySecret = "synthetic-placeholder-not-a-key",
        OssEnabled = false
    };

    private static string Snapshot(AppConfig config) =>
        JsonSerializer.Serialize(config, PackFrameJsonContext.Default.AppConfig);

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

using System.Security.Cryptography;
using PackFrame;

internal static class ImportStructureChecks
{
    internal static void Run(string root)
    {
        var area = Path.Combine(root, "import-structure");
        Directory.CreateDirectory(area);
        try
        {
            CheckRejectedStructures(area);
            CheckSupportedFormats(area);
            CheckExplicitEmptyLists(area);
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void CheckRejectedStructures(string area)
    {
        var packRoot = Path.Combine(area, "rejected");
        var target = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "old.mod" });
        var other = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "other.mod" });
        File.WriteAllText(Path.Combine(target.DirectoryPath, "Saves", "keep.rws"), "keep saved game");
        var official = Official(Path.Combine(area, "official-rejected"));
        InstanceStore.StageActiveMods(target, new[] { "managed.pending", "missing.pending" });
        InstanceStore.StageActiveMods(official, new[] { "official.pending" });
        InstanceStore.StageActiveMods(other, new[] { "other.pending" });
        var beforeManaged = Snapshot(packRoot);
        var beforeOfficial = Snapshot(official.DirectoryPath);
        var cases = new (string Name, string Extension, string Text)[]
        {
            ("ModsConfigData without activeMods", ".xml", "<ModsConfigData />"),
            ("ModsConfigData with only nested activeMods", ".xml",
                "<ModsConfigData><version>1.6</version><knownExpansions /><wrapper><activeMods /></wrapper></ModsConfigData>"),
            ("ModList without modIds", ".xml", "<ModList />"),
            ("ModList with only nested modIds", ".xml", "<ModList><wrapper><modIds /></wrapper></ModList>"),
            ("savedModList without meta", ".xml", "<savedModList />"),
            ("savegame meta without modIds", ".xml", "<savegame><meta><gameVersion>1.6</gameVersion></meta></savegame>"),
            ("RWS meta without modIds", ".rws", "<savegame><meta><gameVersion>1.6</gameVersion></meta></savegame>"),
            ("JSON without activeMods", ".json", "{\"version\":\"1.6\"}"),
            ("JSON with non-array activeMods", ".json", "{\"activeMods\":null}")
        };
        foreach (var (name, extension, text) in cases)
        {
            var file = WriteSource(area, extension, text);
            Require(!InstanceStore.IsSupportedImportFile(file) && !InstanceStore.IsSupportedProfileFile(file),
                name + " is not offered as a supported import");
            Require(!InstanceStore.ImportFileOverwrite(target, file, "1.6") &&
                beforeManaged.SequenceEqual(Snapshot(packRoot)) && HasPending(target) &&
                InstanceStore.LoadInstance(target.DirectoryPath).ActivePackageIds
                    .SequenceEqual(new[] { "managed.pending", "missing.pending" }),
                name + " cannot overwrite managed files or discard its exact pending list");
            Require(!InstanceStore.ImportOfficialOverwrite(official, file, "1.6") &&
                beforeOfficial.SequenceEqual(Snapshot(official.DirectoryPath)) && HasPending(official) &&
                InstanceStore.LoadOfficial(official.DirectoryPath, "Official fixture").ActivePackageIds
                    .SequenceEqual(new[] { "official.pending" }),
                name + " cannot overwrite official files or discard its pending list");
            Require(InstanceStore.ImportFileNew(packRoot, file, "1.6") is null &&
                beforeManaged.SequenceEqual(Snapshot(packRoot)),
                name + " creates no new profile or partial files");
            Require(HasPending(other) && InstanceStore.LoadInstance(other.DirectoryPath).ActivePackageIds
                .SequenceEqual(new[] { "other.pending" }),
                name + " leaves unrelated pending edits intact");
        }
        InstanceStore.DiscardPendingMods();
    }

    private static void CheckSupportedFormats(string area)
    {
        var cases = new (string Name, string Extension, string Text, string? Version)[]
        {
            ("ModsConfigData", ".xml",
                "<ModsConfigData><version>1.6</version><activeMods><li> first.mod </li><li>second.mod</li></activeMods></ModsConfigData>", "1.6"),
            ("namespaced ModsConfigData", ".xml",
                "<ModsConfigData xmlns='urn:fixture'><activeMods><li>first.mod</li><li>second.mod</li></activeMods></ModsConfigData>", null),
            ("ModList", ".xml", "<ModList><modIds><li>first.mod</li><li>second.mod</li></modIds></ModList>", null),
            ("namespaced ModList", ".xml",
                "<ModList xmlns='urn:fixture'><modIds><li>first.mod</li><li>second.mod</li></modIds></ModList>", null),
            ("savedModList", ".xml",
                "<savedModList><meta><gameVersion>1.6</gameVersion><modIds><li>first.mod</li><li>second.mod</li></modIds></meta></savedModList>", "1.6"),
            ("savegame XML", ".xml",
                "<savegame><meta><gameVersion>1.6</gameVersion><modIds><li>first.mod</li><li>second.mod</li></modIds></meta></savegame>", "1.6"),
            ("RWS save", ".rws",
                "<savegame><meta><gameVersion>1.6</gameVersion><modIds><li>first.mod</li><li>second.mod</li></modIds></meta><game /></savegame>", "1.6"),
            ("JSON profile", ".json", "{\"version\":\"1.6\",\"activeMods\":[\" first.mod \",\"second.mod\"]}", "1.6")
        };
        foreach (var (name, extension, text, version) in cases)
        {
            var file = WriteSource(area, extension, text);
            Require(InstanceStore.IsSupportedImportFile(file) && InstanceStore.IsSupportedProfileFile(file),
                name + " remains a supported populated import");
            var imported = InstanceStore.ImportFileNew(Path.Combine(area, "supported"), file, null);
            Require(imported is { IsDamaged: false } && imported.ActivePackageIds
                .SequenceEqual(new[] { "first.mod", "second.mod" }) && imported.GameVersion == (version ?? "1.7"),
                name + " preserves trimmed IDs, order and source/default version");
        }
    }

    private static void CheckExplicitEmptyLists(string area)
    {
        var packRoot = Path.Combine(area, "empty");
        var target = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "old.mod" });
        var other = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "other.mod" });
        var official = Official(Path.Combine(area, "official-empty"));
        InstanceStore.StageActiveMods(other, new[] { "other.pending" });
        var beforeOther = Snapshot(other.DirectoryPath);
        var cases = new (string Name, string Extension, string Text)[]
        {
            ("explicit empty activeMods", ".xml", "<ModsConfigData><activeMods /></ModsConfigData>"),
            ("explicit empty modIds", ".xml", "<ModList><modIds /></ModList>"),
            ("whitespace-only activeMods", ".xml", "<ModsConfigData><activeMods>\n </activeMods></ModsConfigData>"),
            ("whitespace-only modIds", ".xml", "<ModList><modIds>\n </modIds></ModList>"),
            ("explicit empty savegame list", ".xml", "<savegame><meta><modIds /></meta></savegame>"),
            ("explicit empty RWS list", ".rws", "<savegame><meta><modIds /></meta></savegame>"),
            ("explicit empty JSON list", ".json", "{\"activeMods\":[]}")
        };
        foreach (var (name, extension, text) in cases)
        {
            var file = WriteSource(area, extension, text);
            Require(InstanceStore.IsSupportedImportFile(file) && InstanceStore.IsSupportedProfileFile(file),
                name + " is a supported deliberate empty import");
            var imported = InstanceStore.ImportFileNew(packRoot, file, "1.6");
            Require(imported is { IsDamaged: false } && imported.ActivePackageIds.Count == 0,
                name + " creates a healthy empty profile");
            InstanceStore.StageActiveMods(target, new[] { "managed.pending" });
            Require(InstanceStore.ImportFileOverwrite(target, file, "1.6") && !HasPending(target) &&
                InstanceStore.LoadInstance(target.DirectoryPath).ActivePackageIds.Count == 0,
                name + " deliberately replaces a managed list and clears its draft");
            InstanceStore.StageActiveMods(official, new[] { "official.pending" });
            Require(InstanceStore.ImportOfficialOverwrite(official, file, "1.6") && !HasPending(official) &&
                InstanceStore.LoadOfficial(official.DirectoryPath, "Official fixture").ActivePackageIds.Count == 0 &&
                HasPending(other) && beforeOther.SequenceEqual(Snapshot(other.DirectoryPath)),
                name + " deliberately replaces an official list without changing another instance");
        }
    }

    private static ManagedInstance Official(string config)
    {
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, "ModsConfig.xml"),
            "<ModsConfigData><activeMods><li>old.official</li></activeMods></ModsConfigData>");
        File.WriteAllText(Path.Combine(config, "Preferences.xml"), "<preferences>keep</preferences>");
        return InstanceStore.LoadOfficial(config, "Official fixture");
    }

    private static string WriteSource(string area, string extension, string text)
    {
        var file = Path.Combine(area, Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(file, text);
        return file;
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

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

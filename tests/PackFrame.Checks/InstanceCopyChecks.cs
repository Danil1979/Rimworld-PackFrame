using System.Text.Json.Nodes;
using System.Xml.Linq;
using PackFrame;

internal static class InstanceCopyChecks
{
    public static void Run(string root)
    {
        var packRoot = Path.Combine(root, "copy-checks");
        var source = InstanceStore.CreateNew(packRoot, null, "1.6", new[] { "core", "old.mod", "last" });
        var metadataPath = Path.Combine(source.DirectoryPath, InstanceStore.MetadataFileName);
        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))!.AsObject();
        metadata["futureSetting"] = new JsonObject { ["keep"] = true };
        File.WriteAllText(metadataPath, metadata.ToJsonString());
        CheckCopy(packRoot, source, official: false);

        var officialPath = Path.Combine(packRoot, "official-config");
        Directory.CreateDirectory(officialPath);
        File.Copy(Path.Combine(source.ConfigDirectoryPath, "ModsConfig.xml"), Path.Combine(officialPath, "ModsConfig.xml"));
        var official = new ManagedInstance
        {
            FolderName = InstanceStore.OfficialFolderName, DirectoryPath = officialPath,
            IsOfficial = true, GameVersion = "1.6", ActivePackageIds = new() { "core", "old.mod", "last" }
        };
        CheckCopy(packRoot, official, official: true);
    }

    private static void CheckCopy(string root, ManagedInstance source, bool official)
    {
        var configPath = Path.Combine(source.ConfigDirectoryPath, "ModsConfig.xml");
        var document = XDocument.Load(configPath);
        document.Root!.SetAttributeValue("futureAttribute", "keep");
        document.Root.Element("activeMods")!.SetAttributeValue("custom", "preserve");
        document.Root.Add(new XElement("futureConfig", new XAttribute("mode", "custom"), "keep"));
        document.Save(configPath);
        File.WriteAllText(Path.Combine(source.ConfigDirectoryPath, "Preferences.xml"), "unchanged settings");
        var before = File.ReadAllBytes(configPath);
        var metadataPath = Path.Combine(source.DirectoryPath, InstanceStore.MetadataFileName);
        var metadataBefore = File.Exists(metadataPath) ? File.ReadAllBytes(metadataPath) : null;
        var edited = new[] { "last", "core", "new.mod", "missing.mod" };
        InstanceStore.StageActiveMods(source, edited);
        var pendingSnapshot = InstanceStore.PendingConfigDirectories;
        Require(pendingSnapshot.Contains(Path.GetFullPath(source.ConfigDirectoryPath)), "pending snapshot identifies edited instance");
        if (pendingSnapshot is string[] mutableSnapshot) Array.Fill(mutableSnapshot, "detached");
        Require(InstanceStore.PendingConfigDirectories.Contains(Path.GetFullPath(source.ConfigDirectoryPath)),
            "pending snapshot cannot mutate staged instance keys");
        try
        {
            var copied = official ? InstanceStore.CopyOfficial(root, source) : InstanceStore.CopyInstance(root, source, "1.7");
            Require(copied.ActivePackageIds.SequenceEqual(edited), "copy includes pending enable, disable, reorder and missing IDs");
            Require(before.SequenceEqual(File.ReadAllBytes(configPath)) && source.ActivePackageIds.SequenceEqual(edited) && InstanceStore.HasPendingMods,
                "copy leaves source disk and pending edits untouched");
            var copiedConfig = XDocument.Load(Path.Combine(copied.ConfigDirectoryPath, "ModsConfig.xml"));
            Require(copiedConfig.Root!.Attribute("futureAttribute")?.Value == "keep" &&
                copiedConfig.Root.Element("futureConfig")?.Attribute("mode")?.Value == "custom" &&
                copiedConfig.Root.Element("activeMods")?.Attribute("custom")?.Value == "preserve" &&
                copiedConfig.Root.Element("knownExpansions")!.ToString() == document.Root.Element("knownExpansions")!.ToString(),
                "copy preserves unknown config fields, attributes and expansion list");
            Require(copied.GameVersion == (official ? "1.6" : "1.7"), "copy honors version override without changing official version");
            Require(File.ReadAllText(Path.Combine(copied.ConfigDirectoryPath, "Preferences.xml")) == "unchanged settings",
                "copy retains unrelated config files");
            if (!official)
            {
                var copiedMetadata = JsonNode.Parse(File.ReadAllText(Path.Combine(copied.DirectoryPath, InstanceStore.MetadataFileName)))!;
                Require(copiedMetadata["futureSetting"]?["keep"]?.GetValue<bool>() == true &&
                    copiedMetadata["name"]?.GetValue<string>() == copied.FolderName &&
                    metadataBefore!.SequenceEqual(File.ReadAllBytes(metadataPath)), "copy preserves unknown metadata and source identity");
            }
            InstanceStore.DiscardPendingMods();
            var persisted = InstanceStore.Discover(root).Single(instance => instance.FolderName == copied.FolderName);
            Require(persisted.ActivePackageIds.SequenceEqual(edited), "copied edits persist independently after source pending edits are discarded");
            InstanceStore.StageActiveMods(persisted, new[] { "copy.only" });
            InstanceStore.SavePendingMods();
            Require(before.SequenceEqual(File.ReadAllBytes(configPath)) && source.ActivePackageIds.SequenceEqual(edited),
                "editing and saving destination never alters source");
        }
        finally { InstanceStore.DiscardPendingMods(); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

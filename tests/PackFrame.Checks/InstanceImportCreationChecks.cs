using System.IO.Compression;
using PackFrame;

internal static class InstanceImportCreationChecks
{
    private enum Kind { List, Zip, Folder }

    public static void Run(string root)
    {
        foreach (var kind in Enum.GetValues<Kind>())
        {
            Success(root, kind);
            PreparedReturn(root, kind);
            foreach (var failure in new[] { "write", "metadata-write", "validation", "publish", "file", "directory" })
                Failure(root, kind, failure);
            foreach (var blocked in new[] { false, true }) Reservation(root, kind, blocked);
            if (kind != Kind.List)
                foreach (var failure in new[] { "copy", "late-xml", "late-json", "late-preferences" })
                    Failure(root, kind, failure);
        }
    }

    private static void Success(string root, Kind kind)
    {
        using var f = new Fixture(root, kind, "success");
        var created = f.Create(new InstanceCreationOperations
        {
            BeforeValidation = path =>
            {
                Require(Path.GetFileName(path).StartsWith(".create-") &&
                    InstanceStore.Discover(f.Root).Count == 0, kind + " is hidden until publication");
            }
        });
        Require(created.FolderName == "profile1" && !created.IsDamaged &&
            created.ActivePackageIds.SequenceEqual(new[] { "core", "import.mod" }) &&
            created.GameVersion == (kind == Kind.List ? "1.7" : "1.6") &&
            created.Metadata?.Name == (kind == Kind.List ? "profile1" : "Imported name") &&
            created.Metadata.Source == "incoming", kind + " preserves import name, source, list and version");
        Require(Directory.Exists(Path.Combine(created.DirectoryPath, "Saves")) &&
            InstanceStore.LoadInstance(created.DirectoryPath).ActivePackageIds.SequenceEqual(created.ActivePackageIds),
            kind + " returns the persisted published instance");
        if (kind != Kind.List)
            Require(created.Metadata?.PackId == "bound-pack" && created.IsSteamCmd &&
                File.ReadAllText(Path.Combine(created.DirectoryPath, "Saves", "save.rws")) == "save" &&
                File.Exists(Path.Combine(created.ConfigDirectoryPath, "Nested", "Preferences.xml")),
                kind + " preserves bindings and nested imported files");
        f.AssertUnrelated();
        f.AssertClean();
    }

    private static void PreparedReturn(string root, Kind kind)
    {
        using var f = new Fixture(root, kind, "prepared-return");
        var created = f.Create(new InstanceCreationOperations
        {
            PublishDirectory = (staging, target) =>
            {
                ExclusiveDirectoryPublication.Move(staging, target);
                File.WriteAllText(Path.Combine(target, "Config", "ModsConfig.xml"), "<external-change>");
                File.WriteAllText(Path.Combine(target, InstanceStore.MetadataFileName), "{");
            }
        });
        Require(!created.IsDamaged && created.ActivePackageIds.SequenceEqual(new[] { "core", "import.mod" }) &&
            created.FolderName == "profile1", kind + " returns its validated model without a failure-prone post-commit reload");
        f.AssertClean();
        f.AssertUnrelated();
    }

    private static void Failure(string root, Kind kind, string failure)
    {
        using var f = new Fixture(root, kind, failure);
        var writes = 0;
        var copies = 0;
        var publishes = 0;
        ExpectFailure(() => f.Create(new InstanceCreationOperations
        {
            WriteAtomic = (path, write) =>
            {
                writes++;
                if ((failure == "write" && writes == 1) ||
                    (failure == "metadata-write" && Path.GetFileName(path) == InstanceStore.MetadataFileName))
                    FileTransactions.WriteAtomic(path, temporary =>
                    {
                        File.WriteAllText(temporary, "partial");
                        throw new IOException("interrupted write");
                    });
                else FileTransactions.WriteAtomic(path, write);
            },
            CopyFile = (source, target, overwrite) =>
            {
                if (failure == "copy" && ++copies == 2)
                {
                    File.WriteAllText(target, "partial copy");
                    throw new IOException("interrupted copy");
                }
                // Mutate the source after initial import validation, before copying.
                if (failure == "late-xml" && Path.GetFileName(source) == "ModsConfig.xml")
                    File.WriteAllText(source, "<broken>");
                if (failure == "late-json" && Path.GetFileName(source) == InstanceStore.MetadataFileName)
                    File.WriteAllText(source, "{\"steamCmd\":\"broken\"}");
                if (failure == "late-preferences" && Path.GetFileName(source) == "Preferences.xml")
                    File.WriteAllText(source, "<broken>");
                File.Copy(source, target, overwrite);
            },
            BeforeValidation = path =>
            {
                if (failure == "validation")
                    File.WriteAllText(Path.Combine(path, "Config", "ModsConfig.xml"), "<broken>");
            },
            PublishDirectory = (source, target) =>
            {
                publishes++;
                if (failure == "publish") throw new IOException("publication failure");
                if (failure == "file") File.WriteAllText(target, "competitor");
                if (failure == "directory")
                {
                    Directory.CreateDirectory(target);
                    File.WriteAllText(Path.Combine(target, "sentinel"), "competitor");
                }
                ExclusiveDirectoryPublication.Move(source, target);
            }
        }), kind + " rejects " + failure);
        var destination = Path.Combine(f.InstancesRoot, "profile1");
        if (failure == "file") Require(File.ReadAllText(destination) == "competitor", "competing file is untouched");
        else if (failure == "directory")
            Require(Directory.GetFileSystemEntries(destination).Length == 1 &&
                File.ReadAllText(Path.Combine(destination, "sentinel")) == "competitor", "competing directory is untouched");
        else Require(!Directory.Exists(destination) && publishes == (failure == "publish" ? 1 : 0),
            kind + " failure publishes no partial profile");
        f.AssertClean();
        f.AssertUnrelated();
        f.RestoreSource();
        Require(f.Create().FolderName == (failure is "file" or "directory" ? "profile2" : "profile1"),
            kind + " retries without stealing a competitor identity");
        f.AssertClean();
        f.AssertUnrelated();
    }

    private static void Reservation(string root, Kind kind, bool blocked)
    {
        using var f = new Fixture(root, kind, "reserved-" + blocked);
        Directory.CreateDirectory(Path.Combine(f.InstancesRoot, "PROFILE2"));
        File.WriteAllText(Path.Combine(f.InstancesRoot, "profile4"), "reserved file");
        var missing = new ManagedInstance
        {
            FolderName = "profile8", DirectoryPath = Path.Combine(f.InstancesRoot, "unused", "..", "profile8")
        };
        InstanceStore.StageActiveMods(missing, new[] { "old.pending" });
        if (blocked) ExpectFailure(InstanceStore.EnsurePendingWritable, "missing draft becomes blocked");
        var pending = InstanceStore.PendingConfigDirectories.Order().ToArray();
        var blockedPaths = InstanceStore.BlockedPendingConfigDirectories.Order().ToArray();
        var created = f.Create();
        Require(created.FolderName == "profile9" &&
            !Directory.Exists(Path.Combine(f.InstancesRoot, "profile8")) &&
            File.ReadAllText(Path.Combine(f.InstancesRoot, "profile4")) == "reserved file",
            kind + " reserves missing pending/blocked paths and existing files/directories");
        Require(InstanceStore.PendingConfigDirectories.Order().SequenceEqual(pending) &&
            InstanceStore.BlockedPendingConfigDirectories.Order().SequenceEqual(blockedPaths) &&
            created.ActivePackageIds.SequenceEqual(new[] { "core", "import.mod" }),
            kind + " never applies, clears or unblocks existing drafts");
        f.AssertClean();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Kind kind;
        private readonly string source;
        private readonly string list;
        private readonly string zip;
        private readonly ManagedInstance unrelated;
        private readonly byte[] unrelatedConfig;
        private readonly string[] pending;
        internal string Root { get; }
        internal string InstancesRoot => Path.Combine(Root, InstanceStore.InstancesFolderName);

        internal Fixture(string root, Kind kind, string name)
        {
            InstanceStore.DiscardPendingMods();
            this.kind = kind;
            Root = Path.Combine(root, "import-creation", kind + "-" + name);
            Directory.CreateDirectory(InstancesRoot);
            source = Path.Combine(Root, "incoming");
            list = Path.Combine(Root, "incoming.xml");
            zip = Path.Combine(Root, "incoming.zip");
            RestoreSource();
            unrelated = InstanceStore.CreateNew(Path.Combine(Root, "unrelated"), null, "1.6", new[] { "saved.mod" });
            unrelatedConfig = File.ReadAllBytes(Path.Combine(unrelated.ConfigDirectoryPath, "ModsConfig.xml"));
            InstanceStore.StageActiveMods(unrelated, new[] { "unsaved.mod" });
            pending = InstanceStore.PendingConfigDirectories.Order().ToArray();
        }

        internal void RestoreSource()
        {
            Directory.CreateDirectory(Path.Combine(source, "Config", "Nested"));
            Directory.CreateDirectory(Path.Combine(source, "Saves"));
            const string xml = "<ModsConfigData><version>1.6</version><activeMods><li>Core</li><li>Import.Mod</li></activeMods><knownExpansions><li>custom.expansion</li></knownExpansions></ModsConfigData>";
            File.WriteAllText(list, xml);
            File.WriteAllText(Path.Combine(source, "Config", "ModsConfig.xml"), xml);
            File.WriteAllText(Path.Combine(source, "Config", "Nested", "Preferences.xml"), "<Preferences />");
            File.WriteAllText(Path.Combine(source, "Saves", "save.rws"), "save");
            File.WriteAllText(Path.Combine(source, InstanceStore.MetadataFileName),
                "{\"name\":\" Imported name \",\"packId\":\"bound-pack\",\"steamCmd\":true}");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(source, zip);
        }

        internal ManagedInstance Create(InstanceCreationOperations? operations = null)
        {
            operations ??= new InstanceCreationOperations();
            return (kind switch
            {
                Kind.List => InstanceStore.ImportFileNew(Root, list, "1.7", operations),
                Kind.Zip => InstanceStore.ImportArchiveNew(Root, zip, operations),
                _ => InstanceStore.ImportDirectory(Root, source, "incoming", operations)
            }) ?? throw new Exception("Expected supported import");
        }

        internal void AssertUnrelated() => Require(
            InstanceStore.PendingConfigDirectories.Order().SequenceEqual(pending) &&
            unrelated.ActivePackageIds.SequenceEqual(new[] { "unsaved.mod" }) &&
            unrelatedConfig.SequenceEqual(File.ReadAllBytes(Path.Combine(unrelated.ConfigDirectoryPath, "ModsConfig.xml"))),
            kind + " preserves unrelated draft and disk bytes");

        internal void AssertClean() => Require(!Directory.EnumerateDirectories(InstancesRoot, ".create-*").Any(),
            kind + " cleans owned hidden staging");
        public void Dispose() => InstanceStore.DiscardPendingMods();
    }

    private static void ExpectFailure(Action action, string message)
    {
        try { action(); }
        catch (IOException) { Require(true, message); return; }
        throw new Exception("FAIL: expected IOException: " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

using System.Text.Json;
using PackFrame;

internal static class MissingHistoryChecks
{
    public static void Run(string root)
    {
        var directory = Path.Combine(root, "missing-history");
        var instance = new ManagedInstance { DirectoryPath = directory, FolderName = "History checks" };
        var path = ModFullNameList.GetPath(instance);
        Require(ModFullNameList.LoadHistoryOrThrow(instance).Count == 0 && !Directory.Exists(directory),
            "absent history reads as empty without creating files");
        Directory.CreateDirectory(directory);
        foreach (var invalid in new[] { "{broken", "null", "[]", "{\"missingHistory\":42}", "" })
        {
            File.WriteAllText(path, invalid);
            ExpectFailure(() => ModFullNameList.LoadHistoryOrThrow(instance), "invalid history has an explicit read failure");
            Require(ModFullNameList.GetHistoryCount(instance) == -1 &&
                ModFullNameList.LoadHistory(instance).Count == 0 && ModFullNameList.Load(instance).Count == 0,
                "unavailable history remains reachable while legacy read callers remain compatible");
            ExpectFailure(() => ModFullNameList.ClearHistory(instance), "clear rejects unreadable history");
            ExpectFailure(() => ModFullNameList.Save(instance, Array.Empty<ModInfo>(), new[] { "new.mod" }),
                "save rejects unreadable history");
            Require(File.ReadAllText(path) == invalid, "failed save and clear preserve the original history bytes");
        }

        var document = new ModFullNameDocument
        {
            Mods = new() { new() { PackageId = "known.mod", Label = "Known", Author = "Author", Link = "https://example.com/mod" } },
            MissingHistory = new()
            {
                new() { PackageId = " old.mod ", Label = "Old", LastMissingAt = "2020-01-01 10:00" },
                new() { PackageId = "recent.mod", Label = "Recent", LastMissingAt = "2025-01-01 10:00" }
            }
        };
        File.WriteAllText(path, JsonSerializer.Serialize(document, PackFrameJsonContext.Default.ModFullNameDocument));
        var original = File.ReadAllText(path);
        Require(ModFullNameList.LoadHistoryOrThrow(instance).Select(entry => entry.PackageId)
                .SequenceEqual(new[] { "recent.mod", "old.mod" }),
            "history keeps descending timestamps and normalized package identifiers");
        ExpectFailure(() => FileTransactions.WriteAtomic(path, temporary =>
        {
            File.WriteAllText(temporary, "interrupted history replacement");
            throw new IOException("Synthetic interrupted write");
        }), "interrupted atomic history replacement reports failure");
        Require(File.ReadAllText(path) == original && !Directory.EnumerateFiles(directory, "*.tmp-*").Any(),
            "interrupted atomic history replacement preserves old bytes and removes temporary files");
        ExpectFailure(() => ModFullNameList.Save(instance, FailingMods(), Array.Empty<string>()),
            "failed save preparation reports failure");
        Require(File.ReadAllText(path) == original, "failed save preparation preserves saved history");
        ModFullNameList.ClearHistory(instance);
        Require(ModFullNameList.LoadHistoryOrThrow(instance).Count == 0 &&
            ModFullNameList.Load(instance)["known.mod"] is { Label: "Known", Author: "Author", Link: "https://example.com/mod" },
            "clear commits empty history while preserving current mod metadata");
        ModFullNameList.ClearHistory(instance);
        Require(ModFullNameList.GetHistoryCount(instance) == 0 && !Directory.EnumerateFiles(directory, "*.tmp-*").Any(),
            "repeated clears remain empty without leftover temporary files");
        ModFullNameList.Save(instance, Array.Empty<ModInfo>(), new[] { "known.mod" });
        Require(ModFullNameList.LoadHistoryOrThrow(instance).Single() is { PackageId: "known.mod", Label: "Known" },
            "history save recovers after a successful clear and retains known names");

        File.Delete(path);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "keep.txt"), "Keep");
        ExpectFailure(() => ModFullNameList.LoadHistoryOrThrow(instance), "directory at history path is a read failure rather than empty history");
        ExpectFailure(() => ModFullNameList.ClearHistory(instance), "clear preserves an obstructed history path");
        Require(ModFullNameList.GetHistoryCount(instance) == -1 && File.ReadAllText(Path.Combine(path, "keep.txt")) == "Keep",
            "obstructed history remains visible as unavailable without deleting contents");
        Directory.Delete(path, recursive: true);
        File.WriteAllText(path, "{\"mods\":null,\"missingHistory\":null}");
        ModFullNameList.ClearHistory(instance);
        Require(ModFullNameList.GetHistoryCount(instance) == 0,
            "legacy null collections normalize safely on clear");
        var invalidInstance = new ManagedInstance { DirectoryPath = "", FolderName = "Invalid" };
        ExpectFailure(() => ModFullNameList.ClearHistory(invalidInstance), "invalid instance path cannot claim a successful clear");
    }

    private static IEnumerable<ModInfo> FailingMods()
    {
        foreach (var mod in Array.Empty<ModInfo>()) yield return mod;
        throw new IOException("Synthetic enumeration failure");
    }

    private static void ExpectFailure(Action action, string name)
    {
        var failed = false;
        try { action(); }
        catch (Exception) { failed = true; }
        Require(failed, name);
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

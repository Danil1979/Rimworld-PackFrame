using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PackFrame;

public sealed class ModFullNameEntry
{
    public string Label { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
}

public sealed class MissingHistoryEntry
{
    public string Label { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string LastMissingAt { get; set; } = string.Empty;
}

public sealed class ModFullNameDocument
{
    public List<ModFullNameEntry> Mods { get; set; } = new();
    public List<MissingHistoryEntry> MissingHistory { get; set; } = new();
}

public sealed class MissingModRow
{
    public required string Label { get; init; }
    public required string Author { get; init; }
    public required string PackageId { get; init; }
    public required string Link { get; init; }
    public ulong? WorkshopId { get; init; }
    public WorkshopModAvailability Availability { get; init; } = WorkshopModAvailability.Missing;
    public string Status { get; init; } = string.Empty;
    public bool IsDownloading => Availability is WorkshopModAvailability.Queued or
        WorkshopModAvailability.Downloading or WorkshopModAvailability.Completing;
}

public static class ModFullNameList
{
    public const string FileName = "ModFullNameList.json";

    public static string GetPath(ManagedInstance instance)
    {
        return Path.Combine(instance.DirectoryPath, FileName);
    }

    public static IReadOnlyDictionary<string, ModFullNameEntry> Load(ManagedInstance instance)
    {
        var document = LoadDocument(instance);
        return ToModDictionary(document.Mods);
    }

    public static IReadOnlyList<MissingHistoryEntry> LoadHistory(ManagedInstance instance)
    {
        try { return LoadHistoryOrThrow(instance); }
        catch { return Array.Empty<MissingHistoryEntry>(); }
    }

    // A negative count represents unavailable history, never an empty history.
    public static int GetHistoryCount(ManagedInstance instance)
    {
        try { return LoadHistoryOrThrow(instance).Count; }
        catch { return -1; }
    }

    public static IReadOnlyList<MissingHistoryEntry> LoadHistoryOrThrow(ManagedInstance instance)
    {
        var document = LoadDocumentOrThrow(instance);
        return ToHistoryDictionary(document.MissingHistory)
            .Values
            .OrderByDescending(entry => entry.LastMissingAt, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static void Save(
        ManagedInstance instance,
        IEnumerable<ModInfo> mods,
        IEnumerable<string> missingPackageIds,
        IEnumerable<string>? pendingPackageIds = null
    )
    {
        if (string.IsNullOrWhiteSpace(instance.DirectoryPath))
        {
            return;
        }

        var document = LoadDocumentOrThrow(instance);
        var oldMods = ToModDictionary(document.Mods);
        var history = ToHistoryDictionary(document.MissingHistory);
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in missingPackageIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                missing.Add(id.Trim());
            }
        }
        var pending = new HashSet<string>((pendingPackageIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()), StringComparer.OrdinalIgnoreCase);
        missing.ExceptWith(pending);

        var currentMods = new List<ModFullNameEntry>();
        foreach (var mod in mods)
        {
            if (string.IsNullOrWhiteSpace(mod.PackageId))
            {
                continue;
            }

            var packageId = mod.PackageId.Trim();
            missing.Remove(packageId);
            oldMods.TryGetValue(packageId, out var oldMod);
            history.TryGetValue(packageId, out var oldHistory);
            currentMods.Add(new ModFullNameEntry
            {
                Label = string.IsNullOrWhiteSpace(mod.Name)
                    ? oldMod?.Label ?? oldHistory?.Label ?? packageId
                    : mod.Name.Trim(),
                Author = string.IsNullOrWhiteSpace(mod.Author)
                    ? oldMod?.Author ?? oldHistory?.Author ?? string.Empty
                    : mod.Author.Trim(),
                PackageId = packageId,
                Link = string.IsNullOrWhiteSpace(mod.Link)
                    ? oldMod?.Link ?? oldHistory?.Link ?? string.Empty
                    : mod.Link.Trim()
            });
        }

        var currentIds = new HashSet<string>(currentMods.Select(mod => mod.PackageId), StringComparer.OrdinalIgnoreCase);
        foreach (var packageId in pending)
        {
            if (currentIds.Contains(packageId)) continue;
            oldMods.TryGetValue(packageId, out var oldMod);
            history.TryGetValue(packageId, out var oldHistory);
            if (oldMod is null && oldHistory is null) continue;
            currentMods.Add(new ModFullNameEntry
            {
                Label = oldMod?.Label ?? oldHistory!.Label,
                Author = oldMod?.Author ?? oldHistory!.Author,
                PackageId = packageId,
                Link = oldMod?.Link ?? oldHistory!.Link
            });
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        foreach (var packageId in missing)
        {
            oldMods.TryGetValue(packageId, out var oldMod);
            history.TryGetValue(packageId, out var oldHistory);
            history[packageId] = new MissingHistoryEntry
            {
                Label = oldMod?.Label ?? oldHistory?.Label ?? packageId,
                Author = oldMod?.Author ?? oldHistory?.Author ?? string.Empty,
                PackageId = packageId,
                Link = oldMod?.Link ?? oldHistory?.Link ?? string.Empty,
                LastMissingAt = timestamp
            };
        }

        document.Mods = currentMods
            .OrderBy(entry => entry.Label, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        document.MissingHistory = history.Values
            .OrderByDescending(entry => entry.LastMissingAt, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        WriteDocument(instance, document);
    }

    public static void ClearHistory(ManagedInstance instance)
    {
        var document = LoadDocumentOrThrow(instance);
        document.MissingHistory.Clear();
        WriteDocument(instance, document);
    }

    private static ModFullNameDocument LoadDocument(ManagedInstance instance)
    {
        try { return LoadDocumentOrThrow(instance); }
        catch { return new ModFullNameDocument(); }
    }

    private static ModFullNameDocument LoadDocumentOrThrow(ManagedInstance instance)
    {
        if (string.IsNullOrWhiteSpace(instance.DirectoryPath))
            throw new IOException("The instance folder is unavailable.");

        // File.Exists also returns false on access errors. Read directly so those
        // failures cannot be mistaken for a new, empty history and overwritten.
        string json;
        try { json = File.ReadAllText(GetPath(instance)); }
        catch (FileNotFoundException) { return new ModFullNameDocument(); }
        catch (DirectoryNotFoundException) { return new ModFullNameDocument(); }

        var document = JsonSerializer.Deserialize(json, PackFrameJsonContext.Default.ModFullNameDocument)
            ?? throw new JsonException("The mod history document is null.");
        document.Mods ??= new();
        document.MissingHistory ??= new();
        return document;
    }

    private static void WriteDocument(ManagedInstance instance, ModFullNameDocument document)
    {
        // Serialize before touching the destination and flush the complete sibling
        // file before atomic replacement. A failed write leaves the old file intact.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, PackFrameJsonContext.Default.ModFullNameDocument);
        Directory.CreateDirectory(instance.DirectoryPath);
        FileTransactions.WriteAtomic(GetPath(instance), temporary =>
        {
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        });
    }

    private static Dictionary<string, ModFullNameEntry> ToModDictionary(
        IEnumerable<ModFullNameEntry>? entries
    )
    {
        var result = new Dictionary<string, ModFullNameEntry>(
            StringComparer.OrdinalIgnoreCase
        );
        if (entries is null)
        {
            return result;
        }

        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.PackageId))
            {
                continue;
            }

            entry.Label ??= string.Empty;
            entry.Author ??= string.Empty;
            entry.Link ??= string.Empty;
            entry.PackageId = entry.PackageId.Trim();
            result[entry.PackageId] = entry;
        }

        return result;
    }

    private static Dictionary<string, MissingHistoryEntry> ToHistoryDictionary(
        IEnumerable<MissingHistoryEntry>? entries
    )
    {
        var result = new Dictionary<string, MissingHistoryEntry>(
            StringComparer.OrdinalIgnoreCase
        );
        if (entries is null)
        {
            return result;
        }

        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.PackageId))
            {
                continue;
            }

            entry.Label ??= string.Empty;
            entry.Author ??= string.Empty;
            entry.Link ??= string.Empty;
            entry.LastMissingAt ??= string.Empty;
            entry.PackageId = entry.PackageId.Trim();
            result[entry.PackageId] = entry;
        }

        return result;
    }
}

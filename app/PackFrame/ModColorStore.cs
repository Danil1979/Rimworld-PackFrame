using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PackFrame;

public sealed record ModColorChoice(
    bool Cancelled,
    bool Clear,
    string? Hex,
    IReadOnlyDictionary<string, string> Labels
);

public sealed class ModColorData
{
    public Dictionary<string, string> ModColors { get; set; } = new();
    public Dictionary<string, string> ColorLabels { get; set; } = new();
}

public static class ModColorStore
{
    public const string FileName = "ModColors.json";
    private static string? _cachedRoot;
    private static ModColorData? _cachedData;

    public static void Invalidate() => _cachedData = null;

    public static string GetPath(string packRoot)
    {
        return Path.Combine(
            packRoot,
            InstanceStore.InstancesFolderName,
            FileName
        );
    }

    public static ModColorData Load(string? packRoot)
    {
        var data = LoadCached(packRoot);
        return new ModColorData
        {
            ModColors = new(data.ModColors, StringComparer.OrdinalIgnoreCase),
            ColorLabels = new(data.ColorLabels, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static ModColorData LoadCached(string? packRoot)
    {
        if (_cachedData is not null && _cachedRoot == packRoot) return _cachedData;
        _cachedRoot = packRoot;
        var result = new ModColorData
        {
            ModColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ColorLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
        if (string.IsNullOrWhiteSpace(packRoot))
        {
            return _cachedData = result;
        }

        try { result = ReadForWrite(packRoot); }
        catch (IOException exception) { AppLog.Error("Read mod colors: " + GetPath(packRoot), exception); }
        return _cachedData = result;
    }

    // Display reads may fall back, but mutations must read the current complete
    // document. A stale cache or unreadable file must never become an empty save.
    private static ModColorData ReadForWrite(string packRoot)
    {
        var path = GetPath(packRoot);
        var result = new ModColorData
        {
            ModColors = new(StringComparer.OrdinalIgnoreCase),
            ColorLabels = new(StringComparer.OrdinalIgnoreCase)
        };
        if (!File.Exists(path) && !Directory.Exists(path)) return result;
        try
        {
            var loaded = JsonSerializer.Deserialize(File.ReadAllText(path), PackFrameJsonContext.Default.ModColorData)
                ?? throw new InvalidDataException("Color settings are null.");
            foreach (var pair in loaded.ModColors ?? new Dictionary<string, string>())
                if (!string.IsNullOrWhiteSpace(pair.Key)) result.ModColors[pair.Key] = pair.Value ?? string.Empty;
            foreach (var pair in loaded.ColorLabels ?? new Dictionary<string, string>())
                if (!string.IsNullOrWhiteSpace(pair.Key)) result.ColorLabels[pair.Key] = pair.Value ?? string.Empty;
            return result;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            throw new IOException("Could not read color settings: " + path, exception);
        }
    }

    public static void SetModColors(
        string? packRoot,
        IReadOnlyDictionary<string, string?> colors
    )
    {
        if (string.IsNullOrWhiteSpace(packRoot))
        {
            return;
        }

        var data = ReadForWrite(packRoot);
        var changed = false;
        foreach (var pair in colors)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                changed |= data.ModColors.Remove(pair.Key);
            }
            else
            {
                data.ModColors[pair.Key] = pair.Value;
                changed = true;
            }
        }

        if (changed)
        {
            Write(packRoot, data);
        }
    }

    public static void SetLabels(
        string? packRoot,
        IReadOnlyDictionary<string, string> labels
    )
    {
        if (string.IsNullOrWhiteSpace(packRoot))
        {
            return;
        }

        var data = ReadForWrite(packRoot);
        data.ColorLabels = new Dictionary<string, string>(
            labels,
            StringComparer.OrdinalIgnoreCase
        );
        Write(packRoot, data);
    }

    internal static Dictionary<string, string?> ApplyChoice(string packRoot, ModColorChoice choice,
        IReadOnlyList<string> packageIds)
    {
        if (choice.Cancelled || (!choice.Clear && string.IsNullOrWhiteSpace(choice.Hex)))
            throw new ArgumentException("A color or clear choice is required.", nameof(choice));
        var data = ReadForWrite(packRoot);
        var before = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var packageId in packageIds)
        {
            if (string.IsNullOrWhiteSpace(packageId) || before.ContainsKey(packageId)) continue;
            before[packageId] = data.ModColors.TryGetValue(packageId, out var prior) ? prior : null;
            if (choice.Clear) data.ModColors.Remove(packageId);
            else data.ModColors[packageId] = choice.Hex!;
        }
        data.ColorLabels = new Dictionary<string, string>(choice.Labels, StringComparer.OrdinalIgnoreCase);
        // Labels and selected colors are one atomic commit, not two partial saves.
        Write(packRoot, data);
        return before;
    }

    public static string? GetColor(string? packRoot, string packageId)
    {
        return LoadCached(packRoot).ModColors.TryGetValue(packageId, out var color)
            ? color
            : null;
    }

    private static void Write(string packRoot, ModColorData data)
    {
        var path = GetPath(packRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileTransactions.WriteAtomic(path, temporary => File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(
                data,
                PackFrameJsonContext.Default.ModColorData
            )
        ));
        _cachedRoot = packRoot;
        _cachedData = data;
    }
}

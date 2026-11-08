using PackFrame;

internal static class ModColorRecoveryChecks
{
    internal static void Run(string root)
    {
        var area = Path.Combine(root, "color-recovery");
        var labels = new Dictionary<string, string> { ["#E05A5A"] = "Original" };
        ModColorStore.SetLabels(area, labels);
        ModColorStore.SetModColors(area, new Dictionary<string, string?> { ["saved.mod"] = "#E05A5A" });
        var path = ModColorStore.GetPath(area);
        var original = File.ReadAllBytes(path);
        try
        {
            File.WriteAllText(path, "{broken");
            ExpectFailure(() => ModColorStore.SetLabels(area, new Dictionary<string, string> { ["#E05A5A"] = "Changed" }),
                "color-label mutation rejects corruption even with a warm display cache");
            Require(File.ReadAllText(path) == "{broken", "failed label write preserves unreadable source bytes");
            ExpectFailure(() => ModColorStore.SetModColors(area, new Dictionary<string, string?> { ["new.mod"] = "#5AA9E0" }),
                "mod-color mutation rejects unreadable saved data");
            ModColorStore.Invalidate();
            _ = ModColorStore.Load(area);
            ExpectFailure(() => ModColorStore.SetLabels(area, labels),
                "an empty display fallback cannot authorize replacing a corrupt color file");
            File.WriteAllText(path, "null");
            ExpectFailure(() => ModColorStore.SetLabels(area, labels), "null color data is not a valid writable empty document");
            Require(File.ReadAllText(path) == "null", "null color source is preserved after failed mutation");
            File.Delete(path); Directory.CreateDirectory(path);
            ExpectFailure(() => ModColorStore.SetLabels(area, labels), "a directory at the color-file path is an unreadable target");
            Require(Directory.Exists(path), "failed color write does not replace an existing directory");
            Directory.Delete(path); File.WriteAllBytes(path, original);
            ModColorStore.SetLabels(area, new Dictionary<string, string> { ["#E05A5A"] = "Repaired" });
            Require(ModColorStore.Load(area).ColorLabels["#E05A5A"] == "Repaired" &&
                ModColorStore.GetColor(area, "saved.mod") == "#E05A5A", "repair and retry save labels while retaining unrelated mod colors");
            File.WriteAllText(path, "{\"modColors\":{\"external.mod\":\"#4FC1C1\"},\"colorLabels\":{\"#4FC1C1\":\"External\"}}");
            ModColorStore.SetModColors(area, new Dictionary<string, string?> { ["new.mod"] = "#5AA9E0" });
            var merged = ModColorStore.Load(area);
            Require(merged.ModColors["external.mod"] == "#4FC1C1" && merged.ColorLabels["#4FC1C1"] == "External" &&
                merged.ModColors["new.mod"] == "#5AA9E0", "mutations read current saved colors rather than overwriting external changes with a stale cache");
            ModColorStore.SetModColors(area, new Dictionary<string, string?> { ["new.mod"] = null });
            Require(!ModColorStore.Load(area).ModColors.ContainsKey("new.mod") && ModColorStore.GetColor(area, "external.mod") == "#4FC1C1",
                "clear color removes only selected entries");
            var choice = new ModColorChoice(false, false, "#E05A5A", new Dictionary<string, string> { ["#E05A5A"] = "Combined" });
            var before = ModColorStore.ApplyChoice(area, choice, new[] { "external.mod", "new.mod", "NEW.MOD" });
            var combined = ModColorStore.Load(area);
            Require(before.Count == 2 && before["external.mod"] == "#4FC1C1" && before["new.mod"] is null &&
                combined.ModColors["external.mod"] == "#E05A5A" && combined.ModColors["new.mod"] == "#E05A5A" &&
                combined.ColorLabels["#E05A5A"] == "Combined", "combined palette commit saves labels and selected colors with exact prior states for Undo");
            var saved = File.ReadAllBytes(path);
            File.WriteAllText(path, "{broken");
            ExpectFailure(() => ModColorStore.ApplyChoice(area, choice, new[] { "other.mod" }),
                "combined palette failure cannot partially commit labels or selected colors");
            Require(File.ReadAllText(path) == "{broken" && !ModColorStore.Load(area).ModColors.ContainsKey("other.mod"),
                "failed combined save leaves corrupt source and last-good display cache unchanged");
            File.WriteAllBytes(path, saved);
            ModColorStore.ApplyChoice(area, choice with { Clear = true, Hex = null }, new[] { "new.mod" });
            Require(!ModColorStore.Load(area).ModColors.ContainsKey("new.mod") && ModColorStore.GetColor(area, "external.mod") == "#E05A5A",
                "combined clear choice removes only selected colors and retains others");
            Require(ModColorStore.Load(null).ModColors.Count == 0 && ModColorStore.Load(null).ModColors.Count == 0,
                "repeated empty-root display reads never reuse another pack's cached colors");
            Require(ModColorStore.GetColor(area, "external.mod") == "#E05A5A", "returning to a pack after an empty-root read reloads its own colors");
        }
        finally { ModColorStore.Invalidate(); }
    }
    private static void ExpectFailure(Action action, string message)
    {
        try { action(); throw new Exception("FAIL: " + message); }
        catch (IOException) { Require(true, message); }
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

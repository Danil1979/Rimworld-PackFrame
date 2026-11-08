using PackFrame;

internal static class OfficialConfigWorkflowChecks
{
    internal static void Run(string root)
    {
        root = Path.Combine(root, "official-config-workflow");
        var old = Path.Combine(root, "old"); var next = Path.Combine(root, "next");
        Directory.CreateDirectory(old); Directory.CreateDirectory(next);
        var settings = Path.Combine(root, "packframe.json");
        var config = new AppConfig { OfficialConfigPath = old, Language = "en" };
        config.TrySave(settings); var original = File.ReadAllBytes(settings);
        Check(OfficialConfigWorkflow.TryChange(config, next, new[] { old }, settings) == OfficialConfigChange.PendingChanges &&
            config.OfficialConfigPath == old && File.ReadAllBytes(settings).SequenceEqual(original),
            "switching official folders cannot hide pending edits or change persisted destination");
        Check(OfficialConfigWorkflow.TryChange(config, Path.Combine(old, "."), new[] { old }, settings) == OfficialConfigChange.Unchanged &&
            File.ReadAllBytes(settings).SequenceEqual(original),
            "same-folder selection retains drafts and does not need a history reset or settings write");
        Directory.Delete(old);
        Check(OfficialConfigWorkflow.TryChange(config, next, new[] { old }, settings) == OfficialConfigChange.PendingChanges &&
            config.OfficialConfigPath == old && File.ReadAllBytes(settings).SequenceEqual(original),
            "missing old official folder does not detach or hide its retained pending edits");
        Directory.CreateDirectory(old);
        var blocked = Path.Combine(root, "blocked.json"); Directory.CreateDirectory(blocked);
        Check(OfficialConfigWorkflow.TryChange(config, next, Array.Empty<string>(), blocked) == OfficialConfigChange.SaveFailed &&
            config.OfficialConfigPath == old, "failed folder setting leaves the current official destination intact");
        Check(OfficialConfigWorkflow.TryChange(config, next, new[] { Path.Combine(root, "unrelated-profile", "Config") }, settings) == OfficialConfigChange.Applied &&
            config.OfficialConfigPath == next, "unrelated managed-instance drafts do not prevent a safe official-folder change");
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
            Check(OfficialConfigWorkflow.PendingWarning(language, old).Contains(old) &&
                OfficialConfigWorkflow.SaveFailure(language).Contains("PackFrame") &&
                !string.IsNullOrWhiteSpace(OfficialConfigWorkflow.UseFolder(language)) &&
                !string.IsNullOrWhiteSpace(OfficialConfigWorkflow.CurrentFolder(language)),
                $"folder recovery explains the retained destination and persistent settings location ({language})");
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

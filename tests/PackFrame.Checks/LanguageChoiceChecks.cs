using PackFrame;

internal static class LanguageChoiceChecks
{
    internal static void Run(string root)
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var choices = LanguageChoice.Options(language);
            Check(choices.Count == 4 && choices.Select(c => c.Code).Distinct().Count() == 4 &&
                choices.Select(c => c.ControlName).Distinct().Count() == 4 && choices.All(c => Localization.IsSupported(c.Code)),
                $"language choices identify each supported language and control ({language})");
            Check(choices.Count(c => c.IsCurrent) == 1 && choices.Single(c => c.IsCurrent).Code == language &&
                choices.Single(c => c.IsCurrent).AccessibleName.Contains(choices.Single(c => c.IsCurrent).CurrentLabel),
                $"current language is explicitly named for visual and accessibility labels ({language})");
            Check(choices.Where(c => !c.IsCurrent).All(c => c.AccessibleName == c.NativeName),
                $"other language options retain their native names ({language})");
            var name = new string('x', 100);
            var launch = Localization.LaunchInstance(language, name);
            Check(launch.EndsWith(name, StringComparison.Ordinal) && launch.Length > name.Length,
                $"launch action precedes a long instance name before visual truncation ({language})");
            Check(!string.IsNullOrWhiteSpace(LanguageChoice.SaveFailure(language)) && LanguageChoice.SaveFailure(language).Contains("PackFrame"),
                $"language save failure identifies where recovery is needed ({language})");
        }
        foreach (var unknown in new string?[] { null, string.Empty, "unknown" })
            Check(LanguageChoice.Options(unknown).All(c => !c.IsCurrent) && LanguageChoice.Title(unknown) == LanguageChoice.Title("en"),
                "first-run or unknown language does not mislabel a current choice");
        CheckPersistence(Path.Combine(root, "language-preferences"));
    }

    private static void CheckPersistence(string root)
    {
        Directory.CreateDirectory(root);
        var config = new AppConfig { Language = "en", GamePath = "fixture-game", BackgroundColor = "#20354A",
            ProfileOrder = new List<string> { "fixture-one", "fixture-two" } };
        var path = Path.Combine(root, "packframe.json");
        Check(config.TrySetLanguage("ja", path) && config.Language == "ja", "language preference applies after successful persistence");
        using (var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
            Check(saved.RootElement.GetProperty("language").GetString() == "ja" &&
                saved.RootElement.GetProperty("gamePath").GetString() == "fixture-game" && config.ProfileOrder.Count == 2,
                "language persistence retains unrelated app settings");
        var blocked = Path.Combine(root, "blocked.json"); Directory.CreateDirectory(blocked);
        File.WriteAllText(Path.Combine(blocked, "marker"), "keep");
        Check(!config.TrySetLanguage("zh-Hans", blocked) && config.Language == "ja" &&
            File.ReadAllText(Path.Combine(blocked, "marker")) == "keep",
            "failed language save preserves live language and existing destination content");
        var firstRun = new AppConfig();
        Check(!firstRun.TrySetLanguage("en", blocked) && firstRun.Language is null,
            "failed first-run language save leaves startup selection incomplete");
        Directory.Delete(blocked, recursive: true);
        Check(config.TrySetLanguage("zh-Hans", blocked) && config.Language == "zh-Hans" && File.Exists(blocked),
            "language save can be retried after repairing the destination");
        var invalid = Path.Combine(root, "must-not-create", "packframe.json");
        Check(!config.TrySetLanguage("unsupported", invalid) && config.Language == "zh-Hans" && !Directory.Exists(Path.GetDirectoryName(invalid)),
            "unsupported language neither changes live state nor creates configuration files");
    }
    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

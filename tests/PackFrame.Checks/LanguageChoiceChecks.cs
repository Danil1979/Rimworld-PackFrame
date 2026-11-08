using PackFrame;

internal static class LanguageChoiceChecks
{
    internal static void Run()
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
        }
        foreach (var unknown in new string?[] { null, string.Empty, "unknown" })
            Check(LanguageChoice.Options(unknown).All(c => !c.IsCurrent) && LanguageChoice.Title(unknown) == LanguageChoice.Title("en"),
                "first-run or unknown language does not mislabel a current choice");
    }
    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

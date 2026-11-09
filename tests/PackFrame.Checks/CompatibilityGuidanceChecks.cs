using PackFrame;

internal static class CompatibilityGuidanceChecks
{
    internal static void Run()
    {
        var clean = new ModInfo("Example", "Author", "/fixture", "example.mod", "Description", null,
            ModSource.Local, "1.6", DateTime.MinValue);
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            Require(CompatibilityGuidance.Issues(clean, language).Count == 0, "healthy mod has no compatibility card: " + language);
            var affected = clean with { SortOrderAfterNames = new() { "Framework" }, SortOrderAfterIds = new() { "framework" },
                MissingDependencyNames = new() { "missing.framework" }, IncompatibleNames = new() { "Conflicting Mod" },
                VersionMismatch = true, IsDuplicate = true, BadgeColumn = 1 };
            var issues = CompatibilityGuidance.Issues(affected, language);
            Require(issues.Count == 5 && issues.Select(x => x.Title).Distinct().Count() == 5, "diagnostics have five distinct categories: " + language);
            Require(issues[0].Body.Contains("Framework") && issues[1].Body.Contains("missing.framework") && issues[2].Body.Contains("Conflicting Mod"), "diagnosis preserves concrete requirements and conflicts: " + language);
            Require(issues.All(x => !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Body)) &&
                !string.IsNullOrWhiteSpace(CompatibilityGuidance.Apply(language)) && !string.IsNullOrWhiteSpace(CompatibilityGuidance.Help(language)), "diagnosis and action are localized: " + language);
            var repaired = affected with { SortOrderAfterNames = new(), SortOrderAfterIds = new() };
            Require(CompatibilityGuidance.Issues(repaired, language).Count == 4, "order repair leaves unrelated diagnostics visible: " + language);
            Require(CompatibilityGuidance.Issues(clean with { MissingDependencyNames = new() { "dependency" } }, language).Count == 1,
                "missing prerequisite alone remains independently diagnosable: " + language);
        }
        using var stream = typeof(CompatibilityGuidanceChecks).Assembly.GetManifestResourceStream("UiWiring.MainWindow.Compatibility.cs")!;
        var wiring = new StreamReader(stream).ReadToEnd();
        Require(wiring.Contains("ResolveActive(current)") && wiring.Contains("current.FolderName, folder") && wiring.Contains("fresh.SortOrderAfterIds.Count == 0"),
            "repair revalidates instance and current full-order requirements");
        Require(wiring.Contains("ApplySortSuggestion(fresh)") && !wiring.Contains("SavePendingMods") && !wiring.Contains("Install"),
            "diagnosis uses staged order repair without saving or installing");
        Require(wiring.Contains("FindResource(\"FlatButton\")") && wiring.Contains("TextWrapping = TextWrapping.Wrap, Foreground = ThemeService.PrimaryTextBrush"),
            "repair label wraps and uses adaptive foreground with shared focus styling");
        Require(CompatibilityGuidance.Help("en").Contains("only this mod") && CompatibilityGuidance.Help("en").Contains("Undo") &&
            CompatibilityGuidance.Help("en").Contains("separate attention"), "order action describes limited scope and staged recovery");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
}

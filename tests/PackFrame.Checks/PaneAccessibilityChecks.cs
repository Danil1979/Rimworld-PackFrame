using Avalonia.Automation;
using Avalonia.Controls;
using PackFrame;

internal static class PaneAccessibilityChecks
{
    internal static void Run()
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var available = PaneAccessibility.Create(language, 0, "Target colony");
            var primary = PaneAccessibility.Create(language, 1, "Frontier colony");
            var comparison = PaneAccessibility.Create(language, 2, "Frontier colony");
            Require(new[] { available.List, primary.List, comparison.List }.Distinct().Count() == 3,
                "each pane has a distinct accessible list name even for the same instance in " + language);
            Require(new[] { available.Search, primary.Search, comparison.Search }.Distinct().Count() == 3,
                "searches identify their pane in " + language);
            Require(available.List.Contains("Target colony") && primary.List.Contains("Frontier colony") && comparison.List.Contains("Frontier colony"),
                "instance identity includes the available catalog target and load-order panes in " + language);
            var list = new ListBox(); var search = new TextBox(); var field = new ComboBox();
            var instance = new ComboBox(); var manage = new Button();
            primary.Apply(list, search, field, instance, manage);
            Require(AutomationProperties.GetName(list) == primary.List && AutomationProperties.GetName(search) == primary.Search &&
                AutomationProperties.GetName(field) == primary.SearchField && AutomationProperties.GetName(instance) == primary.Instance &&
                AutomationProperties.GetName(manage) == primary.Manage,
                "contextual names reach actual accessibility properties in " + language);
            var renamed = PaneAccessibility.Create(language, 1, "New colony 名称");
            renamed.Apply(list, search, field, instance, manage);
            Require(new[] { list, (Control)search, field, instance, manage }.All(c =>
                AutomationProperties.GetName(c)!.Contains("New colony 名称") && !AutomationProperties.GetName(c)!.Contains("Frontier colony")),
                "changing instance replaces every stale accessible target in " + language);
            Require(!PaneAccessibility.Create(language, 2, null).Search.EndsWith(": "),
                "empty instance selection still has a useful pane name in " + language);
        }
        Require(PaneAccessibility.Create("ja", 0, null).Search != PaneAccessibility.Create("en", 0, null).Search,
            "pane accessibility follows language changes");
    }
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS: " + description);
    }
}

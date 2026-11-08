using PackFrame;

internal static class PaneEmptyGuidanceChecks
{
    internal static void Run()
    {
        var noGame = PaneEmptyGuidance.Create("en", false, false, false, false, 0, 0, false);
        Require(noGame.Title == "Choose your game folder", "empty pane without a game directs to game setup");
        var empty = PaneEmptyGuidance.Create("en", true, false, false, false, 0, 0, true);
        Require(empty.Title == "No installed mods found" && empty.Body.Contains("Refresh"),
            "empty catalog does not falsely claim all mods are already enabled");
        Require(PaneEmptyGuidance.Create("en", true, false, true, false, 0, 0, true) == empty,
            "filters cannot conceal the underlying empty-catalog recovery guidance");
        var missing = PaneEmptyGuidance.Create("en", true, false, false, true, 0, 2, true);
        Require(missing.Title == "2 mods unavailable" && missing.Body.Contains("load order is kept") && missing.Body.Contains("Missing below"),
            "all-missing load order points to exact recovery control without implying an empty saved order");
        Require(PaneEmptyGuidance.Create("en", true, true, true, true, 1, 2, true).Title == "No matching mods",
            "filtered installed rows offer search recovery before missing-only guidance");
        Require(PaneEmptyGuidance.Create("en", true, true, false, true, 0, 0, false).Title == "Choose an instance",
            "active pane without an instance identifies its selector");
        Require(PaneEmptyGuidance.Create("en", true, true, false, false, 0, 0, true).Body.Contains("already active"),
            "all-enabled inactive pane retains useful original guidance");
        Require(PaneEmptyGuidance.Create("en", true, true, false, true, 0, 0, true).Body.Contains("Enable above"),
            "genuinely empty active order points to its nearby enable action");
        var damaged = PaneEmptyGuidance.Create("en", true, true, true, true, 4, 2, true, isDamaged: true);
        Require(damaged.Title == "Instance unreadable" && damaged.Body.Contains("read-only") &&
            damaged.Body.Contains("Refresh") && !damaged.Body.Contains("Enable"),
            "damaged instance guidance takes precedence over filters and empty-list instructions");
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
            Require(!string.IsNullOrWhiteSpace(PaneEmptyGuidance.Create(language, true, true, false, true, 0, 0, true, true).Body),
                "unreadable-instance recovery instructions are available in " + language);
        foreach (var language in new[] { "zh-Hans", "zh-Hant", "ja" })
            Require(PaneEmptyGuidance.Create(language, true, false, false, true, 0, 2, true).Body != missing.Body,
                "missing-only recovery guidance is localized");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS: " + description);
    }
}

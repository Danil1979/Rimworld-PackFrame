using PackFrame;

internal static class PendingInstanceSummaryChecks
{
    internal static void Run()
    {
        var instances = Enumerable.Range(0, 40).Select(index => new ManagedInstance
        {
            FolderName = $"profile{index}",
            DirectoryPath = Path.Combine(Path.GetTempPath(), "pending-summary-check", $"profile{index}"),
            DisplayNameOverride = index < 2 ? "Same display name" : $"Instance {index}: " + new string('名', 300)
        }).ToList();
        var paths = instances.Select(instance => Path.GetFullPath(instance.ConfigDirectoryPath)).ToList();
        var before = instances.Select(instance => instance.DisplayName).ToArray();
        var rows = PendingInstanceSummary.Rows(paths, instances);
        Require(rows.Count == 40 && rows.Select(row => row.Name).SequenceEqual(before),
            "pending review keeps every instance and its complete long name");
        Require(rows[0].Name == rows[1].Name && rows[0].ConfigDirectoryPath != rows[1].ConfigDirectoryPath &&
            rows.Select(row => row.ConfigDirectoryPath).SequenceEqual(paths),
            "equally named pending instances retain distinct configuration paths");
        var renamed = instances[0].DisplayName;
        Require(PendingInstanceSummary.Rows(new[] { paths[0].ToUpperInvariant() }, instances)[0].Name == renamed,
            "pending lookup preserves the store's case-insensitive path identity");
        var unknown = Path.Combine(Path.GetTempPath(), "pending-summary-check", "Unavailable", "Config");
        var fallback = PendingInstanceSummary.Rows(new[] { unknown }, instances).Single();
        Require(fallback.Name == "Unavailable" && fallback.ConfigDirectoryPath == unknown,
            "an undiscovered edited instance remains identifiable in the review");
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var caption = PendingInstanceSummary.Caption(language, rows.Count);
            Require(caption.Contains("40") && caption.Length < 130 && !caption.Contains('\n') &&
                !caption.Contains(rows[0].Name) && !caption.Contains(rows[2].Name),
                "localized pending banner stays compact regardless of instance names");
            Require(!string.IsNullOrWhiteSpace(PendingInstanceSummary.ReviewLabel(language)),
                "pending review has a localized visible label");
        }
        Require(PendingInstanceSummary.Caption("en", 1).Contains("1 instance ·") &&
            PendingInstanceSummary.Caption("en", 40).Contains("Save applies to all edited instances") &&
            PendingInstanceSummary.Caption("en", 0) == "No unsaved mod-list changes",
            "pending summary distinguishes singular, all-instance save scope and clean state");
        Require(PendingInstanceSummary.Rows(Array.Empty<string>(), instances).Count == 0 &&
            before.SequenceEqual(instances.Select(instance => instance.DisplayName)) &&
            instances.All(instance => instance.ActivePackageIds.Count == 0),
            "pending presentation leaves names and mod lists unchanged and clears stale rows");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS: " + description);
    }
}

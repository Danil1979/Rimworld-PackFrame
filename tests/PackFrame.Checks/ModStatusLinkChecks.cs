using PackFrame;

internal static class ModStatusLinkChecks
{
    internal static void Run()
    {
        ModStatusListChecks.Run();
        var accepted = new[]
        {
            "https://steamcommunity.com/sharedfiles/filedetails/?id=1234567890",
            "https://github.com/example/mod#readme", " http://example.com/mod "
        };
        foreach (var link in accepted)
        {
            Uri? opened = null;
            Require(ModStatusLink.TryOpen(link, out var error, uri => opened = uri) && error is null &&
                opened?.AbsoluteUri == link.Trim(), "valid mod link invokes only the selected web URL");
        }
        foreach (var link in new string?[] { null, "", "—", "relative/path", "/tmp/file", "file:///tmp/file",
            "https://example.com\\path", "javascript:alert(1)", "steam://run/123", "https://user:password@example.com/", "https://example.com/\n" })
            Require(!ModStatusLink.TryOpen(link, out var error, _ => throw new Exception("Must not open")) && error is null,
                "unavailable or unsafe mod link cannot invoke a handler");

        var failure = new IOException("Synthetic browser failure");
        Require(!ModStatusLink.TryOpen(accepted[0], out var reported, _ => throw failure) && ReferenceEquals(failure, reported),
            "browser launch errors remain available for visible feedback and logging");
        var row = new MissingModRow { Label = "Example mod", Author = "Author", PackageId = "example.mod",
            Link = accepted[0], Availability = WorkshopModAvailability.Downloading };
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var display = ModStatusDisplayRow.From(row, language);
            Require(display.CanOpenLink && display.LinkAccessibleName.Contains(row.Label) &&
                display.Status == Localization.WorkshopStatus(language, row.Availability),
                "mod link has localized action, identifying accessible name and preserved availability");
        }
        var history = new MissingHistoryEntry { Label = "Old mod", Author = "Author", PackageId = "old.mod",
            Link = accepted[0], LastMissingAt = "2026-11-08 10:00" };
        var historyDisplay = ModStatusDisplayRow.From(history, "en");
        Require(historyDisplay.LastMissingAt == history.LastMissingAt && historyDisplay.Link == history.Link &&
            historyDisplay.CanOpenLink && history.LastMissingAt == "2026-11-08 10:00",
            "history links use a display-only model without rewriting stored entries");
        Require(!ModStatusDisplayRow.From(withLink(""), "en").CanOpenLink,
            "missing link has a disabled web action");

        MissingModRow withLink(string link) => new() { Label=row.Label, Author=row.Author, PackageId=row.PackageId,
            Link=link, Availability=row.Availability };
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS: " + description);
    }
}

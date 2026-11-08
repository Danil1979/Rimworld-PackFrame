using PackFrame;

internal static class ModpackFooterChecks
{
    public static void Run()
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var selected = ModpackFooterStatus.SelectedHint(language, true);
            var unavailable = ModpackFooterStatus.SelectedHint(language, false);
            Require(!string.IsNullOrWhiteSpace(selected) && selected != Localization.ModpackSelectHint(language),
                $"{language}: cached/online selection does not show no-selection instructions");
            Require(ModpackFooterStatus.IsSelectionHint(selected, language) &&
                    ModpackFooterStatus.IsSelectionHint(unavailable, language) &&
                    !ModpackFooterStatus.IsSelectionHint("Operation failed", language),
                $"{language}: idle guidance can track refreshed capabilities without erasing errors");
            Require(!string.IsNullOrWhiteSpace(unavailable) && unavailable != selected && unavailable != Localization.ModpackSelectHint(language),
                $"{language}: packs without installable sources have distinct selected guidance");
        }
        Require(ModpackFooterStatus.SelectedHint("en", true).Contains("requires a connection"),
            "selected guidance does not imply cached packs are installable offline");
        Require(ModpackFooterStatus.CanReplace("Loading", "Loading", false, false),
            "current detail loading message can become selected guidance");
        Require(!ModpackFooterStatus.CanReplace("Invalid Workshop link", "Loading", false, false),
            "late detail success or failure preserves a newer validation error");
        Require(!ModpackFooterStatus.CanReplace("Installing", "Installing", false, true),
            "detail guidance cannot replace active operation status");
        Require(!ModpackFooterStatus.CanReplace("Update complete", "Update complete", true, false) &&
                !ModpackFooterStatus.CanReplace("Cancelled", "Cancelled", true, false),
            "deferred same-selection catalog refresh retains operation completion and cancellation");
        Require(ModpackFooterStatus.CanReplace("Selected", "Selected", false, false),
            "explicit refresh/new selection can update idle guidance");
    }
    static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

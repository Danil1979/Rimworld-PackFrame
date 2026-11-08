using PackFrame;

internal static class DialogStateChecks
{
    public static void Run(string root)
    {
        CheckColorCancellation(Path.Combine(root, "dialog-colors"));
        CheckUpdateChoices();
        CheckExportDrafts();
        CheckExportOperationGate();
        CheckExportNavigation();
        CheckExportSaveFailures(Path.Combine(root, "export-save-failures"));
    }

    private static void CheckColorCancellation(string root)
    {
        var original = new Dictionary<string, string> { ["#E05A5A"] = "Original label" };
        var edited = new Dictionary<string, string> { ["#E05A5A"] = "Edited label" };
        ModColorStore.SetLabels(root, original);
        ModColorStore.SetModColors(root, new Dictionary<string, string?> { ["test.mod"] = "#E05A5A" });
        var before = File.ReadAllText(ModColorStore.GetPath(root));
        Require(!ModColorChoiceCommit.SaveLabels(root, null) &&
            !ModColorChoiceCommit.SaveLabels(root, new ModColorChoice(true, false, null, edited)),
            "dismissed and cancelled palettes do not commit labels");
        Require(File.ReadAllText(ModColorStore.GetPath(root)) == before &&
            ModColorStore.Load(root).ColorLabels["#E05A5A"] == "Original label",
            "palette cancellation preserves both saved labels and the color cache");
        Require(ModColorChoiceCommit.SaveLabels(root, new ModColorChoice(false, false, "#E05A5A", edited)) &&
            ModColorStore.Load(root).ColorLabels["#E05A5A"] == "Edited label",
            "choosing a palette color commits label edits");
        Require(ModColorChoiceCommit.SaveLabels(root, new ModColorChoice(false, true, null, original)) &&
            ModColorStore.Load(root).ColorLabels["#E05A5A"] == "Original label" &&
            ModColorStore.GetColor(root, "test.mod") == "#E05A5A",
            "clear-color choice commits labels without changing unrelated mod colors");
        var absentRoot = Path.Combine(root, "not-created");
        Require(!ModColorChoiceCommit.SaveLabels(absentRoot, null) && !Directory.Exists(absentRoot),
            "dismissing an unused palette creates no settings directory");
    }

    private static void CheckUpdateChoices()
    {
        var replacements = new Dictionary<string, bool>
        {
            ["Config/Local.xml"] = false,
            ["Config/Incoming.xml"] = true,
            ["Mods/Custom"] = false
        };
        var choice = UpdateChoices.FromReplacements(replacements, true);
        Require(choice.KeepLocal.SetEquals(new[] { "config/local.xml", "mods/custom" }) && choice.KeepOrder,
            "update review retains unchecked local files and the selected order");
        Require(!choice.KeepLocal.Contains("Config/Incoming.xml"),
            "checked update-review items use the incoming update");
        replacements["Config/Local.xml"] = true;
        Require(choice.KeepLocal.Contains("Config/Local.xml"),
            "confirmed update choices are independent of later selection edits");
        var allIncoming = UpdateChoices.FromReplacements(
            new Dictionary<string, bool> { ["Config/Incoming.xml"] = true }, false);
        Require(allIncoming.KeepLocal.Count == 0 && !allIncoming.KeepOrder,
            "existing update replacement and order defaults are preserved");
        var empty = UpdateChoices.FromReplacements(Array.Empty<KeyValuePair<string, bool>>(), false);
        Require(empty.KeepLocal.Count == 0 && !empty.KeepOrder,
            "update review handles plans with no conflicting files");
    }

    private static void CheckExportDrafts()
    {
        var untouched = new ExportCollectionDraft();
        Require(untouched.RefreshDefault(null, "Pack") == "Pack",
            "export navigation initializes an unset Workshop field");
        Require(untouched.RefreshDefault("Pack", "Renamed pack") == "Renamed pack",
            "untouched Workshop title follows edited pack defaults");
        var titleDraft = new ExportCollectionDraft();
        var descriptionDraft = new ExportCollectionDraft();
        titleDraft.RefreshDefault(null, "Pack");
        descriptionDraft.RefreshDefault(null, "Pack description");
        var title = "My Workshop title";
        var description = "My description\n\n[URL]Framework link[/URL]";
        for (var navigation = 0; navigation < 3; navigation++)
        {
            var nextTitle = "Pack " + navigation;
            var nextDescription = "Pack description " + navigation;
            title = titleDraft.RefreshDefault(title, nextTitle);
            description = descriptionDraft.RefreshDefault(description, nextDescription);
        }
        Require(title == "My Workshop title" && description == "My description\n\n[URL]Framework link[/URL]",
            "repeated export Back/Next navigation preserves edited Workshop text and inserted links");
        Require(descriptionDraft.RefreshDefault(string.Empty, "New description") == string.Empty,
            "export navigation preserves an intentionally cleared Workshop description");
        titleDraft.RefreshDefault(title, title);
        Require(titleDraft.RefreshDefault(title, "Changed pack again") == title,
            "edited Workshop fields stay independent even when a later pack default matches them");
        var indexUrl = new ExportCollectionDraft();
        indexUrl.RefreshDefault(null, "https://gitee.com/owner/pack");
        Require(indexUrl.RefreshDefault(string.Empty, "https://gitee.com/owner/pack") == string.Empty &&
            indexUrl.RefreshDefault(string.Empty, "https://gitee.com/owner/pack") == string.Empty,
            "clearing an index destination remains a deliberate skip across repeated Back/Next navigation");
    }

    private static void CheckExportNavigation()
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var steps = Enumerable.Range(1, ExportWizardNavigation.PageCount)
                .Select(page => ExportWizardNavigation.Describe(page, language)).ToArray();
            Require(steps.All(step => !string.IsNullOrWhiteSpace(step.Step) &&
                !string.IsNullOrWhiteSpace(step.Hint) && !string.IsNullOrWhiteSpace(step.ExitHint)) &&
                steps.Select(step => step.Step).Distinct().Count() == 5,
                $"all export stages have distinct localized progress and purpose guidance ({language})");
            Require(steps.Take(4).All(step => step.ShowExit) && !steps[4].ShowExit &&
                steps[4].Next == Localization.CloseButton(language),
                $"export permits early exit and final Close never implies submission ({language})");
            Require(steps[1].Next.Contains("Gitee") && steps[2].Next.Contains("GitHub") &&
                steps[0].Exit != steps[0].Next && steps[0].Exit != Localization.CloseButton(language),
                $"export navigation names publishing destinations and distinguishes local save routes ({language})");
        }
        Require(ExportWizardNavigation.Describe(1, "unsupported") == ExportWizardNavigation.Describe(1, "en"),
            "export navigation falls back consistently to English");
        foreach (var invalid in new[] { 0, 6 })
        {
            var rejected = false;
            try { ExportWizardNavigation.Describe(invalid, "en"); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, $"export navigation rejects invalid stage {invalid}");
        }
        // These are inert fixture values, not credentials. No service is contacted.
        foreach (var field in new[] { "GitHub index token", "Gitee index token", "Workshop link" })
        {
            var draft = new ExportCollectionDraft();
            Require(draft.RefreshDefault(null, "fixture-first") == "fixture-first" &&
                draft.RefreshDefault("fixture-first", "fixture-updated") == "fixture-updated",
                $"untouched {field} follows its source before editing");
            Require(draft.RefreshDefault(string.Empty, "fixture-updated") == string.Empty &&
                draft.RefreshDefault(string.Empty, "fixture-later") == string.Empty,
                $"intentionally cleared {field} stays empty across repeated navigation");
            Require(draft.RefreshDefault("fixture-custom", "fixture-new-default") == "fixture-custom",
                $"custom {field} survives a changed source default");
        }
    }

    private static void CheckExportSaveFailures(string root)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "pack.json");
        var missingRejected = false;
        try { ModpackRepoExporter.SetPackInfo(root, "Name", "Description"); }
        catch (FileNotFoundException) { missingRejected = true; }
        Require(missingRejected && !File.Exists(path),
            "export save rejects a missing manifest instead of reporting success");
        foreach (var original in new[] { "null", "{broken", "[]" })
        {
            File.WriteAllText(path, original);
            var rejected = false;
            try { ModpackRepoExporter.SetPackInfo(root, "Name", "Description"); }
            catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException)
                { rejected = true; }
            Require(rejected && File.ReadAllText(path) == original,
                $"export save rejects and preserves invalid manifest {original}");
        }
        File.Delete(path);
        Directory.CreateDirectory(path);
        var unreadableRejected = false;
        try { ModpackRepoExporter.SetPackInfo(root, "Name", "Description"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { unreadableRejected = true; }
        Require(unreadableRejected && Directory.Exists(path),
            "export save does not silently skip an unreadable manifest path");
        Directory.Delete(path);
        File.WriteAllText(path, "{\"id\":\"fixture.stable\",\"name\":\"Before\"}");
        ModpackRepoExporter.SetPackInfo(root, "After", "Recovered", "1.0", "Saved after repair");
        using var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        Require(saved.RootElement.GetProperty("name").GetString() == "After" &&
            saved.RootElement.GetProperty("id").GetString() == "fixture.stable",
            "export save retries successfully after the manifest is repaired without changing identity");
    }

    private static void CheckExportOperationGate()
    {
        var gate = new ExportOperationGate();
        Require(!gate.IsActive && gate.TryBegin() && gate.IsActive,
            "export operation gate is acquired before asynchronous preparation");
        var preparations = 1;
        for (var repeatedClick = 0; repeatedClick < 5; repeatedClick++)
            if (gate.TryBegin()) preparations++;
        Require(preparations == 1 && gate.IsActive,
            "repeated export actions cannot start a second preparation while one is active");
        try { throw new IOException("Simulated preparation failure"); }
        catch (IOException) { }
        finally { gate.End(); }
        Require(!gate.IsActive && gate.TryBegin(),
            "export operation gate permits retry after a failed operation releases it");
        gate.End();
        Require(!gate.IsActive,
            "completed export operations allow navigation and closing again");
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

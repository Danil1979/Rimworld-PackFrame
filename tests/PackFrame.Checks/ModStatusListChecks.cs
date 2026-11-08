using System.Collections.ObjectModel;
using PackFrame;

internal static class ModStatusListChecks
{
    internal static void Run()
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var accessible = new ModStatusListRow(new ModStatusDisplayRow("Example mod", "Author", "example.mod",
                "", "Downloading", "", language));
            Require(accessible.AccessibleName.Contains("Example mod") &&
                accessible.AccessibleName.Contains(Localization.FieldPackageId(language) + ": example.mod") &&
                accessible.AccessibleName.Contains(Localization.StatusHeader(language) + ": Downloading"),
                "status row accessibility names include label, package and localized status field");
            var notified = false;
            accessible.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(ModStatusListRow.AccessibleName);
            accessible.Update(accessible.Snapshot with { Status = "", LastMissingAt = "2026-11-08" });
            Require(notified && accessible.AccessibleName.Contains(Localization.LastMissing(language) + ": 2026-11-08") &&
                !accessible.AccessibleName.Contains("Downloading"),
                "history accessibility names announce the localized date field and refresh with row data");
        }
        var rows = Enumerable.Range(0, 10000).Select(i => new ModStatusDisplayRow(
            $"Example {i}", "Author", $"example.{i}", $"https://example.com/{i}",
            "Downloading", "2026-11-08", "en")).ToArray();
        Require(ModStatusList.Filter(rows, "  EXAMPLE.9999  ").Single() == rows[9999], "search trims and ignores case across a large snapshot");
        foreach (var query in new[] { "Author", "Downloading", "2026-11-08", "https://example.com/" })
            Require(ModStatusList.Filter(rows, query).Count == rows.Length, "search includes metadata, state, date and link");
        Require(ModStatusList.Filter(rows, "absent").Count == 0 && ModStatusList.Filter(rows, " ").Count == rows.Length,
            "no matches and cleared search return accurate counts without mutating source");
        var visible = new ObservableCollection<ModStatusListRow>();
        ModStatusList.Reconcile(visible, rows);
        var firstContainer = visible[0];
        var lastContainer = visible[9999];
        var changedProperties = 0;
        firstContainer.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ModStatusListRow.Snapshot)) changedProperties++; };
        var events = 0;
        visible.CollectionChanged += (_, _) => events++;
        ModStatusList.Reconcile(visible, rows.Select(row => row with { }).ToList());
        Require(events == 0 && ReferenceEquals(visible[9000].Snapshot, rows[9000]), "unchanged refresh retains row objects with zero collection changes");
        var updated = rows.ToArray();
        updated[0] = rows[0] with { Status = "Missing" };
        ModStatusList.Reconcile(visible, updated);
        Require(events == 0 && changedProperties == 1 && ReferenceEquals(visible[0], firstContainer) && visible[0].Snapshot.Status == "Missing" && ReferenceEquals(visible[9000].Snapshot, rows[9000]),
            "live refresh notifies only the changed row without replacing focused row data contexts");
        var filtered = ModStatusList.Filter(updated, "example.9999");
        ModStatusList.Reconcile(visible, filtered);
        Require(visible.Count == 1 && ReferenceEquals(visible[0], lastContainer), "filter retains matching row identity");
        ModStatusList.Reconcile(visible, updated);
        Require(visible.Select(row => row.Snapshot).SequenceEqual(updated) && rows[0].Status == "Downloading", "clear search restores all display rows without altering source data");
        ModStatusList.Reconcile(visible, updated.Reverse().ToArray());
        Require(visible.Select(row => row.Snapshot).SequenceEqual(updated.Reverse()), "reconciliation handles source ordering changes");
        ModStatusList.Reconcile(visible, Array.Empty<ModStatusDisplayRow>());
        Require(visible.Count == 0, "empty snapshot removes stale displayed entries");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS: " + description);
    }
}

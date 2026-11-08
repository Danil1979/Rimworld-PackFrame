using PackFrame;

internal static class CatalogSearchChecks
{
    internal static void Run()
    {
        var items = Enumerable.Range(0, 10000).Select(i => new ModpackListItem {
            Entry = new ModpackEntry { Id = $"fixture.{i}", Name = $"Colony {i}",
                Summary = i % 2 == 0 ? "Construction" : "Exploration", File = $"pack{i}.json" }
        }).ToList();
        Require(CatalogSearch.Filter(items, "  FIXTURE.9999  ").Single() == items[9999],
            "catalog search trims and matches large-list identifiers without copying entries");
        Require(CatalogSearch.Filter(items, "construction").Count == 5000 &&
            CatalogSearch.Filter(items, "colony").Count == 10000,
            "catalog search matches summaries and names without changing their order");
        Require(CatalogSearch.Filter(items, "unknown").Count == 0 &&
            CatalogSearch.Filter(items, "  ").SequenceEqual(items),
            "catalog no-match and clear states retain the complete source snapshot");
        var visible = CatalogSearch.Filter(items, "Construction");
        Require(CatalogSearch.Find(visible, items[9999].Entry) == -1 &&
            CatalogSearch.Find(items, items[9999].Entry) == 9999,
            "a hidden selection cannot resolve to an unrelated visible install target");
        Require(CatalogSearch.Find(visible, items[9998].Entry) == 4999,
            "matching selection retains identity through filtering");
        var duplicate = new[] {
            new ModpackListItem { Entry = new ModpackEntry { Id = "one", Name = "Same name", File = "one.json" } },
            new ModpackListItem { Entry = new ModpackEntry { Id = "two", Name = "Same name", File = "two.json" } }
        };
        Require(CatalogSearch.Find(duplicate, new ModpackEntry { Id = "two", Name = "Renamed" }) == 1,
            "catalog refresh matches stable IDs rather than duplicate or changed display names");
        Require(CatalogSearch.Find(duplicate, new ModpackEntry { Id = "removed", Name = "Same name" }) == -1,
            "removed catalog target does not match a same-name replacement");
        var legacy = new[] {
            new ModpackListItem { Entry = new ModpackEntry { File = "pack.json", GiteeFile = "mirror-a.json" } },
            new ModpackListItem { Entry = new ModpackEntry { File = "pack.json", GiteeFile = "mirror-b.json" } }
        };
        Require(CatalogSearch.Find(legacy, new ModpackEntry { File = "pack.json", GiteeFile = "mirror-b.json" }) == 1,
            "ID-less catalogs preserve the full existing file and mirror identity");
        Require(CatalogSearch.Find(items, null) == -1 && CatalogSearch.Find(Array.Empty<ModpackListItem>(), items[0].Entry) == -1,
            "empty catalog and absent selection remain unselected");
        Require(CatalogSearch.Filter(legacy, "Modpack").Count == 2 && items[9999].Entry.Id == "fixture.9999",
            "optional metadata uses display fallbacks and search never mutates catalog entries");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

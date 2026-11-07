using PackFrame;

internal static class LoadOrderChecks
{
    public static void Run()
    {
        var core = Mod("core");
        var xml = Mod("xml");
        var tool = Mod("tool");
        var source = new[] { core, xml, tool };
        var order = new[] { "core", "missing.mod", "xml", "tool" };
        var numbered = ModListOperations.WithLoadOrder(source, order);
        Require(numbered.Select(mod => mod.LoadOrder).SequenceEqual(new int?[] { 1, 3, 4 }),
            "load-order numbers preserve missing-package slots in the real order");
        Require(numbered.Where(mod => mod.PackageId == "xml").Single().LoadOrder == 3,
            "search results keep the original load-order number");
        Require(source.All(mod => mod.LoadOrder is null && !mod.HasLoadOrder && mod.LoadOrderText == string.Empty) &&
                numbered.All(mod => mod.HasLoadOrder) && numbered[1].LoadOrderText == "3" &&
                !ReferenceEquals(numbered[0], core),
            "numbered views do not add positions to the shared inactive catalogue");

        var largeOrder = Enumerable.Range(1, 304).Select(index => "missing." + index).ToArray();
        largeOrder[0] = "core";
        largeOrder[^1] = "xml";
        var largeView = ModListOperations.WithLoadOrder(new[] { core, xml }, largeOrder);
        Require(largeView[0].LoadOrder == 1 && largeView[1].LoadOrder == 304 &&
                largeView[0].LoadOrderGutterWidth == largeView[1].LoadOrderGutterWidth &&
                largeView[0].LoadOrderGutterWidth > numbered[0].LoadOrderGutterWidth,
            "missing entries count toward a stable gutter for every row in the full load order");
        var filteredLargeView = largeView.Where(mod => mod.PackageId == "core").ToList();
        Require(filteredLargeView.Single().LoadOrderGutterWidth == largeView[1].LoadOrderGutterWidth &&
                source.All(mod => mod.LoadOrderGutterWidth == 0),
            "a one-result search retains the full-order gutter without changing the shared catalogue");

        var thousandOrder = largeOrder.Concat(Enumerable.Range(305, 696).Select(index => "missing." + index)).ToArray();
        var thousandView = ModListOperations.WithLoadOrder(new[] { core, xml }, thousandOrder);
        Require(thousandView.All(mod => mod.LoadOrderGutterWidth > largeView[0].LoadOrderGutterWidth) &&
                largeView.All(mod => mod.LoadOrderGutterWidth == filteredLargeView[0].LoadOrderGutterWidth),
            "four-digit orders expand independently without changing an existing view's gutter");

        var otherView = ModListOperations.WithLoadOrder(new[] { xml, core }, new[] { "xml", "core" });
        Require(otherView.Select(mod => mod.LoadOrder).SequenceEqual(new int?[] { 1, 2 }) &&
                numbered[0].LoadOrder == 1 && numbered[1].LoadOrder == 3 &&
                !ReferenceEquals(otherView[0], numbered[1]) &&
                otherView.All(mod => mod.LoadOrderGutterWidth < largeView[0].LoadOrderGutterWidth),
            "different instance views number shared mods independently");
        otherView[0].SearchMatched = false;
        Require(xml.SearchMatched && numbered[1].SearchMatched,
            "search state in a numbered view does not mutate another view's row");

        var repeated = ModListOperations.WithLoadOrder(
            new[] { core, core, xml }, new[] { "CORE", "core", "missing.mod", "XML" });
        Require(repeated.Select(mod => mod.LoadOrder).SequenceEqual(new int?[] { 1, 2, 4 }) &&
                !ReferenceEquals(repeated[0], repeated[1]),
            "duplicate package occurrences keep distinct case-insensitive load-order positions");

        var moved = ModListOperations.InsertSelection(order, new[] { "tool" }, 0);
        var reordered = ModListOperations.WithLoadOrder(new[] { tool, core, xml }, moved);
        Require(reordered.Select(mod => mod.LoadOrder).SequenceEqual(new int?[] { 1, 2, 4 }) &&
                numbered[2].LoadOrder == 4 && moved.SequenceEqual(new[] { "tool", "core", "missing.mod", "xml" }),
            "reordering recalculates positions without changing prior view snapshots");

        var absent = ModListOperations.WithLoadOrder(new[] { tool }, Array.Empty<string>()).Single();
        Require(absent.LoadOrder is null && !absent.HasLoadOrder && absent.LoadOrderText == string.Empty &&
                absent.LoadOrderGutterWidth == 0,
            "mods outside an enabled order have no displayed load-order number");
    }

    private static ModInfo Mod(string id) => new(
        id, "test", string.Empty, id, string.Empty, null, ModSource.Local, "1.7", DateTime.MinValue);

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

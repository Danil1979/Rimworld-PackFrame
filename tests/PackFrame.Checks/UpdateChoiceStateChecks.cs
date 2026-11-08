using PackFrame;

internal static class UpdateChoiceStateChecks
{
    internal static void Run()
    {
        var paths = Enumerable.Range(1, 10000).Select(i => $"Config/Settings {i:00000}.xml").ToArray();
        var state = new UpdateChoiceState(paths, "en");
        Require(state.Rows.Count == 10000 && state.UseUpdateCount == 10000,
            "large update review retains the existing use-update default for every unique file");
        var first = state.Rows[0];
        var changes = new List<string?>();
        first.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        first.UseUpdate = false;
        Require(state.UseUpdateCount == 9999 && changes.SequenceEqual(new[] { "UseUpdate", "AccessibleName" }) &&
            first.AccessibleName.StartsWith("Keep local: "), "a file decision updates both count and accessible state");
        first.UseUpdate = false;
        Require(changes.Count == 2, "unchanged file decisions do not trigger redundant row updates");
        var filtered = state.Filter(" settings 10000.XML ");
        Require(filtered.Count == 1 && ReferenceEquals(filtered[0], state.Rows[^1]) && !first.UseUpdate,
            "case-insensitive file search keeps stable rows and hidden decisions");
        filtered[0].UseUpdate = false;
        Require(state.Confirm(true).KeepLocal.SetEquals(new[] { paths[0], paths[^1] }),
            "confirmation includes kept files hidden by the current search");
        Require(state.Filter("no such file").Count == 0 && state.UseUpdateCount == 9998,
            "a no-result query never resets decisions");
        Require(state.Filter(" ").Count == 10000 && ReferenceEquals(state.Filter(null)[0], first),
            "clearing search restores original row identities and decisions");
        state.SetAll(false);
        var keep = state.Confirm(true);
        Require(state.UseUpdateCount == 0 && keep.KeepLocal.Count == 10000 && keep.KeepOrder,
            "Keep all local includes every hidden file and retains the requested order choice");
        state.SetAll(true);
        var incoming = state.Confirm(false);
        Require(state.UseUpdateCount == 10000 && incoming.KeepLocal.Count == 0 && !incoming.KeepOrder,
            "Use update for all restores every file including hidden results");
        Require(keep.KeepLocal.Count == 10000 && keep.KeepOrder,
            "confirmed choices are immutable snapshots independent of later bulk changes");
        var duplicates = new UpdateChoiceState(new[] { "Config/A.xml", "config/a.XML", "Config/B.xml" }, "en");
        Require(duplicates.Rows.Count == 2 && duplicates.Rows[0].Path == "Config/A.xml",
            "case-insensitive duplicate conflict paths receive one unambiguous decision");
        duplicates.Rows[0].UseUpdate = false;
        Require(duplicates.Confirm(false).KeepLocal.Contains("CONFIG/A.XML"),
            "kept path identity matches the update service's case-insensitive semantics");
        var empty = new UpdateChoiceState(Array.Empty<string>(), "ja");
        empty.SetAll(false);
        Require(empty.Filter(null).Count == 0 && empty.UseUpdateCount == 0 && empty.Confirm(false).KeepLocal.Count == 0,
            "no-conflict plans need no file decisions");
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var row = new UpdateChoiceState(new[] { "Config/設定.xml" }, language).Rows[0];
            var before = row.AccessibleName; row.UseUpdate = false;
            Require(before.Contains(row.Path) && row.AccessibleName.Contains(row.Path) && before != row.AccessibleName,
                "file decision accessible labels distinguish states in " + language);
        }
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

using System.Text.Json;
using PackFrame;

internal static class PaneLayoutChecks
{
    public static void Run()
    {
        foreach (var (tag, source, target) in new[] { ("0:1", 0, 1), ("0:2", 0, 2), ("1:0", 1, 0), ("2:0", 2, 0) })
            Require(MainWindow.TryGetPaneActionRoute(tag, out var actualSource, out var actualTarget) &&
                    actualSource == source && actualTarget == target,
                $"pane action {tag} explicitly identifies its selection source and destination");
        foreach (var tag in new string?[] { null, "", "1", "0:0", "1:2", "2:1", "3:0", "0:3", "-1:0", "0:1:2" })
            Require(!MainWindow.TryGetPaneActionRoute(tag, out _, out _),
                $"invalid pane action '{tag}' cannot silently fall back to the last-focused list");
        Require(!new AppConfig().ShowSecondaryPane,
            "new users start with a single enabled pane");
        var unspecified = Read("{\"language\":\"en\",\"profileFolder1\":\"profile1\"}");
        Require(!unspecified.ShowSecondaryPane && unspecified.ProfileFolder1 == "profile1",
            "settings without a saved layout use one pane while preserving instance selection");

        foreach (var visible in new[] { false, true })
        {
            var saved = Read($"{{\"showSecondaryPane\":{visible.ToString().ToLowerInvariant()},\"profileFolder2\":\"profile2\"}}");
            var roundTrip = Read(JsonSerializer.Serialize(saved, PackFrameJsonContext.Default.AppConfig));
            Require(roundTrip.ShowSecondaryPane == visible && roundTrip.ProfileFolder2 == "profile2",
                $"saved {(visible ? "split" : "single")} layout survives settings serialization");
        }
    }

    private static AppConfig Read(string json) =>
        JsonSerializer.Deserialize(json, PackFrameJsonContext.Default.AppConfig)
        ?? throw new Exception("FAIL: pane layout settings were null");

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

using PackFrame;
internal static class AvailableTargetGuidanceChecks
{
    internal static void Run()
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var first = AvailableTargetGuidance.Create(language, "Frontier colony", "/one/Config");
            var second = AvailableTargetGuidance.Create(language, "Experimental colony", "/two/Config");
            Require(first.Subtitle.Contains("Frontier colony") && first.Action.Contains("Frontier colony") && first.Help.Contains("/one/Config"), "catalog target visible in caption, default action, and path help: " + language);
            Require(!second.Subtitle.Contains("Frontier colony") && second.Action.Contains("Experimental colony") && second.Help.Contains("/two/Config"), "switching target replaces stale catalog guidance: " + language);
            Require(first.Subtitle.Count(c => c == '\n') == 1 && first.Action.Count(c => c == '\n') == 1, "catalog target has a reserved second line: " + language);
            var missing = AvailableTargetGuidance.Create(language, null, null);
            Require(!string.IsNullOrWhiteSpace(missing.Action) && !missing.Action.Contains("Enter"), "no instance does not promise an enable destination: " + language);
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
}

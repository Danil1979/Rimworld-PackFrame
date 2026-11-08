using PackFrame;

internal static class LaunchSavePromptChecks
{
    internal static async Task RunAsync()
    {
        var saves = 0;
        Require(!await LaunchSavePrompt.ResolveAsync(() => Task.FromResult(false), () => { saves++; return true; }, () => false)
            && saves == 0, "cancel launch keeps edits without saving");
        Require(!await LaunchSavePrompt.ResolveAsync(() => Task.FromResult(true), () => false, () => false),
            "failed save never permits launch");
        Require(!await LaunchSavePrompt.ResolveAsync(() => Task.FromResult(true), () => true, () => true),
            "remaining pending edits never permit launch");
        Require(await LaunchSavePrompt.ResolveAsync(() => Task.FromResult(true), () => { saves++; return true; }, () => false)
            && saves == 1, "confirmed successful save permits one launch continuation");
        try
        {
            await LaunchSavePrompt.ResolveAsync(() => Task.FromResult(true), () => throw new IOException("fixture"), () => false);
            throw new Exception("Expected save exception");
        }
        catch (IOException) { Require(true, "save exception cannot fall through to launch"); }
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var rows = new[] { new PendingInstanceRow("Same name", "/one/Config"), new PendingInstanceRow("Same name", "/two/Config") };
            var message = LaunchSavePrompt.Message(language, "Target colony", rows);
            Require(message.Contains("Target colony") && message.Contains("2") && message.Contains("/one/Config") && message.Contains("/two/Config"),
                "launch save review names target and distinct pending paths in " + language);
            Require(LaunchSavePrompt.ConfirmLabel(language) != LaunchSavePrompt.CancelLabel(language),
                "launch choices are distinct in " + language);
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}

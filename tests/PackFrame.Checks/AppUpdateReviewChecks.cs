using PackFrame;

internal static class AppUpdateReviewChecks
{
    internal static void Run()
    {
        Uri? opened = null;
        AppUpdateWindow.OpenReleasePage("https://example.com/review-release", uri => opened = uri);
        Require(opened?.AbsoluteUri == "https://example.com/review-release",
            "update release action forwards a validated web address to the opener");
        foreach (var invalid in new[] { "file:///tmp/not-a-release", "https://user:secret@example.com/release" })
        {
            var invoked = false;
            var rejected = false;
            try { AppUpdateWindow.OpenReleasePage(invalid, _ => invoked = true); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && !invoked, "update release action rejects an unsafe address before invoking the opener");
        }
        var failure = new IOException("Synthetic browser unavailable");
        var propagated = false;
        try { AppUpdateWindow.OpenReleasePage("https://example.com/review-release", _ => throw failure); }
        catch (IOException error) { propagated = ReferenceEquals(error, failure); }
        Require(propagated, "update release opener failures reach the dialog recovery boundary rather than disappearing");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}

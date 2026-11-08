using PackFrame;

internal static class PublishingCredentialChecks
{
    internal static void Run()
    {
        foreach (var language in new[] { Localization.English, Localization.SimplifiedChinese,
            Localization.TraditionalChinese, Localization.Japanese })
        {
            var github = Localization.ExportCollectionTokenHint(language);
            var gitee = Localization.ExportCollectionGiteeTokenHint(language);
            var index = Localization.ExportCollectionIndexTokenHint(language);
            Require(github.Contains("public_repo", StringComparison.Ordinal) &&
                gitee.Contains("projects", StringComparison.Ordinal) &&
                index.Contains("public_repo", StringComparison.Ordinal),
                "publishing credential scope guidance is present in " + language);
            Require(new[] { github, gitee, index }.All(hint => hint.Contains("packframe.json", StringComparison.Ordinal)),
                "each publishing credential step explains local token storage in " + language);
        }

        // Keep this guidance tied to the persisted configuration contract, not a
        // promise that managed strings are immediately erased on window close.
        var persistedProperties = typeof(AppConfig).GetProperties().Select(property => property.Name).ToHashSet();
        Require(!persistedProperties.Contains("GithubToken") && !persistedProperties.Contains("GitHubToken") &&
            !persistedProperties.Contains("GiteeToken") && persistedProperties.Contains("OssAccessKeySecret"),
            "publishing token guidance distinguishes window-only tokens from persisted OSS credentials");
        Require(Localization.ExportCollectionIndexTokenHint(Localization.English).Contains("write access", StringComparison.Ordinal),
            "Gitee index guidance explains the destination repository write-access requirement");
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

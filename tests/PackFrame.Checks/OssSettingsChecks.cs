using System.Reflection;
using PackFrame;

internal static class OssSettingsChecks
{
    internal static void Run()
    {
        CheckEndpoints();
        CheckBucketsAndKeys();
        CheckSaving();
    }

    private static OssSettingsDraft Valid(string endpoint = "oss-cn-hangzhou.aliyuncs.com") =>
        new(true, endpoint, "test-bucket", "packs/", "synthetic-id", "synthetic-placeholder-not-a-key");

    private static void CheckEndpoints()
    {
        foreach (var (input, expected) in new[]
        {
            ("oss-cn-hangzhou.aliyuncs.com", "oss-cn-hangzhou.aliyuncs.com"),
            (" https://oss-cn-hangzhou.aliyuncs.com/ ", "oss-cn-hangzhou.aliyuncs.com"),
            ("gateway.internal:9443", "gateway.internal:9443"),
            ("https://gateway.internal:9443/", "gateway.internal:9443"),
            ("localhost:9443", "localhost:9443"),
            ("https://bücher.example:9443/", "xn--bcher-kva.example:9443"),
            ("https://custom.example:443/", "custom.example")
        })
        {
            var validation = Valid(input).Validate();
            Require(validation.IsValid && validation.Draft.Endpoint == expected,
                "OSS accepts and normalizes endpoint " + input.Trim());
        }

        foreach (var input in new[]
        {
            "http://oss.example", "ftp://oss.example", "https://oss.example/bucket",
            "oss.example/path", "https://oss.example?token=synthetic", "https://oss.example#fragment",
            "https://synthetic-user:synthetic-placeholder@oss.example", "bad host.example", "oss.example\\path",
            "oss.example:65536", "oss.example:0", "oss.example:abc", "127.0.0.1", "https://[::1]:9443",
            "bad_host.example", "-bad.example", "bad..example"
        })
        {
            var validation = Valid(input).Validate();
            Require(!validation.IsValid && validation.Errors.ContainsKey(OssSettingsField.Endpoint),
                "OSS rejects unsupported endpoint form " + input);
        }
        Require(Valid("https://oss.example/a").Validate().Errors[OssSettingsField.Endpoint] == OssSettingsError.EndpointComponents &&
            Valid("http://oss.example").Validate().Errors[OssSettingsField.Endpoint] == OssSettingsError.EndpointScheme,
            "OSS endpoint failures distinguish unsupported components from unsupported transport");
        Require(Valid("https://gateway.internal:9443/").UploadHost() == "https://test-bucket.gateway.internal:9443" &&
            Valid("https://oss.example/path").UploadHost() is null,
            "OSS destination preview reflects bucket-prefixed HTTPS without silently ignoring invalid paths");
    }

    private static void CheckBucketsAndKeys()
    {
        foreach (var bucket in new[] { "abc", "0-1", new string('a', 63) })
            Require((Valid() with { Bucket = bucket }).Validate().IsValid, "OSS accepts valid bucket " + bucket);
        foreach (var bucket in new[] { "ab", new string('a', 64), "Uppercase", "has.dot", "has_underscore", "-leading", "trailing-", "has space", "仓库名称" })
        {
            var validation = (Valid() with { Bucket = bucket }).Validate();
            Require(validation.Errors.TryGetValue(OssSettingsField.Bucket, out var error) && error == OssSettingsError.BucketFormat,
                "OSS reports bucket syntax errors for " + bucket);
        }
        var missing = new OssSettingsDraft(true, " ", null, null, "", "").Validate();
        Require(missing.Errors.Count == 4 && missing.Errors.Values.All(error => error == OssSettingsError.Required),
            "enabled OSS identifies every required field without mutating settings");
        var keyWhitespace = (Valid() with { AccessKeyId = "synthetic id\nvalue" }).Validate();
        Require(keyWhitespace.Errors[OssSettingsField.AccessKeyId] == OssSettingsError.AccessKeyIdWhitespace,
            "OSS rejects whitespace and line breaks in the Authorization key ID");
        var opaque = (Valid() with { AccessKeySecret = " +/opaque synthetic secret= ", Prefix = " /本地 模组/ " }).Validate();
        Require(opaque.IsValid && opaque.Draft.AccessKeySecret == "+/opaque synthetic secret=" && opaque.Draft.Prefix == "/本地 模组/",
            "OSS preserves opaque secret and object-prefix contents without provider-specific length assumptions");
    }

    private static void CheckSaving()
    {
        var config = new AppConfig { Language = "ja", OssEndpoint = "old.example", OssBucket = "old-bucket",
            OssAccessKeyId = "old-synthetic-id", OssAccessKeySecret = "old-synthetic-placeholder", OssEnabled = true };
        var writes = 0;
        Require(!(Valid("https://oss.example/path")).TrySave(config, _ => { writes++; return true; }) && writes == 0 &&
            config.OssEndpoint == "old.example", "invalid OSS settings never reach the persistence callback or live configuration");
        Require(!Valid("https://gateway.internal:9443/").TrySave(config, _ => false) && config.OssEndpoint == "old.example",
            "failed normalized OSS saves preserve the previous endpoint");
        AppConfig? saved = null;
        Require(Valid("https://gateway.internal:9443/").TrySave(config, candidate => { saved = candidate; return true; }) &&
            config.OssEndpoint == "gateway.internal:9443" && saved?.OssEndpoint == "gateway.internal:9443" && config.Language == "ja",
            "OSS saves custom ports consistently to the candidate and live configuration");
        var normalizer = typeof(OssPublisher).GetMethod("NormalizeEndpoint", BindingFlags.Static | BindingFlags.NonPublic)!;
        var consumed = (string)normalizer.Invoke(null, new object[] { config.OssEndpoint! })!;
        Require(new Uri($"https://{config.OssBucket}.{consumed}/archive.zip").Port == 9443,
            "saved HTTPS endpoint ports survive the actual upload consumer's normalizer");

        var disabled = new OssSettingsDraft(false, "unfinished/path", "Invalid Bucket", null, "", "");
        Require(disabled.Validate().IsValid && disabled.TrySave(config, _ => true) && !config.OssEnabled &&
            config.OssEndpoint == "unfinished/path" && config.OssAccessKeyId == "" && config.OssAccessKeySecret == "",
            "OSS can be disabled and stored keys cleared even when endpoint or bucket fields are incomplete");
        Require(!(disabled with { Enabled = true }).Validate().IsValid,
            "re-enabling saved incomplete OSS settings requires correction before saving");
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}

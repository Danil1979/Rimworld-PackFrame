using System;

namespace PackFrame;

internal sealed record OssSettingsDraft(
    bool Enabled, string? Endpoint, string? Bucket, string? Prefix,
    string? AccessKeyId, string? AccessKeySecret)
{
    internal bool TrySave(AppConfig config, Func<AppConfig, bool>? save = null)
    {
        // Keep the live configuration unchanged until the complete candidate is on disk.
        var candidate = config.Copy();
        Apply(candidate);
        if (!(save?.Invoke(candidate) ?? candidate.TrySave())) return false;
        Apply(config);
        return true;
    }

    private void Apply(AppConfig config)
    {
        config.OssEnabled = Enabled;
        config.OssEndpoint = Endpoint;
        config.OssBucket = Bucket;
        config.OssPrefix = Prefix;
        config.OssAccessKeyId = AccessKeyId;
        config.OssAccessKeySecret = AccessKeySecret;
    }
}

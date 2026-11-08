using System;
using System.Collections.Generic;
using System.Linq;

namespace PackFrame;

internal enum OssSettingsField { Endpoint, Bucket, AccessKeyId, AccessKeySecret }
internal enum OssSettingsError { Required, EndpointFormat, EndpointScheme, EndpointComponents, EndpointHost, BucketFormat, AccessKeyIdWhitespace }

internal sealed record OssSettingsValidation(OssSettingsDraft Draft,
    IReadOnlyDictionary<OssSettingsField, OssSettingsError> Errors)
{
    internal bool IsValid => Errors.Count == 0;
}

internal sealed record OssSettingsDraft(
    bool Enabled, string? Endpoint, string? Bucket, string? Prefix,
    string? AccessKeyId, string? AccessKeySecret)
{
    internal OssSettingsValidation Validate()
    {
        var draft = this with
        {
            Endpoint = Endpoint?.Trim(), Bucket = Bucket?.Trim(), Prefix = Prefix?.Trim(),
            AccessKeyId = AccessKeyId?.Trim(), AccessKeySecret = AccessKeySecret?.Trim()
        };
        var errors = new Dictionary<OssSettingsField, OssSettingsError>();
        // Disabling upload must remain possible even with incomplete or legacy settings.
        if (!Enabled) return new(draft, errors);

        if (!TryNormalizeEndpoint(draft.Endpoint, out var endpoint, out var endpointError))
            errors[OssSettingsField.Endpoint] = endpointError;
        else draft = draft with { Endpoint = endpoint };
        if (string.IsNullOrWhiteSpace(draft.Bucket)) errors[OssSettingsField.Bucket] = OssSettingsError.Required;
        else if (!IsBucketName(draft.Bucket)) errors[OssSettingsField.Bucket] = OssSettingsError.BucketFormat;
        if (string.IsNullOrWhiteSpace(draft.AccessKeyId)) errors[OssSettingsField.AccessKeyId] = OssSettingsError.Required;
        else if (draft.AccessKeyId.Any(char.IsWhiteSpace) || draft.AccessKeyId.Any(char.IsControl))
            errors[OssSettingsField.AccessKeyId] = OssSettingsError.AccessKeyIdWhitespace;
        if (string.IsNullOrWhiteSpace(draft.AccessKeySecret)) errors[OssSettingsField.AccessKeySecret] = OssSettingsError.Required;
        return new(draft, errors);
    }

    internal string? UploadHost()
    {
        return TryNormalizeEndpoint(Endpoint, out var endpoint, out _) && IsBucketName(Bucket?.Trim())
            ? $"https://{Bucket!.Trim()}.{endpoint}" : null;
    }

    private static bool IsBucketName(string? bucket) => bucket is { Length: >= 3 and <= 63 } &&
        IsBucketLetter(bucket[0]) && IsBucketLetter(bucket[^1]) &&
        bucket.All(character => IsBucketLetter(character) || character == '-');

    private static bool IsBucketLetter(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static bool TryNormalizeEndpoint(string? endpoint, out string? normalized, out OssSettingsError error)
    {
        normalized = null;
        error = OssSettingsError.Required;
        var value = endpoint?.Trim();
        if (string.IsNullOrEmpty(value)) return false;
        error = OssSettingsError.EndpointFormat;
        if (value.Any(char.IsWhiteSpace) || value.Contains('\\')) return false;
        if (value.Contains("://", StringComparison.Ordinal) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            error = OssSettingsError.EndpointScheme;
            return false;
        }
        if (!Uri.TryCreate(value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value,
            UriKind.Absolute, out var uri) || uri.Port <= 0) return false;
        if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
        {
            error = OssSettingsError.EndpointComponents;
            return false;
        }
        error = OssSettingsError.EndpointHost;
        if (uri.HostNameType != UriHostNameType.Dns) return false;
        string host;
        try { host = uri.IdnHost.TrimEnd('.'); }
        catch (UriFormatException) { return false; }
        if (host.Length is 0 or > 253 || host.Split('.').Any(label => label.Length is 0 or > 63 ||
            !char.IsAsciiLetterOrDigit(label[0]) || !char.IsAsciiLetterOrDigit(label[^1]) ||
            label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))) return false;
        // UploadAsync prepends the bucket and always uses HTTPS. Save the authority
        // without a scheme so its existing URL normalizer does not discard a custom port.
        normalized = host + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port);
        return true;
    }

    internal bool TrySave(AppConfig config, Func<AppConfig, bool>? save = null)
    {
        var validation = Validate();
        if (!validation.IsValid) return false;
        // Keep the live configuration unchanged until the complete candidate is on disk.
        var candidate = config.Copy();
        validation.Draft.Apply(candidate);
        if (!(save?.Invoke(candidate) ?? candidate.TrySave())) return false;
        validation.Draft.Apply(config);
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

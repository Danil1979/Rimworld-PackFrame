using System;
using System.Diagnostics;
using System.Linq;

namespace PackFrame;

// A display-only row keeps UI strings and link validation out of persisted history.
public sealed record ModStatusDisplayRow(
    string Label, string Author, string PackageId, string Link, string Status,
    string LastMissingAt, string? Language)
{
    public string AuthorLabel => Localization.FieldAuthor(Language) + ":";
    public string PackageLabel => Localization.FieldPackageId(Language) + ":";

    internal static string SearchPlaceholder(string? language, bool history) => history
        ? OperationText.Choose(language,
            "Search name, author, package ID, link or date…", "搜索名称、作者、包 ID、链接或日期…",
            "搜尋名稱、作者、套件 ID、連結或日期…", "名前・作者・パッケージ ID・リンク・日付を検索…")
        : OperationText.Choose(language,
            "Search name, author, package ID, link or status…", "搜索名称、作者、包 ID、链接或状态…",
            "搜尋名稱、作者、套件 ID、連結或狀態…", "名前・作者・パッケージ ID・リンク・状態を検索…");

    internal static string EmptyAvailabilityText(string? language) => OperationText.Choose(language,
        "No mod availability entries to display.", "没有可显示的模组可用性记录。",
        "沒有可顯示的模組可用性記錄。", "表示する MOD の状態情報はありません。");

    public bool CanOpenLink => ModStatusLink.TryGetUri(Link, out _);
    public string LinkAction => CanOpenLink
        ? OperationText.Choose(Language, "Open link ↗", "打开链接 ↗", "開啟連結 ↗", "リンクを開く ↗")
        : OperationText.Choose(Language, "No web link", "无网页链接", "無網頁連結", "Web リンクなし");
    public string LinkAccessibleName => LinkAction + ": " + Label;

    internal static ModStatusDisplayRow From(MissingModRow row, string? language) => new(
        row.Label, row.Author, row.PackageId, row.Link,
        string.IsNullOrWhiteSpace(row.Status) ? Localization.WorkshopStatus(language, row.Availability) : row.Status,
        "", language);
    internal static ModStatusDisplayRow From(MissingHistoryEntry row, string? language) => new(
        row.Label, row.Author, row.PackageId, row.Link, "", row.LastMissingAt, language);
}

internal static class ModStatusLink
{
    internal static bool TryGetUri(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || value.Contains('\\') ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(parsed.Host) || !string.IsNullOrEmpty(parsed.UserInfo)) return false;
        uri = parsed;
        return true;
    }

    internal static bool TryOpen(string? value, out Exception? error, Action<Uri>? open = null)
    {
        error = null;
        if (!TryGetUri(value, out var uri)) return false;
        try
        {
            if (open is not null) open(uri!);
            else Process.Start(new ProcessStartInfo { FileName = uri!.AbsoluteUri, UseShellExecute = true });
            return true;
        }
        catch (Exception exception) { error = exception; return false; }
    }

    internal static string FailureText(string? language) => OperationText.Choose(language,
        "The link could not be opened. Copy the displayed web address into your browser, or check your default browser settings.",
        "无法打开链接。请将显示的网址复制到浏览器，或检查默认浏览器设置。",
        "無法開啟連結。請將顯示的網址複製到瀏覽器，或檢查預設瀏覽器設定。",
        "リンクを開けませんでした。表示中の URL をブラウザーにコピーするか、既定のブラウザー設定を確認してください。");
}

using System;

namespace PackFrame;

internal static class ModpackFooterStatus
{
    internal static string SelectedHint(string language, bool canInstall) => canInstall
        ? OperationText.Choose(language,
            "Review the selected pack, then choose an installation option below. Installation requires a connection.",
            "查看所选合集，然后选择下方的安装方式。安装需要联网。",
            "查看所選合集，然後選擇下方的安裝方式。安裝需要連線。",
            "選択したパックを確認し、下のインストール方法を選んでください。インストールには接続が必要です。")
        : OperationText.Choose(language,
            "Review the selected pack. No installable mod sources are listed.",
            "查看所选合集。此合集未列出可安装的模组来源。",
            "查看所選合集。此合集未列出可安裝的模組來源。",
            "選択したパックを確認してください。インストール可能な MOD ソースがありません。");

    internal static bool IsSelectionHint(string? status, string language) =>
        status == SelectedHint(language, true) || status == SelectedHint(language, false);

    // A late detail response must not replace a newer validation error or operation result.
    internal static bool CanReplace(string? current, string? expected, bool preserveStatus, bool busy) =>
        !preserveStatus && !busy && string.Equals(current, expected, StringComparison.Ordinal);
}

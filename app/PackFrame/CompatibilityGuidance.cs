using System.Collections.Generic;

namespace PackFrame;

internal sealed record CompatibilityIssue(string Title, string Body);

internal static class CompatibilityGuidance
{
    internal static string Heading(string? language) => OperationText.Choose(language,
        "Compatibility issues", "兼容性问题", "相容性問題", "互換性の問題");
    internal static string Review(string? language) => OperationText.Choose(language,
        "Review compatibility", "查看兼容性问题", "檢視相容性問題", "互換性の問題を確認");
    internal static string Apply(string? language) => OperationText.Choose(language,
        "Apply order suggestion", "应用排序建议", "套用排序建議", "順序の提案を適用");
    internal static string Note(string? language) => OperationText.Choose(language,
        "Order edit only. Save to apply; Undo to restore.",
        "仅调整顺序。保存后生效，可撤销。", "僅調整順序。儲存後生效，可復原。", "順序のみ変更。保存で反映、元に戻すで復元。");
    internal static string Help(string? language) => OperationText.Choose(language,
        "Moves only this mod after the listed requirements. Save applies the edit; Undo restores it. Missing prerequisites and incompatibilities need separate attention.",
        "仅将此模组移至所列要求之后。保存后生效，可撤销。缺失前置和不兼容问题需另行处理。",
        "僅將此模組移至所列要求之後。儲存後生效，可復原。缺少前置與不相容問題需另行處理。",
        "この MOD のみを記載された MOD の後へ移動します。保存で反映、元に戻すで復元できます。不足する前提 MOD と非互換性は別途対応が必要です。");

    internal static IReadOnlyList<CompatibilityIssue> Issues(ModInfo mod, string? language)
    {
        var issues = new List<CompatibilityIssue>();
        if (mod.SortOrderAfterNames.Count > 0)
            issues.Add(new(OperationText.Choose(language, "Load order", "加载顺序", "載入順序", "読み込み順"),
                Localization.SortAfterTooltip(language, string.Join(", ", mod.SortOrderAfterNames))));
        if (mod.MissingDependencyNames.Count > 0)
            issues.Add(new(OperationText.Choose(language, "Missing prerequisites", "缺失前置", "缺少前置", "不足する前提 MOD"),
                string.Join("\n", mod.MissingDependencyNames) + "\n" + OperationText.Choose(language,
                    "Enable or install the required mods before playing. Changing load order does not supply them.",
                    "游玩前请启用或安装所需模组。调整顺序不会补齐这些模组。",
                    "遊玩前請啟用或安裝所需模組。調整順序不會補齊這些模組。",
                    "プレイ前に必要な MOD を有効化またはインストールしてください。順序変更では追加されません。")));
        if (mod.IncompatibleNames.Count > 0)
            issues.Add(new(OperationText.Choose(language, "Incompatible mods", "不兼容模组", "不相容模組", "非互換 MOD"),
                string.Join("\n", mod.IncompatibleNames) + "\n" + OperationText.Choose(language,
                    "Review these mods together and choose which to keep enabled. Reordering does not resolve incompatibility.",
                    "请一并检查这些模组，并选择保留启用的模组。重新排序无法解决不兼容。",
                    "請一併檢查這些模組，並選擇保留啟用的模組。重新排序無法解決不相容。",
                    "併用する MOD を確認し、有効にするものを選択してください。並べ替えでは非互換性は解消しません。")));
        if (mod.VersionMismatch)
            issues.Add(new(OperationText.Choose(language, "Game version", "游戏版本", "遊戲版本", "ゲームバージョン"),
                OperationText.Choose(language, "This mod does not declare support for the selected game version. Check its supported versions and author guidance.",
                    "此模组未声明支持所选游戏版本。请检查支持版本及作者说明。", "此模組未聲明支援所選遊戲版本。請檢查支援版本及作者說明。",
                    "選択したゲームバージョンへの対応が宣言されていません。対応バージョンと作者の説明を確認してください。")));
        if (mod.IsDuplicate)
            issues.Add(new(OperationText.Choose(language, "Duplicate package", "重复包标识", "重複套件識別碼", "重複パッケージ"),
                OperationText.Choose(language, "Multiple installed mods use this package ID. Check Package & location to identify this copy before changing files.",
                    "多个已安装模组使用此包标识。修改文件前，请通过“包标识与位置”确认当前副本。",
                    "多個已安裝模組使用此套件識別碼。修改檔案前，請透過「套件與位置」確認目前副本。",
                    "複数の MOD が同じパッケージ ID を使用しています。ファイル変更前に「パッケージと場所」で対象を確認してください。")));
        return issues;
    }
}

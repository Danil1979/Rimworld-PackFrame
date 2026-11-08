using System.Collections.Generic;
using System.Linq;

namespace PackFrame;

internal sealed record LanguageChoice(string Code, string ControlName, string NativeName, bool IsCurrent, string CurrentLabel)
{
    internal string AccessibleName => IsCurrent ? NativeName + " · " + CurrentLabel : NativeName;
    internal static string Title(string? language) => OperationText.Choose(language,
        "Choose language", "选择语言", "選擇語言", "言語を選択");
    internal static string SaveFailure(string? language) => OperationText.Choose(language,
        "Could not save language. Check write access to the PackFrame folder, then choose again.",
        "无法保存语言。请检查 PackFrame 文件夹的写入权限，然后重新选择。",
        "無法儲存語言。請檢查 PackFrame 資料夾的寫入權限，然後重新選擇。",
        "言語を保存できません。PackFrame フォルダーの書き込み権限を確認して、再度選択してください。");
    internal static IReadOnlyList<LanguageChoice> Options(string? currentLanguage)
    {
        var label = OperationText.Choose(currentLanguage, "Current language", "当前语言", "目前語言", "現在の言語");
        return new[] {
            ("en", "EnglishButton", "English"), ("zh-Hans", "HansButton", "简体中文"),
            ("zh-Hant", "HantButton", "繁體中文"), ("ja", "JapaneseButton", "日本語")
        }.Select(x => new LanguageChoice(x.Item1, x.Item2, x.Item3, x.Item1 == currentLanguage, label)).ToArray();
    }
}

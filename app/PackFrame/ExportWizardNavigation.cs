using System;

namespace PackFrame;

internal sealed record ExportWizardNavigation(string Step, string Hint, string Next, string Exit, string ExitHint, bool ShowExit)
{
    internal const int PageCount = 5;

    internal static ExportWizardNavigation Describe(int page, string? language)
    {
        if (page is < 1 or > PageCount) throw new ArgumentOutOfRangeException(nameof(page));
        string T(string en, string hans, string hant, string ja) => OperationText.Choose(language, en, hans, hant, ja);
        var step = T($"Step {page} of {PageCount}", $"第 {page} 步，共 {PageCount} 步", $"第 {page} 步，共 {PageCount} 步", $"ステップ {page} / {PageCount}");
        var hint = page switch
        {
            1 => T("Save these details in the local folder. Continue only if you want to publish or submit the pack.",
                "将这些信息保存到本地文件夹。只有需要发布或提交合集时，才需继续。",
                "將這些資訊儲存到本機資料夾。只有需要發布或提交合集時，才需繼續。",
                "この情報をローカルフォルダーに保存します。公開や掲載申請を行う場合のみ次へ進んでください。"),
            2 => T("Optional: create or update a Steam collection here. Next skips this action.",
                "可选：在此创建或更新Steam 合集。下一步会跳过此操作。",
                "選用：在此建立或更新Steam 合集。下一步會略過此操作。",
                "任意：ここでSteam コレクションを作成・更新できます。次へ進むだけでは実行されません。"),
            3 => T("Optional: use Upload to publish on Gitee. Next only moves to GitHub.",
                "可选：点击上传可发布到 Gitee。下一步只会转到 GitHub 页面。",
                "選用：點擊上傳可發布到 Gitee。下一步只會前往 GitHub 頁面。",
                "任意：アップロードで Gitee に公開します。次へ進むだけでは公開されません。"),
            4 => T("Optional: use Upload to publish on GitHub. Next only moves to catalog review.",
                "可选：点击上传可发布到 GitHub。下一步只会转到合集列表审核页面。",
                "選用：點擊上傳可發布到 GitHub。下一步只會前往合集列表審核頁面。",
                "任意：アップロードで GitHub に公開します。次へ進むだけでは公開されません。"),
            _ => T("Optional: use Submit to request a catalog listing. Close does not submit.",
                "可选：点击提交可申请加入合集列表。关闭不会提交。",
                "選用：點擊提交可申請加入合集列表。關閉不會提交。",
                "任意：送信すると一覧への掲載を申請します。閉じるだけでは送信されません。")
        };
        var next = page switch
        {
            1 => T("Save and continue", "保存并继续", "儲存並繼續", "保存して次へ"),
            2 => T("Next: Gitee", "下一步：Gitee", "下一步：Gitee", "次へ：Gitee"),
            3 => T("Next: GitHub", "下一步：GitHub", "下一步：GitHub", "次へ：GitHub"),
            4 => T("Next: catalog", "下一步：合集列表", "下一步：合集列表", "次へ：掲載申請"),
            _ => Localization.CloseButton(language)
        };
        var exit = page == 1
            ? T("Save and close", "保存并关闭", "儲存並關閉", "保存して閉じる")
            : Localization.CloseButton(language);
        var exitHint = page == 1
            ? T("Save local details and close without publishing.", "保存本地信息并关闭，不会发布。", "儲存本機資訊並關閉，不會發布。", "情報をローカルに保存して閉じます。公開は行いません。")
            : T("Keep completed files and uploads. Unsent form fields are not saved.", "保留已完成的文件和上传。未提交的表单内容不会保存。", "保留已完成的檔案和上傳。未提交的表單內容不會儲存。", "作成済みのファイルとアップロードは保持されます。未送信の入力内容は保存されません。");
        return new(step, hint, next, exit, exitHint, page < PageCount);
    }
}

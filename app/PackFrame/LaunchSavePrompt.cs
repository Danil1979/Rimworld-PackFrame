using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PackFrame;

internal static class LaunchSavePrompt
{
    internal static string ConfirmLabel(string? language) => OperationText.Choose(language,
        "Save all and launch", "全部保存并启动", "全部儲存並啟動", "すべて保存して起動");

    internal static string CancelLabel(string? language) => OperationText.Choose(language,
        "Return to editing", "返回编辑", "返回編輯", "編集に戻る");

    internal static string Message(string? language, string target, IReadOnlyList<PendingInstanceRow> rows)
    {
        var heading = OperationText.Choose(language,
            $"Save changes before launching {target}?", $"启动 {target} 前保存修改？",
            $"啟動 {target} 前儲存修改？", $"{target} を起動する前に変更を保存しますか？");
        var scope = OperationText.Choose(language,
            $"This saves all {rows.Count} edited instances listed below, then launches only {target}. Other instances may have unsaved edits even if they are not currently visible.",
            $"将保存下列全部 {rows.Count} 个已编辑实例，然后仅启动 {target}。当前未显示的实例也可能有未保存的修改。",
            $"將儲存下列全部 {rows.Count} 個已編輯實例，然後僅啟動 {target}。目前未顯示的實例也可能有未儲存的修改。",
            $"下記の編集した {rows.Count} 個のインスタンスをすべて保存し、{target} のみを起動します。表示されていないインスタンスにも未保存の編集がある場合があります。");
        var cancel = OperationText.Choose(language,
            "Return to editing keeps your unsaved edits without saving or launching.",
            "返回编辑会保留未保存的修改，不会保存或启动。",
            "返回編輯會保留未儲存的修改，不會儲存或啟動。",
            "「編集に戻る」は未保存の編集を保持し、保存も起動もしません。");
        return heading + "\n\n" + scope + "\n\n" +
            string.Join("\n\n", rows.Select(row => row.Name + "\n" + row.ConfigDirectoryPath)) + "\n\n" + cancel;
    }

    // Saving must succeed completely before launch can be allowed. Keep the gate
    // independent of UI and process creation so cancellation/failure are testable.
    internal static async Task<bool> ResolveAsync(Func<Task<bool>> confirm, Func<bool> save, Func<bool> hasPending)
    {
        if (!await confirm()) return false;
        return save() && !hasPending();
    }
}

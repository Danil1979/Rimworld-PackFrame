using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PackFrame;

public sealed record PendingInstanceRow(string Name, string ConfigDirectoryPath);

// Presentation only: keep the banner independent of names, and retain each
// configuration path in the review so equally named instances remain distinct.
internal static class PendingInstanceSummary
{
    internal static IReadOnlyList<PendingInstanceRow> Rows(
        IEnumerable<string> pending, IEnumerable<ManagedInstance> instances, IEnumerable<string>? blocked = null)
    {
        var known = instances.Where(instance => !string.IsNullOrWhiteSpace(instance.ConfigDirectoryPath))
            .GroupBy(instance => Path.GetFullPath(instance.ConfigDirectoryPath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var blockedPaths = new HashSet<string>(blocked ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return pending.Select(path =>
        {
            var name = known.TryGetValue(path, out var instance) ? instance.DisplayName
                : Path.GetFileName(Path.GetDirectoryName(path)) ?? path;
            if (blockedPaths.Contains(path) && !name.StartsWith("⚠ ", StringComparison.Ordinal)) name = "⚠ " + name;
            return new PendingInstanceRow(name, path);
        }).ToList();
    }

    internal static string Caption(string? language, int count) => count == 0
        ? OperationText.Choose(language, "No unsaved mod-list changes", "模组列表没有未保存的修改",
            "模組清單沒有未儲存的修改", "MOD リストに未保存の変更はありません")
        : OperationText.Choose(language,
            $"Unsaved changes in {count} {(count == 1 ? "instance" : "instances")} · Save applies to all edited instances",
            $"{count} 个实例有未保存的修改 · 保存将应用于所有已编辑实例",
            $"{count} 個實例有未儲存的修改 · 儲存將套用至所有已編輯實例",
            $"{count} 個のインスタンスに未保存の変更 · 保存は編集した全インスタンスに適用");

    internal static string BlockedCaption(string? language, int count, int blocked) => OperationText.Choose(language,
        $"Unsaved edits kept in {count} {(count == 1 ? "instance" : "instances")} · Save blocked for {blocked}",
        $"已保留 {count} 个实例的未保存修改 · {blocked} 个实例无法保存",
        $"已保留 {count} 個實例的未儲存修改 · {blocked} 個實例無法儲存",
        $"{count} 個の未保存の編集を保持 · {blocked} 個のインスタンスは保存不可");

    internal static string RecoveryHint(string? language) => OperationText.Choose(language,
        "Keep this window open. Repair the unreadable files, then Refresh to restore your edits. Review instances shows their folders.",
        "请保持窗口打开。修复无法读取的文件后点击“刷新”以恢复修改。“查看实例”可查看文件夹。",
        "請保持視窗開啟。修復無法讀取的檔案後點選「重新整理」以恢復修改。「檢視實例」可查看資料夾。",
        "このウィンドウを開いたまま、読み取れないファイルを修復して「更新」してください。編集が復元されます。「インスタンスを確認」でフォルダーを確認できます。");

    internal static string SaveBlocked(string? language) => OperationText.Choose(language,
        "Save blocked. Unsaved edits kept; see the recovery notice.",
        "无法保存。未保存的修改已保留，请查看恢复提示。",
        "無法儲存。未儲存的修改已保留，請查看復原提示。",
        "保存できません。未保存の編集は保持されています。復旧手順を確認してください。");

    internal static string ReviewLabel(string? language) => OperationText.Choose(language,
        "Review instances", "查看实例", "檢視實例", "インスタンスを確認");
}

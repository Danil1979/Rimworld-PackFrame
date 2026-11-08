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
        IEnumerable<string> pending, IEnumerable<ManagedInstance> instances)
    {
        var known = instances.Where(instance => !string.IsNullOrWhiteSpace(instance.ConfigDirectoryPath))
            .GroupBy(instance => Path.GetFullPath(instance.ConfigDirectoryPath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return pending.Select(path => new PendingInstanceRow(
            known.TryGetValue(path, out var instance) ? instance.DisplayName
                : Path.GetFileName(Path.GetDirectoryName(path)) ?? path, path)).ToList();
    }

    internal static string Caption(string? language, int count) => count == 0
        ? OperationText.Choose(language, "No unsaved mod-list changes", "模组列表没有未保存的修改",
            "模組清單沒有未儲存的修改", "MOD リストに未保存の変更はありません")
        : OperationText.Choose(language,
            $"Unsaved changes in {count} {(count == 1 ? "instance" : "instances")} · Save applies to all edited instances",
            $"{count} 个实例有未保存的修改 · 保存将应用于所有已编辑实例",
            $"{count} 個實例有未儲存的修改 · 儲存將套用至所有已編輯實例",
            $"{count} 個のインスタンスに未保存の変更 · 保存は編集した全インスタンスに適用");

    internal static string ReviewLabel(string? language) => OperationText.Choose(language,
        "Review instances", "查看实例", "檢視實例", "インスタンスを確認");
}

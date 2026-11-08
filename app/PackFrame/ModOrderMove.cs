using System;
using System.Collections.Generic;
using System.Linq;

namespace PackFrame;

internal static class ModOrderMove
{
    internal static bool CanMove(IReadOnlyList<string> order, IReadOnlyCollection<string> ids, int direction)
    {
        if (direction is not (-1 or 1)) return false;
        var selected = new HashSet<string>(ids.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < order.Count; index++)
        {
            var neighbor = index + direction;
            if (selected.Contains(order[index]) && neighbor >= 0 && neighbor < order.Count && !selected.Contains(order[neighbor])) return true;
        }
        return false;
    }

    // Move each selected block by one full-order position, preserving both selected
    // and unselected relative order, including missing entries and duplicates.
    internal static List<string> Apply(IReadOnlyList<string> order, IReadOnlyCollection<string> ids, int direction)
    {
        var result = order.ToList();
        if (direction is not (-1 or 1)) return result;
        var selected = new HashSet<string>(ids.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
        for (var i = direction < 0 ? 1 : result.Count - 2; direction < 0 ? i < result.Count : i >= 0; i -= direction)
        {
            var neighbor = i + direction;
            if (selected.Contains(result[i]) && !selected.Contains(result[neighbor]))
                (result[i], result[neighbor]) = (result[neighbor], result[i]);
        }
        return result;
    }

    internal static string ButtonLabel(string? language, int direction) => direction < 0
        ? OperationText.Choose(language, "↑ Move up", "↑ 上移", "↑ 上移", "↑ 上へ")
        : OperationText.Choose(language, "↓ Move down", "↓ 下移", "↓ 下移", "↓ 下へ");

    internal static string Label(string? language, int direction) => direction < 0
        ? OperationText.Choose(language, "Move up in full load order", "在完整加载顺序中上移", "在完整載入順序中上移", "読み込み順序全体で上へ移動")
        : OperationText.Choose(language, "Move down in full load order", "在完整加载顺序中下移", "在完整載入順序中下移", "読み込み順序全体で下へ移動");

    internal static string Help(string? language) => OperationText.Choose(language,
        "Alt+↑ / Alt+↓ moves selected mods one position in the full load order, including hidden and missing entries. Save applies the change; Undo restores it.",
        "Alt+↑ / Alt+↓ 将所选模组在完整加载顺序中移动一位，包括隐藏和缺失的条目。保存后生效，可撤销。",
        "Alt+↑ / Alt+↓ 將所選模組在完整載入順序中移動一位，包括隱藏和缺失的項目。儲存後生效，可復原。",
        "Alt+↑ / Alt+↓ で選択した MOD を読み込み順序全体の1つ前後へ移動します。非表示・不足項目も含みます。保存で適用し、元に戻す操作で復元できます。");
}

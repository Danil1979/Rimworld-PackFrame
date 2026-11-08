using System;
using System.Collections.Generic;
using System.Linq;

namespace PackFrame;

internal static class InstanceCreationHistory
{
    // Call only after the new profile has been published successfully. The new
    // profile is an independent saved copy; Undo restores the scratch list only.
    internal static List<InstanceSnapshot> ClearScratchAfterCreate(ManagedInstance? source)
    {
        if (source?.IsScratch != true || source.ActivePackageIds.Count == 0)
            return new List<InstanceSnapshot>();
        var snapshot = new InstanceSnapshot(source.FolderName, source.ActivePackageIds.ToList());
        InstanceStore.StageActiveMods(source, Array.Empty<string>());
        return new List<InstanceSnapshot> { snapshot };
    }

    internal static string SelectorName(ManagedInstance instance, string? language) =>
        instance.IsScratch ? ScratchName(language) : instance.DisplayName;

    internal static string ScratchName(string? language) => OperationText.Choose(language,
        "Scratch list", "临时列表", "暫存清單", "一時リスト");

    internal static string ScratchHint(string? language) => OperationText.Choose(language,
        "Temporary mod list. Use Instance → Create instance from this list to keep it. Save changes does not save this list.",
        "临时模组列表。选择“实例 → 从此列表创建实例”以保留它。“保存更改”不会保存此列表。",
        "暫存模組清單。選擇「執行個體 → 從此清單建立執行個體」以保留它。「儲存變更」不會儲存此清單。",
        "一時的な MOD リストです。「インスタンス → このリストからインスタンスを作成」で保存できます。「変更を保存」では保存されません。");

    internal static string CreateFromScratch(string? language) => OperationText.Choose(language,
        "Create instance from this list", "从此列表创建实例", "從此清單建立執行個體", "このリストからインスタンスを作成");

    internal static string ScratchCreated(string? language, string name) => OperationText.Choose(language,
        $"Created {name}. Undo restores the scratch list and keeps the new instance.",
        $"已创建 {name}。撤销会恢复临时列表，并保留新实例。",
        $"已建立 {name}。復原會還原暫存清單，並保留新的執行個體。",
        $"{name} を作成しました。元に戻すと一時リストが復元され、新しいインスタンスは保持されます。");
}

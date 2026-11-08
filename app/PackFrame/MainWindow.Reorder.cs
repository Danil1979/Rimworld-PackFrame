using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class MainWindow
{
    private bool CanReorderSelection(int column, int direction)
    {
        if (column is not (1 or 2) || _loadingMods || _painting || _customDragActive || _instanceWriteInProgress) return false;
        var list = PaneWorkflow(column).List;
        var instance = GetInstanceForColumn(column);
        return list.IsEffectivelyVisible && instance is not null && !instance.IsDamaged &&
            ModOrderMove.CanMove(instance.ActivePackageIds, GetSelectedMods(list).Select(mod => mod.PackageId).ToArray(), direction);
    }

    private void UpdateReorderActions(int column)
    {
        var instance = GetInstanceForColumn(column);
        foreach (var direction in new[] { -1, 1 })
        {
            var button = Find<Button>($"Move{(direction < 0 ? "Up" : "Down")}{column}");
            var shortcut = direction < 0 ? "Alt+↑" : "Alt+↓";
            var name = $"{ModOrderMove.Label(_config.Language, direction)} ({shortcut}): {instance?.DisplayName}";
            AutomationProperties.SetName(button, name);
            AutomationProperties.SetHelpText(button, ModOrderMove.Help(_config.Language));
            ToolTip.SetTip(button, name + "\n" + ModOrderMove.Help(_config.Language));
            button.IsEnabled = CanReorderSelection(column, direction);
        }
    }

    private void OnReorderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        var route = tag.Split(':');
        if (route.Length == 2 && int.TryParse(route[0], out var column) && int.TryParse(route[1], out var direction))
            ReorderSelection(column, direction);
    }

    private void ReorderSelection(int column, int direction)
    {
        if (!CanReorderSelection(column, direction)) return;
        var list = PaneWorkflow(column).List;
        var instance = GetInstanceForColumn(column)!;
        var ids = GetSelectedMods(list).Select(mod => mod.PackageId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var before = instance.ActivePackageIds.ToList();
        var order = ModOrderMove.Apply(before, ids, direction);
        if (ActiveIdsEqual(before, order)) return;
        try
        {
            InstanceStore.StageActiveMods(instance, order);
            PushUndo(new List<InstanceSnapshot> { new(instance.FolderName, before) }, ids, column, column);
            RefreshManager(false);
            // Synchronous restoration keeps repeated shortcut presses on the same
            // selection and invalidates queued old scroll offsets before the next key.
            SelectMovedMods(column, ids);
            var selected = direction < 0 ? list.SelectedItems?.OfType<ModInfo>().FirstOrDefault()
                : list.SelectedItems?.OfType<ModInfo>().LastOrDefault();
            if (selected is not null) list.ScrollIntoView(selected);
            UpdateSelectionAction();
        }
        catch (Exception exception)
        {
            AppLog.Error("move selection in load order", exception);
            ShowToast(exception.Message);
        }
    }
}

using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace PackFrame;

public partial class MainWindow
{
    private readonly Border _sameListBadge1;
    private readonly Border _sameListBadge2;
    private readonly TextBlock _launchTargetText;
    private readonly TextBlock _dragGhostAction;
    private readonly Border _dropTargetOutline;

    private static bool IsSameInstance(ManagedInstance? first, ManagedInstance? second) =>
        first is not null && second is not null &&
        string.Equals(first.DirectoryPath, second.DirectoryPath, StringComparison.OrdinalIgnoreCase);

    private void UpdatePaneHeaders()
    {
        var first = GetSelectedInstance(0);
        var second = GetSelectedInstance(1);
        var sameList = IsSameInstance(first, second);
        foreach (var badge in new[] { _sameListBadge1, _sameListBadge2 })
        {
            badge.IsVisible = sameList;
            ((TextBlock)badge.Child!).Text = Localization.SameList(_config.Language);
            ToolTip.SetTip(badge, Localization.SameListHint(_config.Language));
        }
        ToolTip.SetTip(_instanceCombo1, first?.LoadError ?? first?.DisplayName);
        ToolTip.SetTip(_instanceCombo2, second?.LoadError ?? second?.DisplayName);
    }

    private void UpdateLaunchTarget(ManagedInstance? instance)
    {
        var text = string.IsNullOrWhiteSpace(instance?.DisplayName)
            ? Localization.LaunchGame(_config.Language)
            : Localization.LaunchInstance(_config.Language, instance.DisplayName);
        _launchTargetText.Text = text;
        ToolTip.SetTip(_launchButton, text);
        AutomationProperties.SetName(_launchButton, text);
    }

    private bool UpdateModDragFeedback(ListBox? target)
    {
        ClearModDragFeedback();
        if (target is null || _dragPayload is null)
        {
            return false;
        }

        var source = GetInstanceForColumn(_dragPayload.SourceColumn);
        var column = GetListColumn(target);
        var destination = GetInstanceForColumn(column);
        if (source?.IsDamaged == true || destination?.IsDamaged == true ||
            (column == 0 ? source is null : destination is null))
        {
            return false;
        }

        var top = target.TranslatePoint(default, _dragOverlay);
        if (top is null)
        {
            return false;
        }

        _dragGhostAction.Text = column == 0
            ? Localization.DragDisableFrom(_config.Language, source!.DisplayName)
            : IsSameInstance(source, destination)
                ? Localization.DragMoveHere(_config.Language)
                : string.IsNullOrWhiteSpace(destination!.DisplayName)
                    ? Localization.DragAddHere(_config.Language)
                    : Localization.DragAddTo(_config.Language, destination.DisplayName);
        _dragGhostAction.IsVisible = true;
        _dropTargetOutline.Width = target.Bounds.Width;
        _dropTargetOutline.Height = target.Bounds.Height;
        Canvas.SetLeft(_dropTargetOutline, top.Value.X);
        Canvas.SetTop(_dropTargetOutline, top.Value.Y);
        _dropTargetOutline.IsVisible = true;
        return true;
    }

    private void ClearModDragFeedback()
    {
        _dropTargetOutline.IsVisible = false;
        _dragGhostAction.IsVisible = false;
    }

    private void EndModDrag()
    {
        StopAutoScroll();
        HideDragGhost();
        _insertionLine.IsVisible = false;
        _insertionLine.Opacity = 1;
        _customDragActive = false;
        _dragPayload = null;
        _dragSourceList = null;
    }
}

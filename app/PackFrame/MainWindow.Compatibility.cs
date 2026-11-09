using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace PackFrame;

public partial class MainWindow
{
    private void UpdateCompatibilityDetails(ModInfo? mod)
    {
        var card = Find<Border>("CompatibilityCard");
        var content = Find<StackPanel>("CompatibilityContent");
        content.Children.Clear();
        var issues = mod is null ? Array.Empty<CompatibilityIssue>() : CompatibilityGuidance.Issues(mod, _config.Language);
        card.IsVisible = issues.Count > 0;
        if (mod is null || issues.Count == 0) return;
        var column = mod.BadgeColumn > 0 ? mod.BadgeColumn : mod.HasLoadOrder && GetCommandList() is { } list ? GetListColumn(list) : 0;
        var instance = column > 0 ? GetInstanceForColumn(column) : null;
        var scope = instance?.DisplayName ?? WorkflowText("Available mods", "可用模组", "可用模組", "利用可能な MOD");
        var title = CompatibilityGuidance.Heading(_config.Language);
        AutomationProperties.SetName(card, title + ": " + mod.Name + " · " + scope);
        content.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold,
            Foreground = ThemeService.PrimaryTextBrush, TextWrapping = TextWrapping.Wrap });
        var context = new TextBlock { Text = scope, FontSize = 12,
            Foreground = ThemeService.SecondaryTextBrush, TextWrapping = TextWrapping.Wrap };
        ToolTip.SetTip(context, instance?.ConfigDirectoryPath);
        content.Children.Add(context);
        foreach (var issue in issues)
        {
            content.Children.Add(new TextBlock { Text = issue.Title, Margin = new Thickness(0, 5, 0, 0),
                FontWeight = FontWeight.SemiBold, Foreground = ThemeService.PrimaryTextBrush, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new SelectableTextBlock { Text = issue.Body,
                Foreground = ThemeService.PrimaryTextBrush, TextWrapping = TextWrapping.Wrap });
        }
        if (mod.SortOrderAfterIds.Count == 0 || instance is null || mod.BadgeColumn <= 0) return;
        var folder = instance.FolderName;
        var button = new Button { Content = new TextBlock { Text = CompatibilityGuidance.Apply(_config.Language), TextWrapping = TextWrapping.Wrap, Foreground = ThemeService.PrimaryTextBrush },
            Theme = Application.Current?.FindResource("FlatButton") as ControlTheme,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, MinHeight = 34,
            Padding = new Thickness(8, 6), Margin = new Thickness(0, 6, 0, 0),
            IsEnabled = !instance.IsDamaged && !_loadingMods && !_instanceWriteInProgress };
        AutomationProperties.SetName(button, CompatibilityGuidance.Apply(_config.Language) + ": " + mod.Name + " · " + scope);
        var help = CompatibilityGuidance.Help(_config.Language);
        AutomationProperties.SetHelpText(button, help);
        ToolTip.SetTip(button, help);
        button.Click += (_, _) =>
        {
            // Resolve fresh full-order diagnostics, not an old row or a filtered subset.
            var current = GetInstanceForColumn(column);
            if (_windowClosed || _loadingMods || _instanceWriteInProgress || current is null || current.IsDamaged ||
                !string.Equals(current.FolderName, folder, StringComparison.OrdinalIgnoreCase)) return;
            var fresh = ApplyOrderFlags(ApplySteamCmdFilter(ResolveActive(current), current), column)
                .FirstOrDefault(row => row.PackageId.Equals(mod.PackageId, StringComparison.OrdinalIgnoreCase));
            if (fresh is null || fresh.SortOrderAfterIds.Count == 0) return;
            ApplySortSuggestion(fresh);
            PaneWorkflow(column).List.Focus();
        };
        content.Children.Insert(4, button);
        content.Children.Insert(5, new TextBlock { Text = CompatibilityGuidance.Note(_config.Language), FontSize = 12,
            Foreground = ThemeService.SecondaryTextBrush, TextWrapping = TextWrapping.Wrap });
    }

    private void ReviewCompatibility(ModInfo mod)
    {
        _detailsVisible = true;
        ApplyWorkspaceLayout();
        ShowDetails(mod);
        Find<ScrollViewer>("DetailsScrollViewer").Offset = default;
        var card = Find<Border>("CompatibilityCard");
        card.BringIntoView();
        card.Focus();
    }
}

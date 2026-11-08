using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PackFrame;

// Keep the row's DataContext stable while immutable display snapshots change. This
// lets live status updates retain focused links, text selection and row containers.
public sealed class ModStatusListRow : INotifyPropertyChanged
{
    public ModStatusDisplayRow Snapshot { get; private set; }
    public string AccessibleName => string.Join(" · ", new[]
    {
        Snapshot.Label,
        $"{Localization.FieldPackageId(Snapshot.Language)}: {Snapshot.PackageId}",
        string.IsNullOrWhiteSpace(Snapshot.Status) ? null :
            $"{Localization.StatusHeader(Snapshot.Language)}: {Snapshot.Status}",
        string.IsNullOrWhiteSpace(Snapshot.LastMissingAt) ? null :
            $"{Localization.LastMissing(Snapshot.Language)}: {Snapshot.LastMissingAt}"
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public event PropertyChangedEventHandler? PropertyChanged;

    internal ModStatusListRow(ModStatusDisplayRow snapshot) => Snapshot = snapshot;

    internal void Update(ModStatusDisplayRow snapshot)
    {
        if (Snapshot == snapshot) return;
        Snapshot = snapshot;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Snapshot)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibleName)));
    }
}

// Display-only filtering: never writes to the instance or its saved history.
internal sealed class ModStatusList
{
    private readonly ListBox _list;
    private readonly TextBox _search;
    private readonly Button _clear;
    private readonly TextBlock _count;
    private readonly string? _language;
    private IReadOnlyList<ModStatusDisplayRow> _all = Array.Empty<ModStatusDisplayRow>();
    private readonly ObservableCollection<ModStatusListRow> _visible = new();
    private string? _selectedPackage;
    private bool _updating;

    internal ModStatusList(Window window, string? language, bool history = false)
    {
        _language = language;
        _list = window.FindControl<ListBox>("Rows")!;
        _search = window.FindControl<TextBox>("SearchBox")!;
        _clear = window.FindControl<Button>("ClearSearchButton")!;
        _count = window.FindControl<TextBlock>("FilterCountText")!;
        _search.PlaceholderText = ModStatusDisplayRow.SearchPlaceholder(language, history);
        var shortcut = OperatingSystem.IsMacOS() ? "⌘F" : "Ctrl+F";
        window.FindControl<TextBlock>("SearchHintText")!.Text = OperationText.Choose(language,
            $"{shortcut} to search · Esc clears search", $"{shortcut} 搜索 · Esc 清除搜索",
            $"{shortcut} 搜尋 · Esc 清除搜尋", $"{shortcut} で検索 · Esc で検索をクリア");
        Avalonia.Automation.AutomationProperties.SetName(_search, _search.PlaceholderText);
        Avalonia.Automation.AutomationProperties.SetName(_clear, OperationText.Choose(language,
            "Clear search", "清除搜索", "清除搜尋", "検索をクリア"));
        var header = window.FindControl<Border>("RowHeaderContent")!;
        // Scroll.Viewport includes the scroll presenter's padding. The arranged
        // items panel is the same width used by row containers, even after filtering.
        _list.LayoutUpdated += (_, _) =>
        {
            if (_list.ItemsPanelRoot is { } panel) AlignHeaderToRows(header, panel);
        };
        _list.ItemsSource = _visible;
        _list.SelectionChanged += (_, _) =>
        {
            if (!_updating && _list.SelectedItem is ModStatusListRow selected)
                _selectedPackage = selected.Snapshot.PackageId;
        };
        _search.TextChanged += (_, _) => Apply();
        _clear.Click += (_, _) => { _search.Clear(); _search.Focus(); };
        // Tunnel so a focused text control cannot consume Escape before the search clears.
        window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    internal static void AlignHeaderToRows(Border header, Panel panel)
    {
        var width = panel.Bounds.Width;
        if (width > 0 && double.IsFinite(width) && header.Width != width)
            header.Width = width;
    }

    internal void Update(IEnumerable<ModStatusDisplayRow> rows)
    {
        _all = rows.ToList();
        Apply();
    }

    internal static IReadOnlyList<ModStatusDisplayRow> Filter(IEnumerable<ModStatusDisplayRow> rows, string? query)
    {
        var term = query?.Trim() ?? "";
        return rows.Where(row => term.Length == 0 || new[] { row.Label, row.Author, row.PackageId,
            row.Link, row.Status, row.LastMissingAt }.Any(value =>
                value.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    // Keep unchanged objects and containers, especially during frequent Steam status refreshes.
    internal static void Reconcile(ObservableCollection<ModStatusListRow> target,
        IReadOnlyList<ModStatusDisplayRow> next)
    {
        var remaining = next.Select(row => row.PackageId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = target.Count - 1; i >= 0; i--)
            if (!remaining.Contains(target[i].Snapshot.PackageId)) target.RemoveAt(i);
        for (var i = 0; i < next.Count; i++)
        {
            if (i < target.Count && target[i].Snapshot == next[i]) continue;
            if (i < target.Count && string.Equals(target[i].Snapshot.PackageId, next[i].PackageId, StringComparison.OrdinalIgnoreCase))
            {
                target[i].Update(next[i]);
                continue;
            }
            var found = -1;
            for (var j = i + 1; j < target.Count; j++)
                if (string.Equals(target[j].Snapshot.PackageId, next[i].PackageId, StringComparison.OrdinalIgnoreCase)) { found = j; break; }
            if (found >= 0)
            {
                target.Move(found, i);
                target[i].Update(next[i]);
            }
            else target.Insert(i, new ModStatusListRow(next[i]));
        }
        while (target.Count > next.Count) target.RemoveAt(target.Count - 1);
    }

    private void Apply()
    {
        var filtered = Filter(_all, _search.Text);
        _updating = true;
        try
        {
            Reconcile(_visible, filtered);
            _list.SelectedItem = _visible.FirstOrDefault(row => string.Equals(
                row.Snapshot.PackageId, _selectedPackage, StringComparison.OrdinalIgnoreCase));
        }
        finally { _updating = false; }
        _clear.IsEnabled = !string.IsNullOrEmpty(_search.Text);
        _count.Text = OperationText.Choose(_language,
            $"Showing {filtered.Count} of {_all.Count}", $"显示 {filtered.Count} / {_all.Count} 条",
            $"顯示 {filtered.Count} / {_all.Count} 筆", $"{_all.Count} 件中 {filtered.Count} 件を表示");
        if (filtered.Count == 0 && _all.Count > 0)
            _count.Text += OperationText.Choose(_language,
                " · No matches. Clear search to show all entries.", " · 无匹配项。清除搜索以显示全部记录。",
                " · 無符合項目。清除搜尋以顯示全部記錄。", " · 一致する項目がありません。検索をクリアすると全件を表示します。");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // ListBox deliberately uses one tab stop. Expose the current row's action
        // without changing it to a tab sequence through every realized mod row.
        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None &&
            e.Source is ListBoxItem item && item.FindAncestorOfType<ListBox>() == _list)
        {
            var link = item.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(button => button.IsEffectivelyEnabled && button.IsEffectivelyVisible);
            if (link is not null && link.Focus(NavigationMethod.Tab)) e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift &&
            e.Source is Button button && button.FindAncestorOfType<ListBox>() == _list &&
            button.FindAncestorOfType<ListBoxItem>() is { } row)
        {
            e.Handled = row.Focus(NavigationMethod.Tab);
            return;
        }
        if (e.Key == Key.F && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            _search.Focus();
            _search.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && !string.IsNullOrEmpty(_search.Text))
        {
            _search.Clear();
            _search.Focus();
            e.Handled = true;
        }
    }
}

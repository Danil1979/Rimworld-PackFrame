using System;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PackFrame;

public partial class ModpackWindow
{
    private ModpackEntry? _preferredCatalogEntry;

    private void InitializeCatalogSearch()
    {
        var search = Find<TextBox>("CatalogSearchBox");
        search.PlaceholderText = T("Search modpack names, summaries or IDs…", "搜索合集名称、简介或 ID…",
            "搜尋合集名稱、簡介或 ID…", "パック名・概要・ID を検索…");
        AutomationProperties.SetName(search, search.PlaceholderText);
        var clear = T("Clear catalog search", "清除合集搜索", "清除合集搜尋", "カタログ検索をクリア");
        AutomationProperties.SetName(Find<Button>("ClearCatalogSearchButton"), clear);
        ToolTip.SetTip(Find<Button>("ClearCatalogSearchButton"), clear);
        var shortcut = OperatingSystem.IsMacOS() ? "⌘F" : "Ctrl+F";
        Find<TextBlock>("CatalogSearchHint").Text = T($"{shortcut} search · ↓ browse · Esc clear",
            $"{shortcut} 搜索 · ↓ 浏览 · Esc 清除", $"{shortcut} 搜尋 · ↓ 瀏覽 · Esc 清除",
            $"{shortcut} 検索 · ↓ 一覧へ · Esc クリア");
        AddHandler(InputElement.KeyDownEvent, OnCatalogSearchKeyDown, RoutingStrategies.Tunnel);
    }

    private void UpdateCatalogSearchState(int visible)
    {
        Find<TextBlock>("ListCount").Text = $"{visible} / {_items.Count}";
        var count = T($"Showing {visible} of {_items.Count} modpacks", $"显示 {visible} / {_items.Count} 个合集",
            $"顯示 {visible} / {_items.Count} 個合集", $"{_items.Count} パック中 {visible} 件を表示");
        AutomationProperties.SetName(Find<TextBlock>("ListCount"), count);
        ToolTip.SetTip(Find<TextBlock>("ListCount"), count);
        var empty = Find<TextBlock>("CatalogSearchEmpty");
        empty.Text = T("No matching modpacks.\nClear search to show all entries.", "没有匹配的合集。\n清除搜索以显示全部合集。",
            "沒有符合的合集。\n清除搜尋以顯示全部合集。", "一致するパックがありません。\n検索をクリアすると全件を表示します。");
        empty.IsVisible = _items.Count > 0 && visible == 0;
        Find<Button>("ClearCatalogSearchButton").IsEnabled = !_busy &&
            !string.IsNullOrEmpty(Find<TextBox>("CatalogSearchBox").Text);
    }

    private void OnCatalogSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_closed || _busy || _applyingCatalog) return;
        var previous = _selectedItem;
        var visible = CatalogSearch.Filter(_items, Find<TextBox>("CatalogSearchBox").Text);
        var matching = CatalogSearch.Find(visible, previous?.Entry ?? _preferredCatalogEntry);
        var selected = matching < 0 ? null : visible[matching];
        var list = Find<ListBox>("PackList");
        _applyingCatalog = true;
        try { list.ItemsSource = visible; list.SelectedItem = selected; }
        finally { _applyingCatalog = false; }
        UpdateCatalogSearchState(visible.Count);
        if (ReferenceEquals(previous, selected) && (selected is not null || _items.Count == 0)) return;
        CancelDetailLoad();
        _selectedItem = null;
        _selectedPack = null;
        ClearCatalogSelection(visible.Count);
        SetControls(true);
        if (selected is not null) _ = LoadPackAsync(selected);
    }

    private void ClearCatalogSelection(int visible)
    {
        var noMatches = _items.Count > 0 && visible == 0;
        ClearDetail(noMatches
            ? T("No matching modpacks.", "没有匹配的合集。", "沒有符合的合集。", "一致するパックがありません。")
            : _items.Count == 0 ? Localization.ModpackEmpty(_language) : Localization.ModpackSelectHint(_language));
        // The list already explains how to recover. Keep the empty details area quiet.
        if (noMatches) SetPackNotice(null);
    }

    private void OnClearCatalogSearchClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || _closed) return;
        Find<TextBox>("CatalogSearchBox").Clear();
        Find<TextBox>("CatalogSearchBox").Focus();
    }

    private void OnCatalogSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_closed || _busy) return;
        var search = Find<TextBox>("CatalogSearchBox");
        var list = Find<ListBox>("PackList");
        if (e.Key == Key.F && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            search.Focus(); search.SelectAll(); e.Handled = true;
        }
        else if (e.Key == Key.Escape && !string.IsNullOrEmpty(search.Text) &&
                 (search.IsKeyboardFocusWithin || list.IsKeyboardFocusWithin))
        {
            search.Clear(); search.Focus(); e.Handled = true;
        }
        else if (e.Key is Key.Down or Key.Enter && search.IsKeyboardFocusWithin && list.ItemCount > 0)
        {
            if (list.SelectedIndex < 0) list.SelectedIndex = 0;
            var target = list.SelectedItem;
            list.ScrollIntoView(target!);
            list.ContainerFromIndex(list.SelectedIndex)?.Focus(NavigationMethod.Directional);
            // A filtered/virtualized row may be realized on the next layout pass.
            Dispatcher.UIThread.Post(() =>
            {
                if (!_closed && !_busy && search.IsKeyboardFocusWithin && ReferenceEquals(list.SelectedItem, target))
                    list.ContainerFromIndex(list.SelectedIndex)?.Focus(NavigationMethod.Directional);
            }, DispatcherPriority.Loaded);
            e.Handled = true;
        }
    }
}

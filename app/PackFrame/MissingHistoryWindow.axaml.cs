using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class MissingHistoryWindow : Window
{
    private ManagedInstance? _instance;
    private string? _language;
    private bool _isClearing;
    private bool _canClear;

    public MissingHistoryWindow()
    {
        InitializeComponent();
    }

    public MissingHistoryWindow(ManagedInstance instance, string? language)
        : this()
    {
        _instance = instance;
        _language = language;
        var title = Localization.MissingHistoryTitle(language);
        Title = $"Rimworld PackFrame - {title}";
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<Button>("ClearButton")!.Content = Localization.ClearHistory(language);
        this.FindControl<TextBlock>("LabelHeader")!.Text = Localization.FieldName(language);
        this.FindControl<TextBlock>("AuthorHeader")!.Text = Localization.FieldAuthor(language);
        this.FindControl<TextBlock>("PackageHeader")!.Text =
            Localization.FieldPackageId(language);
        this.FindControl<TextBlock>("LinkHeader")!.Text = Localization.Link(language);
        this.FindControl<TextBlock>("LastMissingHeader")!.Text =
            Localization.LastMissing(language);
        this.FindControl<Button>("FooterCloseButton")!.Content =
            Localization.CloseButton(language);
        this.FindControl<Button>("RefreshButton")!.Content = Localization.Refresh(language);
        this.FindControl<TextBlock>("SummaryText")!.Text = instance.DisplayName;
        KeyDown += OnWindowKeyDown;
        LoadRows();
    }

    private void LoadRows()
    {
        if (_instance is null)
        {
            return;
        }

        try
        {
            var rows = ModFullNameList.LoadHistoryOrThrow(_instance)
                .Select(entry => new MissingHistoryEntry
                {
                    Label = entry.Label,
                    Author = string.IsNullOrWhiteSpace(entry.Author) ? "—" : entry.Author,
                    PackageId = entry.PackageId,
                    Link = string.IsNullOrWhiteSpace(entry.Link) ? "—" : entry.Link,
                    LastMissingAt = string.IsNullOrWhiteSpace(entry.LastMissingAt) ? "—" : entry.LastMissingAt
                }).ToList();
            ApplyRows(rows);
        }
        catch (Exception exception)
        {
            _canClear = false;
            this.FindControl<Button>("ClearButton")!.IsEnabled = false;
            this.FindControl<TextBlock>("EmptyText")!.IsVisible = false;
            ShowFailure(exception, clearing: false);
        }
    }

    private void ApplyRows(IReadOnlyList<MissingHistoryEntry> rows)
    {
        if (_instance is null) return;
        this.FindControl<TextBlock>("StatusText")!.IsVisible = false;
        this.FindControl<ItemsControl>("Rows")!.ItemsSource = rows;
        this.FindControl<TextBlock>("SummaryText")!.Text =
            Localization.MissingHistorySummary(
                _language,
                _instance.DisplayName,
                rows.Count
            );
        this.FindControl<TextBlock>("EmptyText")!.Text =
            Localization.MissingHistoryEmpty(_language);
        this.FindControl<TextBlock>("EmptyText")!.IsVisible = rows.Count == 0;
        _canClear = rows.Count > 0;
        this.FindControl<Button>("ClearButton")!.IsEnabled = _canClear && !_isClearing;
    }

    private async void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (_instance is null || _isClearing || !_canClear) return;
        _isClearing = true;
        this.FindControl<Button>("ClearButton")!.IsEnabled = false;
        this.FindControl<Button>("RefreshButton")!.IsEnabled = false;
        try
        {
            var confirm = new ConfirmWindow(
                Localization.ClearHistoryConfirm(_language, _instance.DisplayName),
                Localization.ClearHistory(_language),
                Localization.CancelButton(_language));
            if (!await confirm.ShowDialog<bool>(this)) return;

            ModFullNameList.ClearHistory(_instance);
            ApplyRows(Array.Empty<MissingHistoryEntry>());
        }
        catch (Exception exception)
        {
            ShowFailure(exception, clearing: true);
        }
        finally
        {
            _isClearing = false;
            this.FindControl<Button>("ClearButton")!.IsEnabled = _canClear;
            this.FindControl<Button>("RefreshButton")!.IsEnabled = true;
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (!_isClearing) LoadRows();
    }

    private void ShowFailure(Exception exception, bool clearing)
    {
        var status = this.FindControl<TextBlock>("StatusText")!;
        status.Text = clearing
            ? OperationText.Choose(_language,
                "History could not be cleared. Saved history and displayed entries are unchanged. Check the history file and folder permissions, then try again.",
                "无法清除历史记录。已保存的历史和显示的记录未改变。请检查历史记录文件和文件夹权限后重试。",
                "無法清除歷史記錄。已儲存的歷史和顯示的記錄未變更。請檢查歷史記錄檔案和資料夾權限後重試。",
                "履歴を消去できませんでした。保存済みの履歴と表示中の項目は変更していません。履歴ファイルとフォルダーの権限を確認して再試行してください。")
            : OperationText.Choose(_language,
                "History could not be read. Previously displayed entries are unchanged. Check the history file and folder permissions, then select Refresh.",
                "无法读取历史记录。先前显示的记录未改变。请检查历史记录文件和文件夹权限，然后点击刷新。",
                "無法讀取歷史記錄。先前顯示的記錄未變更。請檢查歷史記錄檔案和資料夾權限，然後點選重新整理。",
                "履歴を読み取れませんでした。表示済みの項目は変更していません。履歴ファイルとフォルダーの権限を確認して更新してください。");
        status.IsVisible = true;
        ToolTip.SetTip(status, exception.Message);
        AppLog.Error(clearing ? "Clear missing history" : "Read missing history", exception);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

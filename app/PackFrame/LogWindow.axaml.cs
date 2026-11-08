using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PackFrame;

public partial class LogWindow : Window
{
    private const int MaxLogChars = 400_000;
    private readonly string? _language;

    public LogWindow()
    {
        InitializeComponent();
    }

    public LogWindow(string? language)
        : this()
    {
        _language = language;
        Title = $"Rimworld PackFrame - {Localization.LogButton(language)}";
        this.FindControl<Button>("OpenLogFileButton")!.Content =
            Localization.OpenLogFile(language);
        this.FindControl<Button>("RefreshButton")!.Content = Localization.Refresh(language);
        this.FindControl<Button>("CloseButton")!.Content = Localization.CloseButton(language);
        Avalonia.Automation.AutomationProperties.SetName(this.FindControl<SelectableTextBlock>("LogText")!, Localization.LogButton(language));
        KeyDown += OnWindowKeyDown;
        LoadLog();
        Opened += (_, _) => Dispatcher.UIThread.Post(
            () => this.FindControl<ScrollViewer>("LogScroll")!.ScrollToEnd(),
            DispatcherPriority.Background
        );
    }

    private void LoadLog()
    {
        var read = DiagnosticLogReader.Read(AppLog.LogPath, MaxLogChars);
        var status = this.FindControl<TextBlock>("StatusText")!;
        status.IsVisible = read.Failed;
        status.Text = read.Failed ? OperationText.Choose(_language,
            "The log could not be read. Check folder permissions, then select Refresh. Previously displayed entries are unchanged.",
            "无法读取日志。请检查文件夹权限，然后点击刷新。先前显示的记录未改变。",
            "無法讀取記錄。請檢查資料夾權限，然後點選重新整理。先前顯示的記錄未變更。",
            "ログを読み取れません。フォルダーの権限を確認して更新してください。表示済みのログは変更していません。") : null;
        this.FindControl<Button>("OpenLogFileButton")!.IsEnabled = !read.Missing;
        if (!read.Failed)
        {
            this.FindControl<SelectableTextBlock>("LogText")!.Text =
                string.IsNullOrWhiteSpace(read.Text) ? Localization.LogEmpty(_language) : read.Text;
        }
    }

    private void OnOpenLogFileClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(AppLog.LogPath))
            {
                LoadLog();
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = AppLog.LogPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            var status = this.FindControl<TextBlock>("StatusText")!;
            status.Text = OperationText.Choose(_language,
                "Could not open the log in another app. You can still select and copy the entries here.",
                "无法在其他应用中打开日志。您仍可在此选择并复制记录。",
                "無法在其他應用程式中開啟記錄。您仍可在此選取並複製記錄。",
                "別のアプリでログを開けませんでした。ここでログを選択してコピーできます。");
            status.IsVisible = true;
            AppLog.Error("Open log externally", exception);
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => LoadLog();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}

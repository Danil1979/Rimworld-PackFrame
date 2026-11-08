using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace PackFrame;

public partial class ConfigFolderWindow : Window
{
    private readonly Border _pathField;
    private readonly TextBlock _autoDetectStatus;
    private string _autoDetectFailedText = string.Empty;
    private string? _language;
    private bool _closed;
    internal Func<string, string?>? TryAcceptPath { get; set; }

    public ConfigFolderWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _closed = true;
        _pathField = this.FindControl<Border>("PathField")!;
        _autoDetectStatus = this.FindControl<TextBlock>("AutoDetectStatus")!;
        this.FindControl<TextBox>("PathBox")!.TextChanged += (_, _) =>
        {
            SetInvalid(false);
            _autoDetectStatus.Text = string.Empty;
        };
    }

    public ConfigFolderWindow(
        string? currentPath,
        string description,
        string browseText,
        string confirmText,
        string cancelText,
        string watermark,
        string autoDetectText,
        string autoDetectFailedText,
        string? language = null
    )
        : this()
    {
        _language = language;
        Title = OperationText.Choose(language, "RimWorld configuration folder", "RimWorld 配置文件夹", "RimWorld 設定資料夾", "RimWorld 設定フォルダー");
        this.FindControl<TextBlock>("HeadingText")!.Text = Title;
        this.FindControl<TextBlock>("CurrentFolderLabel")!.Text = OfficialConfigWorkflow.CurrentFolder(language);
        this.FindControl<SelectableTextBlock>("CurrentFolderText")!.Text = currentPath;
        this.FindControl<StackPanel>("CurrentFolderPanel")!.IsVisible = !string.IsNullOrWhiteSpace(currentPath);
        this.FindControl<TextBlock>("PathLabel")!.Text = OfficialConfigWorkflow.NewFolder(language);
        var pathBox = this.FindControl<TextBox>("PathBox")!;
        AutomationProperties.SetName(pathBox, Title);
        pathBox.Text = currentPath ?? string.Empty;
        pathBox.PlaceholderText = watermark;
        this.FindControl<TextBlock>("DescriptionText")!.Text = description;
        this.FindControl<Button>("BrowseButton")!.Content = browseText;
        this.FindControl<Button>("ConfirmButton")!.Content = confirmText;
        this.FindControl<Button>("CancelButton")!.Content = cancelText;
        this.FindControl<Button>("AutoDetectButton")!.Content = autoDetectText;
        _autoDetectFailedText = autoDetectFailedText;

        if (string.IsNullOrWhiteSpace(currentPath))
        {
            AutoDetect();
        }

        Opened += (_, _) =>
        {
            pathBox.Focus();
            pathBox.SelectAll();
        };
        pathBox.KeyDown += OnPathKeyDown;
    }

    private void OnPathKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirm();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Confirm();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Confirm()
    {
        var text = (this.FindControl<TextBox>("PathBox")!.Text ?? string.Empty)
            .Trim()
            .Trim('"');
        if (text.Length == 0)
        {
            ShowPathError(OperationText.Choose(_language, "Enter a folder path or choose Browse.", "请输入文件夹路径，或点击浏览。", "請輸入資料夾路徑，或點選瀏覽。", "フォルダーのパスを入力するか、参照で選択してください。"));
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(text));
        }
        catch
        {
            ShowPathError(OperationText.Choose(_language, "This path is not valid. Check it and try again.", "路径格式无效，请检查后重试。", "路徑格式無效，請檢查後重試。", "パスが無効です。確認して再試行してください。"));
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            ShowPathError(OperationText.Choose(_language, "This folder does not exist or cannot be accessed. Choose an existing folder.", "文件夹不存在或无法访问，请选择现有文件夹。", "資料夾不存在或無法存取，請選擇現有資料夾。", "フォルダーが存在しないか、アクセスできません。既存のフォルダーを選択してください。"));
            return;
        }

        var error = TryAcceptPath?.Invoke(fullPath);
        if (!string.IsNullOrWhiteSpace(error))
        {
            // The chosen path is valid; persistence or an old draft prevents the
            // change. Keep the input for retry without marking the path invalid.
            _autoDetectStatus.Text = error;
            SetInvalid(false);
            this.FindControl<Button>("ConfirmButton")!.Focus();
            RevealStatus();
            return;
        }
        Close(fullPath);
    }

    private void OnAutoDetectClick(object? sender, RoutedEventArgs e)
    {
        AutoDetect();
    }

    private void AutoDetect()
    {
        var path = RimWorldLocator.FindConfigDirectory();
        if (string.IsNullOrWhiteSpace(path))
        {
            _autoDetectStatus.Text = _autoDetectFailedText;
            SetInvalid(true);
            RevealStatus();
            return;
        }

        this.FindControl<TextBox>("PathBox")!.Text = path;
        _autoDetectStatus.Text = string.Empty;
        SetInvalid(false);
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null)
            {
                ShowPathError(OperationText.Choose(_language, "The folder picker is unavailable. Enter the path directly.", "文件夹选择器不可用，请直接输入路径。", "資料夾選擇器無法使用，請直接輸入路徑。", "フォルダー選択を利用できません。パスを直接入力してください。"));
                return;
            }

            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });
            if (_closed || folders.Count == 0)
            {
                return;
            }

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            this.FindControl<TextBox>("PathBox")!.Text = path;
            SetInvalid(false);
        }
        catch (Exception ex)
        {
            AppLog.Error("Open configuration folder picker", ex);
            if (_closed) return;
            ShowPathError(OperationText.Choose(_language, "Could not open the folder picker. Enter the path directly or try again.", "无法打开文件夹选择器，请直接输入路径或重试。", "無法開啟資料夾選擇器，請直接輸入路徑或重試。", "フォルダー選択を開けませんでした。パスを入力するか再試行してください。"));
        }
    }

    private void ShowPathError(string message)
    {
        _autoDetectStatus.Text = message;
        SetInvalid(true);
        this.FindControl<TextBox>("PathBox")!.Focus();
        RevealStatus();
    }

    private void RevealStatus() => Dispatcher.UIThread.Post(() =>
    {
        if (!_closed) _autoDetectStatus.BringIntoView();
    }, DispatcherPriority.Loaded);

    private void SetInvalid(bool invalid)
    {
        if (invalid)
        {
            if (!_pathField.Classes.Contains("invalid"))
            {
                _pathField.Classes.Add("invalid");
            }
        }
        else
        {
            _pathField.Classes.Remove("invalid");
        }
    }
}

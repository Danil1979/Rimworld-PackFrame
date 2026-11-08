using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace PackFrame;

internal sealed class AppUpdateWindow : Window
{
    private readonly string? _language;
    private readonly UpdateInfo _update;
    private readonly Action<string?> _openUrl;
    private readonly bool _canInstall;
    private readonly Func<UpdateInfo, IProgress<AppUpdateProgress>, CancellationToken, Task<PreparedAppUpdate>> _prepare;
    private readonly Button _action;
    private readonly Button _later;
    private readonly SelectableTextBlock _status;
    private readonly ProgressBar _progress;
    private CancellationTokenSource? _download;
    private bool _closed;

    internal PreparedAppUpdate? Prepared { get; private set; }

    internal AppUpdateWindow(string? language, UpdateInfo update, PreparedAppUpdate? prepared,
        Action<string?>? openUrl = null,
        Func<UpdateInfo, IProgress<AppUpdateProgress>, CancellationToken, Task<PreparedAppUpdate>>? prepare = null,
        bool? canInstall = null)
    {
        _language = language;
        _update = update;
        Prepared = prepared;
        _openUrl = openUrl ?? (url => OpenReleasePage(url));
        _canInstall = canInstall ?? AppUpdateService.CanInstall;
        _prepare = prepare ?? ((info, progress, token) => AppUpdateService.PrepareAsync(info, progress, token));
        Title = T("PackFrame update", "PackFrame 更新", "PackFrame 更新", "PackFrame の更新");
        RequestedThemeVariant = ThemeVariant.Dark;
        Background = ThemeService.BackgroundBrush;
        Foreground = ThemeService.PrimaryTextBrush;
        Width = 640;
        Height = 560;
        MinWidth = 480;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _status = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(_status, T("Update status", "更新状态", "更新狀態", "更新状態"));
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, IsVisible = false };
        _later = MakeButton(T("Later", "稍后", "稍後", "後で"));
        _later.IsCancel = true;
        _later.Click += (_, _) => Close();
        _action = MakeButton(string.Empty);
        _action.Click += OnActionClick;
        AutomationProperties.SetName(_progress, T("Download progress", "下载进度", "下載進度", "ダウンロードの進行状況"));
        var buttons = new WrapPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _later, _action }
        };
        _later.Margin = new Thickness(0, 4, 8, 0);
        _action.Margin = new Thickness(0, 4, 0, 0);
        var notes = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? T("No release notes provided.", "未提供更新说明。", "未提供更新說明。", "更新内容の記載はありません。")
            : update.ReleaseNotes.Length > 20000 ? update.ReleaseNotes[..20000] : update.ReleaseNotes;
        var heading = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12), Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = T("Available version: ", "可用版本：", "可用版本：", "利用可能なバージョン: ") + update.Tag,
                    FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 64, TextTrimming = TextTrimming.CharacterEllipsis
                },
                new TextBlock
                {
                    Text = T("Current version: ", "当前版本：", "目前版本：", "現在のバージョン: ") + UpdateService.CurrentVersion,
                    Foreground = ThemeService.SecondaryTextBrush, TextWrapping = TextWrapping.Wrap
                },
                new TextBlock { Text = T("Release notes", "更新说明", "更新說明", "更新内容") }
            }
        };
        var notesView = new ScrollViewer
        {
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new SelectableTextBlock { Text = notes, TextWrapping = TextWrapping.Wrap }
        };
        var statusView = new ScrollViewer
        {
            MaxHeight = 120, Margin = new Thickness(0, 0, 0, 12),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _status
        };
        var footer = new StackPanel { Spacing = 8, Children = { _progress, buttons } };
        var layout = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };
        Grid.SetRow(notesView, 1); Grid.SetRow(statusView, 2); Grid.SetRow(footer, 3);
        layout.Children.Add(heading); layout.Children.Add(notesView);
        layout.Children.Add(statusView); layout.Children.Add(footer);
        Content = layout;
        SetReadyState();
        Opened += (_, _) => _later.Focus();
        Closing += (_, _) => _download?.Cancel();
        Closed += (_, _) => { _closed = true; _download?.Cancel(); };
    }

    private Button MakeButton(string text) => new()
    {
        Content = text, MinHeight = 32,
        Theme = Application.Current?.FindResource("FlatButton") as ControlTheme
    };

    internal static void OpenReleasePage(string? url, Action<Uri>? open = null)
    {
        if (!ModStatusLink.TryOpen(url, out var error, open))
            throw error ?? new InvalidOperationException("The release page does not have a valid web address.");
    }

    private string T(string en, string zh, string tw, string ja) =>
        OperationText.Choose(_language, en, zh, tw, ja);

    private void SetReadyState()
    {
        _action.IsEnabled = true;
        _later.Content = T("Later", "稍后", "稍後", "後で");
        if (Prepared is not null)
        {
            _status.Text = T("Update downloaded and verified. Ready to install.",
                "更新已下载并校验，可以安装。", "更新已下載並驗證，可以安裝。", "更新をダウンロードし、検証しました。インストールできます。");
            _action.Content = T("Install and restart", "安装并重启", "安裝並重新啟動", "インストールして再起動");
        }
        else if (_update.Asset is not null && _canInstall)
        {
            var size = _update.Asset.Size / 1024d / 1024d;
            _status.Text = T($"Download: {size:0.#} MB. Instances and settings are preserved.",
                $"下载大小：{size:0.#} MB。保留实例和设置。", $"下載大小：{size:0.#} MB。保留執行個體和設定。",
                $"ダウンロード: {size:0.#} MB。インスタンスと設定は保持されます。");
            _action.Content = T("Download update", "下载更新", "下載更新", "更新をダウンロード");
        }
        else
        {
            _status.Text = T("No compatible in-app update package is available. You can check the release page.",
                "暂无适用的应用内更新包，可前往发布页面查看。", "暫無適用的應用程式內更新套件，可前往發行頁面查看。",
                "アプリ内更新に対応するパッケージはありません。リリースページをご確認ください。");
            _action.Content = T("Open release page", "查看发布页面", "查看發行頁面", "リリースページを開く");
        }
    }

    private async void OnActionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_download is not null) return;
        if (Prepared is not null)
        {
            Close(Prepared);
            return;
        }
        if (_update.Asset is null || !_canInstall)
        {
            try { _openUrl(_update.Url); }
            catch (Exception exception)
            {
                AppLog.Error("Open update release page", exception);
                _status.Text = T("Could not open the release page. You can copy this address into your browser:",
                    "无法打开发布页面。您可以将此地址复制到浏览器：",
                    "無法開啟發行頁面。您可以將此位址複製到瀏覽器：",
                    "リリースページを開けませんでした。このアドレスをブラウザーにコピーできます:") + "\n" + _update.Url;
            }
            return;
        }

        var download = new CancellationTokenSource();
        _download = download;
        _action.IsEnabled = false;
        _later.Content = Localization.CancelButton(_language);
        _progress.IsVisible = true;
        _progress.IsIndeterminate = true;
        _status.Text = T("Downloading update...", "正在下载更新...", "正在下載更新...", "更新をダウンロード中...");
        try
        {
            var progress = new Progress<AppUpdateProgress>(value =>
            {
                if (_closed || !ReferenceEquals(_download, download)) return;
                if (value.Phase == "verify")
                {
                    _progress.IsIndeterminate = true;
                    _status.Text = T("Verifying download...", "正在校验下载...", "正在驗證下載...", "ダウンロードを検証中...");
                }
                else if (value.Phase == "extract")
                {
                    _progress.IsIndeterminate = true;
                    _status.Text = T("Preparing update files...", "正在准备更新文件...", "正在準備更新檔案...", "更新ファイルを準備中...");
                }
                else if (value.TotalBytes is > 0)
                {
                    _progress.IsIndeterminate = false;
                    _progress.Value = Math.Clamp(100d * value.BytesReceived / value.TotalBytes.Value, 0, 100);
                }
            });
            var token = download.Token;
            var prepared = await Task.Run(() => _prepare(_update, progress, token), token);
            if (!_closed && !token.IsCancellationRequested)
            {
                Prepared = prepared;
                SetReadyState();
                _later.Focus();
            }
        }
        catch (OperationCanceledException)
        {
            if (!_closed)
            {
                SetReadyState();
                _status.Text = T("Download canceled. You can try again.", "下载已取消，可以重试。",
                    "下載已取消，可以重試。", "ダウンロードをキャンセルしました。再試行できます。");
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("Download app update", exception);
            if (!_closed)
            {
                SetReadyState();
                _action.Content = T("Retry download", "重试下载", "重試下載", "ダウンロードを再試行");
                _status.Text = T("Download failed. Check your connection, free disk space and folder permissions, then try again.",
                    "下载失败。请检查网络连接、磁盘空间和文件夹权限，然后重试。",
                    "下載失敗。請檢查網路連線、磁碟空間和資料夾權限，然後重試。",
                    "ダウンロードに失敗しました。接続、ディスクの空き容量、フォルダーの権限を確認して再試行してください。") + "\n" + exception.Message;
            }
        }
        finally
        {
            download.Dispose();
            if (ReferenceEquals(_download, download)) _download = null;
            if (!_closed) _progress.IsVisible = false;
        }
    }
}

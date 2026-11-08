using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace PackFrame;

public partial class ModpackWindow
{
    private CancellationTokenSource? _catalogCancellation;
    private CancellationTokenSource? _detailCancellation;
    private int _catalogVersion;
    private string? _catalogUrl;
    private string? _catalogHash;
    private bool _applyingCatalog;
    private bool _catalogIsBundled;
    private ModpackCatalogSnapshot? _pendingCatalog;
    private readonly HashSet<string> _checkedPackDetails = new(StringComparer.Ordinal);

    private async Task LoadIndexAsync(bool refreshDetails = false)
    {
        if (_busy || _closed) return;
        var rawUrl = RepoBox.Text?.Trim() ?? string.Empty;
        _catalogCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _catalogCancellation = cancellation;
        var version = ++_catalogVersion;
        _pendingCatalog = null;
        string? normalized = null;

        try
        {
            normalized = ModpackService.NormalizeIndexUrl(rawUrl);
            if (_config is not null)
            {
                _config.ModpackIndexUrl = rawUrl;
                _config.Save();
            }
            if (_catalogUrl != normalized)
            {
                _catalogUrl = normalized;
                _catalogHash = null;
                ClearCatalog();
            }

            var local = ModpackCatalogCache.Shared.LoadLocal(normalized);
            if (local is not null) ApplyCatalog(local, refreshDetails);
            else if (_indexUri is null)
                ClearDetail(T("Loading catalog...", "正在读取合集列表...",
                    "正在讀取合集清單...", "カタログを読み込み中..."));

            SetCatalogStatus(T("Checking catalog for updates...", "正在检查合集列表更新...",
                "正在檢查合集清單更新...", "カタログの更新を確認中..."));
            // Let the local catalog render before beginning the network check.
            await Task.Yield();
            var updated = await ModpackCatalogCache.Shared.RefreshAsync(normalized, cancellation.Token);
            if (_closed || version != _catalogVersion) return;

            var changed = updated.ContentHash != _catalogHash;
            if (_busy) _pendingCatalog = updated;
            else ApplyCatalog(updated);
            SetCatalogStatus(changed
                ? T("Catalog updated.", "合集列表已更新。", "合集清單已更新。", "カタログを更新しました。")
                : T("Catalog up to date.", "合集列表已是最新。", "合集清單已是最新。", "カタログは最新です。"));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (_closed || version != _catalogVersion) return;
            if (normalized is null)
            {
                _catalogUrl = null;
                _catalogHash = null;
                ClearCatalog();
            }
            if (_indexUri is not null)
            {
                var source = _catalogIsBundled
                    ? T("Showing the bundled catalog.", "正在显示内置合集列表。", "正在顯示內建合集清單。", "同梱カタログを表示しています。")
                    : T("Showing the saved catalog.", "正在显示已保存的合集列表。", "正在顯示已儲存的合集清單。", "保存済みカタログを表示しています。");
                SetCatalogStatus(source + " " + T(
                    "Could not check for updates. You can keep browsing; new details and installation require a connection. Select Refresh to retry.",
                    "无法检查更新。您仍可浏览本地内容；新详情和安装需要联网。点击“刷新”重试。",
                    "無法檢查更新。您仍可瀏覽本機內容；新詳情和安裝需要連線。點擊「重新整理」重試。",
                    "更新を確認できませんでした。保存済みの内容は閲覧できますが、新しい詳細やインストールには接続が必要です。「更新」で再試行できます。"), exception.Message);
            }
            else
            {
                var message = T("Catalog unavailable. Check the repository address and your connection, then select Refresh.",
                    "无法读取合集列表。请检查仓库地址和网络连接，然后点击“刷新”。",
                    "無法讀取合集清單。請檢查儲存庫網址和網路連線，然後點擊「重新整理」。",
                    "カタログを読み込めません。リポジトリのアドレスと接続を確認して「更新」を選択してください。");
                SetCatalogStatus(message, exception.Message);
                if (!_busy) ClearDetail(message);
            }
        }
        finally
        {
            if (ReferenceEquals(_catalogCancellation, cancellation))
            {
                _catalogCancellation = null;
                if (!_closed) SetControls(true);
            }
        }
    }

    private void ApplyCatalog(ModpackCatalogSnapshot snapshot, bool refreshDetails = false)
    {
        if (_closed) return;
        if (_busy)
        {
            _pendingCatalog = snapshot;
            return;
        }
        _catalogIsBundled = snapshot.IsBundled;
        if (_catalogHash == snapshot.ContentHash && !refreshDetails)
        {
            _indexUri = snapshot.IndexUri;
            return;
        }

        var previous = _selectedItem?.Entry;
        var previousStatus = Find<TextBlock>("StatusText").Text;
        var hadSelectedDetails = _selectedPack is not null;
        CancelDetailLoad();
        _indexUri = snapshot.IndexUri;
        _catalogHash = snapshot.ContentHash;
        _selectedItem = null;
        _selectedPack = null;
        _items = snapshot.Index.Packs
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id) ||
                            !string.IsNullOrWhiteSpace(entry.File) ||
                            !string.IsNullOrWhiteSpace(entry.GiteeFile))
            .Select(entry => new ModpackListItem
            {
                Entry = entry,
                CountText = entry.ModCount > 0
                    ? Localization.ModpackModsCount(_language, entry.ModCount)
                    : string.Empty
            }).ToList();
        ClearDetail(_items.Count == 0
            ? Localization.ModpackEmpty(_language)
            : Localization.ModpackSelectHint(_language));

        var matching = previous is null ? -1 : _items.FindIndex(item =>
            !string.IsNullOrWhiteSpace(previous.Id)
                ? item.Entry.Id == previous.Id
                : item.Entry.File == previous.File && item.Entry.GiteeFile == previous.GiteeFile);
        // Background/deferred catalog updates retain a result for the same selected pack.
        // An explicit Refresh or a genuinely new selection starts fresh browsing guidance.
        var preserveStatus = hadSelectedDetails && previous is not null && matching >= 0 && !refreshDetails &&
            !ModpackFooterStatus.IsSelectionHint(previousStatus, _language);
        if (preserveStatus) Find<TextBlock>("StatusText").Text = previousStatus;
        if (matching < 0 && _currentInstance?.Metadata?.PackId is { } bound)
            matching = _items.FindIndex(item => item.Entry.Id == bound);
        var list = Find<ListBox>("PackList");
        _applyingCatalog = true;
        try
        {
            list.ItemsSource = _items;
            list.SelectedIndex = _items.Count == 0 ? -1 : matching >= 0 ? matching : 0;
        }
        finally { _applyingCatalog = false; }
        Find<TextBlock>("ListCount").Text = _items.Count.ToString();
        if (list.SelectedItem is ModpackListItem selected) _ = LoadPackAsync(selected, refreshDetails, preserveStatus);
        else SetControls(true);
    }

    private void ClearCatalog()
    {
        CancelDetailLoad();
        _indexUri = null;
        _selectedItem = null;
        _selectedPack = null;
        _items = new List<ModpackListItem>();
        Find<ListBox>("PackList").ItemsSource = _items;
        Find<TextBlock>("ListCount").Text = "0";
        ClearDetail(Localization.ModpackNoUrl(_language));
        SetControls(true);
    }

    private void ApplyPendingCatalog()
    {
        if (_closed || _busy || _pendingCatalog is null) return;
        var pending = _pendingCatalog;
        _pendingCatalog = null;
        ApplyCatalog(pending);
    }

    private void CancelDetailLoad()
    {
        _detailVersion++;
        _detailCancellation?.Cancel();
        _detailCancellation = null;
    }

    private void SetCatalogStatus(string text, string? detail = null)
    {
        var status = Find<TextBlock>("CatalogStatusText");
        status.Text = text;
        ToolTip.SetTip(status, detail ?? text);
        var error = Find<Expander>("CatalogErrorDetails");
        error.Header = T("Error details", "错误详情", "錯誤詳情", "エラーの詳細");
        error.IsVisible = !string.IsNullOrWhiteSpace(detail);
        error.IsExpanded = false;
        Find<SelectableTextBlock>("CatalogErrorText").Text = detail;
    }

    private void SetPackNotice(string? text)
    {
        Find<Border>("PackNotice").IsVisible = !string.IsNullOrWhiteSpace(text);
        Find<TextBlock>("PackNoticeText").Text = text;
    }

    private void ShowSelectedPack(ModpackDefinition pack, ModpackListItem item, int version, bool updateFooter = true)
    {
        _selectedPack = pack;
        SetPackNotice(null);
        ShowPack(pack, item);
        SetCover(null);
        if (updateFooter && !_busy)
            Find<TextBlock>("StatusText").Text = ModpackFooterStatus.SelectedHint(_language, _canInstall);
        _ = LoadCoverAsync(pack.Image ?? item.Entry.Image, version);
    }

    private async Task<ModpackDefinition> ReadPackForOperationAsync(CancellationToken cancellationToken)
    {
        if (_selectedItem is null || _indexUri is null)
            throw new InvalidOperationException("No modpack is selected.");
        // Browsing may use saved details; installs and config writes require a fresh manifest.
        Find<TextBlock>("StatusText").Text = T("Checking pack details...", "正在检查合集详情...",
            "正在檢查合集詳情...", "パックの詳細を確認中...");
        var snapshot = await ModpackDetailsCache.Shared.RefreshAsync(
            _selectedItem.Entry, _indexUri, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _selectedPack = snapshot.Pack;
        return snapshot.Pack;
    }
}

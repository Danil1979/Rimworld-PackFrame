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
                SetCatalogStatus(T("Using the saved catalog. Update check failed.",
                    "正在使用本地合集列表，更新检查失败。", "正在使用本機合集清單，更新檢查失敗。",
                    "保存済みカタログを使用中。更新を確認できませんでした。"), exception.Message);
            }
            else
            {
                var message = Localization.ModpackLoadFailed(_language, exception.Message);
                SetCatalogStatus(message);
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
        if (_catalogHash == snapshot.ContentHash && !refreshDetails)
        {
            _indexUri = snapshot.IndexUri;
            return;
        }

        var previous = _selectedItem?.Entry;
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
        if (list.SelectedItem is ModpackListItem selected) _ = LoadPackAsync(selected, refreshDetails);
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
    }

    private void ShowSelectedPack(ModpackDefinition pack, ModpackListItem item, int version)
    {
        _selectedPack = pack;
        ShowPack(pack, item);
        SetCover(null);
        Find<TextBlock>("StatusText").Text = Localization.ModpackSelectHint(_language);
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

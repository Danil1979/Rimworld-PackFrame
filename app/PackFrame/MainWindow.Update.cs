using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class MainWindow
{
    private string UpdateText(string en, string zh, string tw, string ja) =>
        OperationText.Choose(_config.Language, en, zh, tw, ja);

    private async void OnCheckUpdateClick(object? sender, RoutedEventArgs e)
    {
        if (_checkingUpdate) return;
        _checkingUpdate = true;
        _checkUpdateButton.IsEnabled = false;
        using var cancellation = new CancellationTokenSource();
        EventHandler cancelOnClose = (_, _) => cancellation.Cancel();
        Closed += cancelOnClose;
        ShowToast(UpdateText("Checking for updates...", "正在检查更新...", "正在檢查更新...", "更新を確認中..."));
        try
        {
            var update = await UpdateService.CheckAsync(cancellation.Token);
            if (_windowClosed) return;
            if (update is null)
            {
                ShowToast(UpdateText("No published updates are available.", "暂无已发布的更新。", "暫無已發行的更新。", "公開された更新はありません。"));
                return;
            }
            if (!UpdateService.IsNewer(update.Tag))
            {
                ShowToast(UpdateText("You are using the latest version.", "当前已是最新版本。", "目前已是最新版本。", "最新バージョンです。"));
                return;
            }

            var prepared = await Task.Run(() => AppUpdateService.FindPrepared(update), cancellation.Token);
            if (_windowClosed) return;
            var offer = new AppUpdateWindow(_config.Language, update, prepared, OpenUrl);
            var install = await offer.ShowDialog<PreparedAppUpdate?>(this);
            if (install is null || _windowClosed) return;
            await InstallAppUpdateAsync(install);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            AppLog.Error("Check or prepare app update", exception);
            if (!_windowClosed)
                ShowToast(UpdateText("Update failed: ", "更新失败：", "更新失敗：", "更新に失敗しました: ") + exception.Message);
        }
        finally
        {
            Closed -= cancelOnClose;
            _checkingUpdate = false;
            if (!_windowClosed) _checkUpdateButton.IsEnabled = true;
        }
    }

    private async Task InstallAppUpdateAsync(PreparedAppUpdate update)
    {
        if (GameLaunchService.IsRunning())
        {
            ShowToast(UpdateText("Close RimWorld before installing the update. The download is saved for later.",
                "请先关闭 RimWorld 再安装更新。已下载的更新会保留。", "請先關閉 RimWorld 再安裝更新。已下載的更新會保留。",
                "更新をインストールする前に RimWorld を終了してください。ダウンロード済みの更新は保持されます。"));
            return;
        }
        var hadPendingChanges = InstanceStore.HasPendingMods;
        if (!await ResolvePendingChangesAsync() || _windowClosed) return;
        if (hadPendingChanges)
        {
            // A cancelled/failed handoff can leave this window open. Reflect the
            // saved or discarded disk state before preparing the helper.
            ClearUndoHistory();
            ReloadInstances(null, -1);
            UpdateSaveState();
        }

        IsEnabled = false;
        try
        {
            ShowToast(UpdateText("Preparing to restart...", "正在准备重启...", "正在準備重新啟動...", "再起動を準備中..."));
            using var handoff = await Task.Run(() => AppUpdateService.CreateHandoff(update));
            if (_windowClosed) return;
            var closed = false;
            EventHandler commitOnClose = (_, _) =>
            {
                closed = true;
                try { handoff.Commit(); }
                catch (Exception exception) { AppLog.Error("Commit app update handoff", exception); }
            };
            Closed += commitOnClose;
            try
            {
                _closeApproved = true;
                Close();
            }
            finally
            {
                Closed -= commitOnClose;
                if (!closed) _closeApproved = false;
            }
        }
        finally
        {
            if (!_windowClosed) IsEnabled = true;
        }
    }

    private void ShowAppUpdateResult()
    {
        try
        {
            var result = AppUpdateService.TakeLastResult();
            if (result is null) return;
            ShowToast(result.Success
                ? UpdateText("Updated to ", "已更新至 ", "已更新至 ", "更新しました: ") + result.Tag
                : UpdateText("App update failed: ", "程序更新失败：", "程式更新失敗：", "アプリの更新に失敗しました: ") + result.Error);
        }
        catch (Exception exception) { AppLog.Error("Read app update result", exception); }
    }
}

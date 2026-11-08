using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;

namespace PackFrame;

public partial class MainWindow
{
    private bool _instanceOperationBusy;
    private bool _instanceWriteInProgress;
    private int _instanceFileVersion;
    private sealed class UnsupportedInstanceImportException : Exception { }

    private Task CreateInstanceAsync(int column) => RunInstanceOperationAsync(
        Localization.NewInstance(_config.Language), () => CreateInstanceCoreAsync(column));

    private Task CopyInstanceAsync(int column) => RunInstanceOperationAsync(
        Localization.CopyInstance(_config.Language), () => CopyInstanceCoreAsync(column));

    private Task RenameInstanceAsync(int column) => RunInstanceOperationAsync(
        Localization.RenameInstance(_config.Language), () => RenameInstanceCoreAsync(column));

    private Task DeleteInstanceAsync(int column) => RunInstanceOperationAsync(
        Localization.DeleteInstance(_config.Language), () => DeleteInstanceCoreAsync(column));

    private Task ImportInstancesAsync(int column) => RunInstanceOperationAsync(
        Localization.ImportInstance(_config.Language), () => ImportInstancesCoreAsync(column));

    private Task OpenModpacksAsync(int column) => RunInstanceOperationAsync(
        Localization.ModpacksTitle(_config.Language), () => OpenModpacksCoreAsync(column));

    private string CopyModsOperation => OperationText.Choose(_config.Language,
        "Copy mods to instance", "复制模组到实例", "複製模組到執行個體", "インスタンスに MOD をコピー");

    private Task CopyModsToProfileAsync(DragPayload payload, int column, int index) => RunInstanceOperationAsync(
        CopyModsOperation, () => CopyModsToProfileCoreAsync(payload, column, index));

    private async Task RunInstanceOperationAsync(string operation, Func<Task> action)
    {
        if (_windowClosed || _instanceOperationBusy) return;
        if (_loadingMods)
        {
            ShowToast(Localization.LoadingMods(_config.Language));
            return;
        }
        _instanceOperationBusy = true;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            AppLog.Error(operation, exception);
            if (_windowClosed) return;
            var message = operation + "\n\n" + OperationText.Choose(_config.Language,
                "The operation could not be completed. Check the source file, available disk space and folder permissions, then try again. Open the log for details and any recovery paths.",
                "操作未能完成。请检查源文件、磁盘剩余空间和文件夹权限，然后重试。打开日志可查看详情及恢复路径。",
                "操作未能完成。請檢查來源檔案、磁碟剩餘空間和資料夾權限，然後重試。開啟記錄可查看詳情及復原路徑。",
                "操作を完了できませんでした。元ファイル、ディスクの空き容量、フォルダーの権限を確認して再試行してください。詳細と復元先はログで確認できます。");
            if (exception is UnsupportedInstanceImportException)
                message = operation + "\n\n" + InvalidImportMessage();
            var dialog = new ConfirmWindow(message, Localization.LogButton(_config.Language),
                Localization.CloseButton(_config.Language));
            if (await dialog.ShowDialog<bool>(this) && !_windowClosed)
                await new LogWindow(_config.Language).ShowDialog(this);
        }
        finally
        {
            _instanceOperationBusy = false;
            WakeWorkshopMonitoring();
        }
    }

    private string InvalidImportMessage() => OperationText.Choose(_config.Language,
        "This file could not be imported. Choose a valid ModsConfig XML, JSON mod list, RML, RimWorld save or instance ZIP containing Config/ModsConfig.xml. Your current instance has not been replaced.",
        "无法导入此文件。请选择有效的 ModsConfig XML、JSON 模组列表、RML、RimWorld 存档，或包含 Config/ModsConfig.xml 的实例 ZIP。当前实例未被替换。",
        "無法匯入此檔案。請選擇有效的 ModsConfig XML、JSON 模組清單、RML、RimWorld 存檔，或包含 Config/ModsConfig.xml 的執行個體 ZIP。目前執行個體未被取代。",
        "このファイルをインポートできません。有効な ModsConfig XML、JSON MOD リスト、RML、RimWorld セーブ、または Config/ModsConfig.xml を含むインスタンス ZIP を選んでください。現在のインスタンスは置き換えられていません。");

    private Task<T> RunInstanceWorkAsync<T>(string operation, Func<T> work) =>
        RunInstanceWorkCoreAsync(operation, () => Task.Run(work));

    private async Task<T> RunInstanceWorkCoreAsync<T>(string operation, Func<Task<T>> work)
    {
        var content = Content as Control;
        var wasEnabled = content?.IsEnabled ?? true;
        _instanceWriteInProgress = true;
        _instanceFileVersion++;
        if (content is not null) content.IsEnabled = false;
        _undoToastText.TextWrapping = TextWrapping.Wrap;
        _undoToastText.MaxWidth = 600;
        ShowToast(operation + "\n" + OperationText.Choose(_config.Language,
            "Please wait. Keep PackFrame open until this operation finishes.",
            "请稍候。操作完成前请保持 PackFrame 打开。",
            "請稍候。操作完成前請保持 PackFrame 開啟。",
            "処理が完了するまで PackFrame を開いたままお待ちください。"));
        _undoToastTimer.Stop();
        try
        {
            return await work();
        }
        finally
        {
            _instanceWriteInProgress = false;
            _instanceFileVersion++;
            if (!_windowClosed)
            {
                if (content is not null) content.IsEnabled = wasEnabled;
                _undoToast.IsVisible = false;
            }
        }
    }

    private Task RunInstanceWorkAsync(string operation, Action work) =>
        RunInstanceWorkAsync(operation, () => { work(); return true; });

    private bool CanModifyInstanceFiles()
    {
        if (!GameLaunchService.IsRunning()) return true;
        ShowToast(Localization.ModpackGameRunning(_config.Language));
        return false;
    }
}

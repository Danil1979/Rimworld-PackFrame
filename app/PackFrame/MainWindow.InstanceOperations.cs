using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

    internal sealed class InvalidInstanceArchiveException(InvalidDataException cause)
        : Exception("The selected instance ZIP could not be read.", cause) { }

    internal static T ReadInstanceImport<T>(string file, Func<T> read)
    {
        try { return read(); }
        // Catch only direct archive decoder failures for a known ZIP source. Wrapped
        // configuration, metadata and transaction errors retain their recovery details.
        catch (InvalidDataException exception) when (
            Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidInstanceArchiveException(exception);
        }
    }

    internal static bool IsSupportedInstanceImportFile(string file) => ReadInstanceImport(file, () =>
    {
        // Store probing returns false for unreadable inputs. Validate the ZIP directory
        // first so format errors can offer a specific recovery action in the import dialog.
        if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(file);
            _ = archive.Entries.Count;
        }
        return InstanceStore.IsSupportedImportFile(file);
    });

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

    private Task DeleteLocalModsAsync(IReadOnlyList<ModInfo> mods) => RunInstanceOperationAsync(
        Localization.DeleteModFiles(_config.Language), () => DeleteLocalModsCoreAsync(mods));

    internal sealed record LocalModDeletionResult(
        IReadOnlyList<ModInfo> Deleted, IReadOnlyList<(ModInfo Mod, Exception Error)> Failed);

    private sealed class LocalModDeletionException(LocalModDeletionResult result)
        : Exception("Failed to delete " + result.Failed.Count + " local mod folders.",
            new AggregateException(result.Failed.Select(item => item.Error)))
    {
        internal LocalModDeletionResult Result { get; } = result;
    }

    internal static async Task<LocalModDeletionResult?> ConfirmAndDeleteLocalModsAsync(
        IReadOnlyList<ModInfo> mods, Func<IReadOnlyList<ModInfo>, Task<bool>> confirm,
        Func<IReadOnlyList<ModInfo>, Task<LocalModDeletionResult>> work)
    {
        // The displayed count, confirmed paths and eventual work must share one snapshot.
        var targets = Array.AsReadOnly(mods.ToArray());
        if (targets.Count == 0 || !await confirm(targets)) return null;
        return await work(targets);
    }

    internal static LocalModDeletionResult DeleteLocalModFolders(IReadOnlyList<ModInfo> targets,
        Action<string>? deleteDirectory = null)
    {
        deleteDirectory ??= path => Directory.Delete(path, recursive: true);
        var deleted = new List<ModInfo>();
        var failed = new List<(ModInfo Mod, Exception Error)>();
        foreach (var mod in targets)
        {
            try
            {
                if (mod.Source != ModSource.Local || string.IsNullOrWhiteSpace(mod.Path))
                    throw new IOException("The selected item is not a local mod folder: " + mod.Path);
                deleteDirectory(mod.Path);
                deleted.Add(mod);
            }
            catch (Exception exception)
            {
                AppLog.Error("Delete local mod folder: " + mod.Path, exception);
                failed.Add((mod, exception));
            }
        }
        return new LocalModDeletionResult(deleted, failed);
    }

    internal static List<ModInfo> WithoutDeletedModPaths(IReadOnlyList<ModInfo> catalog,
        IReadOnlyList<ModInfo> deleted)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var paths = deleted.Select(mod => NormalizeModDeletionPath(mod.Path))
            .Where(path => path is not null).ToHashSet(comparison);
        // A malformed surviving row must not interrupt reconciliation of successful deletions.
        return catalog.Where(mod => NormalizeModDeletionPath(mod.Path) is not { } path ||
            !paths.Contains(path)).ToList();
    }

    private static string? NormalizeModDeletionPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (ArgumentException) { return null; }
        catch (NotSupportedException) { return null; }
        catch (PathTooLongException) { return null; }
    }

    private static string LocalModDeletionOrderNotice(string? language) => OperationText.Choose(language,
        "Mod-list orders are kept. Deleted files may leave missing mod entries in affected load orders.",
        "模组列表顺序会保留。删除文件后，受影响的加载顺序中可能会出现缺失模组。",
        "模組清單順序會保留。刪除檔案後，受影響的載入順序中可能會出現缺失模組。",
        "MOD の読み込み順は保持されます。ファイルを削除すると、読み込み順に不足 MOD が残る場合があります。");

    private string LocalModDeletionFailureMessage(LocalModDeletionResult result)
    {
        var count = result.Deleted.Count;
        var total = count + result.Failed.Count;
        return OperationText.Choose(_config.Language,
            $"Deleted {count} of {total} mod folders. The remaining folders could not be fully deleted and may contain partially removed files. Check folder permissions and open the log for details.",
            $"已删除 {total} 个模组文件夹中的 {count} 个。其余文件夹未能完全删除，其中的部分文件可能已被移除。请检查文件夹权限并打开日志查看详情。",
            $"已刪除 {total} 個模組資料夾中的 {count} 個。其餘資料夾未能完全刪除，其中的部分檔案可能已被移除。請檢查資料夾權限並開啟記錄查看詳情。",
            $"{total} 個中 {count} 個の MOD フォルダーを削除しました。残りのフォルダーは完全に削除できず、一部のファイルが削除されている可能性があります。アクセス権を確認し、詳細はログをご覧ください。")
            + "\n\n" + LocalModDeletionOrderNotice(_config.Language);
    }

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
            if (exception is InvalidInstanceArchiveException)
                message = operation + "\n\n" + OperationText.InvalidInstanceArchive(_config.Language);
            if (exception is LocalModDeletionException deletion)
                message = operation + "\n\n" + LocalModDeletionFailureMessage(deletion.Result);
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

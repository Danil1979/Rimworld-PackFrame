using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PackFrame;

internal static class FileTransactions
{
    public static void WriteAtomic(string path, Action<string> write)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            write(temporary);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // Stage all files before calling this; retain backups if rollback itself fails.
    public static void ApplyFiles(string staging, string target, bool includeSubdirectories = false)
    {
        Directory.CreateDirectory(target);
        var backup = Path.Combine(target, ".packframe-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var applied = new List<(string Path, string? Backup)>();
        var createdDirectories = new List<string>();
        try
        {
            foreach (var source in Directory.EnumerateFiles(staging, "*", includeSubdirectories
                         ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                         .OrderBy(path => Path.GetRelativePath(staging, path), StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(staging, source);
                var destination = Path.Combine(target, relative);
                EnsureDestinationDirectory(Path.GetDirectoryName(destination)!, target, createdDirectories);
                if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Cannot replace a linked configuration file: " + destination);
                string? old = null;
                if (File.Exists(destination))
                {
                    old = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(old)!);
                    File.Copy(destination, old);
                }
                WriteAtomic(destination, temporary => File.Copy(source, temporary));
                applied.Add((destination, old));
            }
        }
        catch (Exception failure)
        {
            AppLog.Error("Apply configuration files: " + target, failure);
            var errors = new List<Exception> { failure };
            for (var i = applied.Count - 1; i >= 0; i--)
            {
                var entry = applied[i];
                try
                {
                    if (entry.Backup is null) File.Delete(entry.Path);
                    else WriteAtomic(entry.Path, temporary => File.Copy(entry.Backup, temporary));
                }
                catch (Exception rollbackFailure) { errors.Add(rollbackFailure); }
            }
            for (var i = createdDirectories.Count - 1; i >= 0; i--)
            {
                try { Directory.Delete(createdDirectories[i], recursive: false); }
                catch (Exception rollbackFailure) { errors.Add(rollbackFailure); }
            }
            if (errors.Count > 1)
                throw new AggregateException($"Update and rollback failed. Backups: {backup}", errors);
            TryDeleteDirectory(backup);
            throw;
        }
        TryDeleteDirectory(backup);
    }

    private static void EnsureDestinationDirectory(string directory, string target, List<string> created)
    {
        // The selected target itself may be a user-managed link. Do not follow
        // links below it, which could replace unrelated files outside Config.
        if (Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Equals(Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
        EnsureDestinationDirectory(Path.GetDirectoryName(directory)!, target, created);
        if (Directory.Exists(directory))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cannot replace files in a linked configuration folder: " + directory);
            return;
        }
        Directory.CreateDirectory(directory);
        created.Add(directory);
    }

    public static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

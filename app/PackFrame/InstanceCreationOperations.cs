using System;
using System.IO;

namespace PackFrame;

// Per-operation seams keep failure checks deterministic without changing global
// filesystem behavior or the state of other instance operations.
internal sealed class InstanceCreationOperations
{
    internal Action<string, string, bool> CopyFile { get; init; } = File.Copy;
    internal Action<string, Action<string>> WriteAtomic { get; init; } = FileTransactions.WriteAtomic;
    internal Action<string>? BeforeValidation { get; init; }
    internal Action<string, string> PublishDirectory { get; init; } = ExclusiveDirectoryPublication.Move;
    internal Action<string> DeleteStagingDirectory { get; init; } = path => Directory.Delete(path, true);
}

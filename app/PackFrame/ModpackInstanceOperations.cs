using System;

namespace PackFrame;

// Per-install failure seams. Production publication refuses competing targets.
internal sealed class ModpackInstanceOperations
{
    internal Action<string, string> PublishDirectory { get; init; } = ExclusiveDirectoryPublication.Move;
    internal Action<string, string> RestoreMods { get; init; } = ExclusiveDirectoryPublication.Move;
}

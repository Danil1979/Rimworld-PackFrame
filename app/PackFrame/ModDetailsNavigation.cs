using System;

namespace PackFrame;

internal static class ModDetailsNavigation
{
    internal static bool ShouldReset(string? previousPackage, string? previousPath,
        string? package, string? path, bool windows) =>
        !string.Equals(previousPackage, package, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(previousPath, path, windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

using System;
using System.IO;

namespace PackFrame;

internal sealed record DiagnosticLogRead(string? Text, bool Missing, bool Failed);

internal static class DiagnosticLogReader
{
    internal static DiagnosticLogRead Read(string path, int maximumCharacters)
    {
        try
        {
            var text = File.ReadAllText(path);
            return new(text.Length > maximumCharacters ? text[^maximumCharacters..] : text, false, false);
        }
        catch (FileNotFoundException) { return new(null, true, false); }
        catch (DirectoryNotFoundException) { return new(null, true, false); }
        catch (IOException) { return new(null, false, true); }
        catch (UnauthorizedAccessException) { return new(null, false, true); }
    }
}

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace PackFrame;

// Only for publishing a fully prepared sibling directory. Do not replace this
// with a check followed by Directory.Move on Unix: rename can replace an empty
// directory created between that check and the actual move.
internal static class ExclusiveDirectoryPublication
{
    internal static void Move(string source, string destination)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // The Windows runtime uses MoveFile with replacement disabled
                // and supplies the appropriate extended-path handling.
                // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Windows.cs
                Directory.Move(source, destination);
                return;
            }

            int result;
            if (OperatingSystem.IsLinux())
                result = RenameAt2(-100, source, -100, destination, 1); // AT_FDCWD, RENAME_NOREPLACE
            else if (OperatingSystem.IsMacOS())
                result = RenameExclusive(source, destination, 4); // RENAME_EXCL
            else
                throw new PlatformNotSupportedException("Exclusive instance publication is unavailable on this platform.");

            if (result != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                throw new IOException($"Could not publish instance without replacing an existing destination: {destination}",
                    new Win32Exception(error));
            }
        }
        catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException
            or PlatformNotSupportedException)
        {
            // Older libc, kernels or filesystems may lack exclusive renaming.
            // Fail closed; a copy/merge or ordinary rename is not a safe fallback.
            throw new IOException("Exclusive instance publication is not supported: " + destination, exception);
        }
    }

    // Linux ABI: int renameat2(int, const char*, int, const char*, unsigned int).
    // https://man7.org/linux/man-pages/man2/rename.2.html
    // RENAME_NOREPLACE = 1 in linux/fs.h; AT_FDCWD = -100 in linux/fcntl.h.
    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RenameAt2(int oldDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath,
        int newDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath, uint flags);

    // Darwin ABI and RENAME_EXCL = 0x00000004:
    // https://github.com/apple-oss-distributions/xnu/blob/main/bsd/sys/stdio.h
    [DllImport("libSystem.B.dylib", EntryPoint = "renamex_np", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RenameExclusive([MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath, uint flags);
}

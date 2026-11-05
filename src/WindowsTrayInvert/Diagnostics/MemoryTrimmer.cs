using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace WindowsTrayInvert.Diagnostics;

/// <summary>
/// Gives memory back to Windows after the window is gone: full compacting GC, then trims the working set so the
/// idle tray process shows only what it really touches.
/// </summary>
public static class MemoryTrimmer
{
    public static void Trim()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        using Process process = Process.GetCurrentProcess();
        // (-1, -1) = remove as many pages as possible from the working set; they fault back in on use.
        if (!SetProcessWorkingSetSize(process.Handle, -1, -1))
            Trace.WriteLine($"SetProcessWorkingSetSize failed: {Marshal.GetLastWin32Error()}");
    }

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);
}

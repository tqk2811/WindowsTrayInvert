using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace WindowsTrayInvert.Tray;

/// <summary>
/// Watches a HKCU key and its whole subtree (subkey added/removed, value changed) using
/// <c>RegNotifyChangeKeyValue</c> on a dedicated background thread.
/// <see cref="Changed"/> is raised on that thread, once per burst of changes (debounced).
/// </summary>
public sealed class RegistryWatcher : IDisposable
{
    private const int RegNotifyChangeName = 0x1;
    private const int RegNotifyChangeLastSet = 0x4;

    private readonly string _keyPath;
    private readonly TimeSpan _debounce;
    private readonly ManualResetEvent _stopEvent = new(false);
    private Thread? _thread;

    public RegistryWatcher(string keyPath, TimeSpan debounce)
    {
        _keyPath = keyPath;
        _debounce = debounce;
    }

    public event EventHandler? Changed;

    /// <summary>Raised on the watcher thread when the watch loop dies; the watcher is stopped afterwards.</summary>
    public event EventHandler<Exception>? Failed;

    public void Start()
    {
        if (_thread is not null)
            throw new InvalidOperationException("Watcher already started.");

        _thread = new Thread(WatchLoop) { IsBackground = true, Name = nameof(RegistryWatcher) };
        _thread.Start();
    }

    public void Dispose()
    {
        _stopEvent.Set();
        _thread?.Join();
        _stopEvent.Dispose();
    }

    private void WatchLoop()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(_keyPath)
                ?? throw new InvalidOperationException($@"Key HKCU\{_keyPath} does not exist.");
            using AutoResetEvent changeEvent = new(false);
            WaitHandle[] handles = [_stopEvent, changeEvent];

            while (true)
            {
                Register(key.Handle, changeEvent);
                if (WaitHandle.WaitAny(handles) == 0)
                    return;

                // Swallow the rest of the burst: re-arm and wait until the key stays quiet for _debounce.
                while (true)
                {
                    Register(key.Handle, changeEvent);
                    int index = WaitHandle.WaitAny(handles, _debounce);
                    if (index == 0)
                        return;
                    if (index == WaitHandle.WaitTimeout)
                        break;
                }

                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Failed?.Invoke(this, ex);
        }
    }

    private static void Register(SafeRegistryHandle key, AutoResetEvent changeEvent)
    {
        int result = RegNotifyChangeKeyValue(
            key, true, RegNotifyChangeName | RegNotifyChangeLastSet,
            changeEvent.SafeWaitHandle, true);
        if (result != 0)
            throw new Win32Exception(result);
    }

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern int RegNotifyChangeKeyValue(
        SafeRegistryHandle hKey, bool bWatchSubtree, int dwNotifyFilter, SafeWaitHandle hEvent, bool fAsynchronous);
}

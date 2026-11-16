using System.Runtime.InteropServices;

namespace Hollowwork.Automation;

public interface IKeySink : IDisposable
{
    void Press(string key);
}

public interface IFocusProbe : IDisposable
{
    bool IsGameFocused(string windowTitle, string windowClass);
}

public sealed class X11KeySink : IKeySink
{
    private readonly IntPtr _display;
    private readonly object _gate = new();

    static X11KeySink()
    {
        if (OperatingSystem.IsLinux())
        {
            XInitThreads();
        }
    }

    public X11KeySink()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("X11 input is only available on Linux.");
        }

        _display = XOpenDisplay(null);
        if (_display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not open the X11 display. Is DISPLAY set?");
        }
    }

    public void Press(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        lock (_gate)
        {
            var keysym = XStringToKeysym(key);
            if (keysym == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Unknown X11 key '{key}'.");
            }

            var keycode = XKeysymToKeycode(_display, keysym);
            if (keycode == 0)
            {
                throw new InvalidOperationException($"No keycode for '{key}'.");
            }

            XTestFakeKeyEvent(_display, keycode, true, 0);
            XTestFakeKeyEvent(_display, keycode, false, 0);
            XFlush(_display);
        }
    }

    public void Dispose()
    {
        if (_display != IntPtr.Zero)
        {
            XCloseDisplay(_display);
        }
    }

    [DllImport("libX11.so.6")]
    private static extern int XInitThreads();

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(string? name);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XStringToKeysym(string name);

    [DllImport("libX11.so.6")]
    private static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libXtst.so.6")]
    private static extern int XTestFakeKeyEvent(IntPtr display, uint keycode, bool isPress, ulong delay);
}

public sealed class X11FocusProbe : IFocusProbe
{
    private readonly IntPtr _display;
    private readonly object _gate = new();

    static X11FocusProbe()
    {
        if (OperatingSystem.IsLinux())
        {
            XInitThreads();
        }
    }

    public X11FocusProbe()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("X11 focus probing is only available on Linux.");
        }

        _display = XOpenDisplay(null);
        if (_display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not open the X11 display. Is DISPLAY set?");
        }
    }

    public bool IsGameFocused(string windowTitle, string windowClass)
    {
        lock (_gate)
        {
            if (XGetInputFocus(_display, out var focus, out _) == 0 || focus.ToInt64() <= 1)
            {
                return false;
            }

            var title = ReadName(focus);
            if (!string.IsNullOrWhiteSpace(windowTitle) &&
                title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (XGetClassHint(_display, focus, out var hint) != 0)
            {
                try
                {
                    var className = hint.ResClass == IntPtr.Zero
                        ? string.Empty
                        : Marshal.PtrToStringAnsi(hint.ResClass) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(windowClass) &&
                        className.Contains(windowClass, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                finally
                {
                    if (hint.ResName != IntPtr.Zero)
                    {
                        XFree(hint.ResName);
                    }

                    if (hint.ResClass != IntPtr.Zero)
                    {
                        XFree(hint.ResClass);
                    }
                }
            }

            return false;
        }
    }

    public void Dispose()
    {
        if (_display != IntPtr.Zero)
        {
            XCloseDisplay(_display);
        }
    }

    private string ReadName(IntPtr window)
    {
        if (XFetchName(_display, window, out var name) == 0 || name == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            return Marshal.PtrToStringAnsi(name) ?? string.Empty;
        }
        finally
        {
            XFree(name);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XClassHint
    {
        public IntPtr ResName;
        public IntPtr ResClass;
    }

    [DllImport("libX11.so.6")]
    private static extern int XInitThreads();

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(string? name);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XGetInputFocus(IntPtr display, out IntPtr focus, out int revertTo);

    [DllImport("libX11.so.6")]
    private static extern int XFetchName(IntPtr display, IntPtr window, out IntPtr name);

    [DllImport("libX11.so.6")]
    private static extern int XGetClassHint(IntPtr display, IntPtr window, out XClassHint hint);

    [DllImport("libX11.so.6")]
    private static extern int XFree(IntPtr data);
}

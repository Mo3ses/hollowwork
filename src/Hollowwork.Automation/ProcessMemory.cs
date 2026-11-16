using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hollowwork.Automation;

public interface IMemoryReader : IDisposable
{
    int ProcessId { get; }

    ulong BaseAddress { get; }

    int ImageSize { get; }

    bool TryRead(ulong address, Span<byte> buffer);
}

public static class ProcessMemoryFactory
{
    public static IMemoryReader Open(AutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (OperatingSystem.IsLinux())
        {
            return LinuxProcessMemory.Open(profile);
        }

        if (OperatingSystem.IsWindows())
        {
            return WindowsProcessMemory.Open(profile);
        }

        throw new PlatformNotSupportedException("Automation memory reading is implemented for Linux and Windows.");
    }
}

internal sealed class LinuxProcessMemory : IMemoryReader
{
    private readonly int _pid;

    private LinuxProcessMemory(int pid, ulong baseAddress, int imageSize)
    {
        _pid = pid;
        BaseAddress = baseAddress;
        ImageSize = imageSize;
    }

    public int ProcessId => _pid;

    public ulong BaseAddress { get; }

    public int ImageSize { get; }

    public static LinuxProcessMemory Open(AutomationProfile profile)
    {
        var pid = FindProcess(profile.ProcessName)
            ?? throw new InvalidOperationException(
                $"Process '{profile.ProcessName}' not found. Start PoE1 through Steam/Proton first.");

        var baseAddress = FindBaseAddress(pid, profile.ProcessName)
            ?? throw new InvalidOperationException(
                $"Could not find the image base for '{profile.ProcessName}' in /proc/{pid}/maps.");

        var header = new byte[0x400];
        if (!ReadNative(pid, baseAddress, header))
        {
            throw new InvalidOperationException("Could not read the PE header from the game process.");
        }

        var peOffset = BitConverter.ToInt32(header, 0x3C);
        if (peOffset <= 0 || peOffset + 0x40 > header.Length ||
            header[peOffset] != (byte)'P' || header[peOffset + 1] != (byte)'E')
        {
            throw new InvalidOperationException("The game image does not expose a PE header.");
        }

        var imageSize = BitConverter.ToInt32(header, peOffset + 24 + 0x38);
        if (imageSize <= 0)
        {
            throw new InvalidOperationException("The game PE header has an invalid SizeOfImage.");
        }

        return new LinuxProcessMemory(pid, baseAddress, imageSize);
    }

    public bool TryRead(ulong address, Span<byte> buffer)
    {
        if (address == 0 || buffer.Length == 0)
        {
            return false;
        }

        return ReadNative(_pid, address, buffer);
    }

    public void Dispose()
    {
    }

    private static int? FindProcess(string processName)
    {
        var wanted = Path.GetFileName(processName);

        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), out var pid))
            {
                continue;
            }

            try
            {
                var raw = File.ReadAllBytes(Path.Combine(directory, "cmdline"));
                if (raw.Length == 0)
                {
                    continue;
                }

                var end = Array.IndexOf(raw, (byte)0);
                var first = System.Text.Encoding.UTF8.GetString(raw, 0, end < 0 ? raw.Length : end);
                if (first.EndsWith(wanted, StringComparison.OrdinalIgnoreCase) ||
                    first.EndsWith(Path.GetFileNameWithoutExtension(wanted), StringComparison.OrdinalIgnoreCase))
                {
                    return pid;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    private static ulong? FindBaseAddress(int pid, string processName)
    {
        try
        {
            foreach (var line in File.ReadLines($"/proc/{pid}/maps"))
            {
                var parts = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6 || parts[2] != "00000000")
                {
                    continue;
                }

                if (!parts[5].EndsWith(processName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dash = parts[0].IndexOf('-');
                if (dash <= 0)
                {
                    continue;
                }

                return ulong.Parse(parts[0][..dash], System.Globalization.NumberStyles.HexNumber);
            }
        }
        catch (IOException)
        {
        }

        return null;
    }

    private static unsafe bool ReadNative(int pid, ulong address, Span<byte> buffer)
    {
        fixed (byte* pointer = buffer)
        {
            var local = new Iovec
            {
                Base = (IntPtr)pointer,
                Length = (nuint)buffer.Length,
            };

            var remote = new Iovec
            {
                Base = (IntPtr)(long)address,
                Length = (nuint)buffer.Length,
            };

            var read = process_vm_readv(pid, ref local, 1, ref remote, 1, 0);
            return read == buffer.Length;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Iovec
    {
        public IntPtr Base;
        public nuint Length;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern nint process_vm_readv(
        int pid,
        ref Iovec local,
        nuint localCount,
        ref Iovec remote,
        nuint remoteCount,
        nuint flags);
}

internal sealed class WindowsProcessMemory : IMemoryReader
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;
    private readonly IntPtr _handle;
    private readonly Process _process;

    private WindowsProcessMemory(Process process, IntPtr handle)
    {
        _process = process;
        _handle = handle;
        ProcessId = process.Id;
        BaseAddress = (ulong)(process.MainModule?.BaseAddress.ToInt64() ?? 0);
        ImageSize = process.MainModule?.ModuleMemorySize ?? 0;
    }

    public int ProcessId { get; }

    public ulong BaseAddress { get; }

    public int ImageSize { get; }

    public static WindowsProcessMemory Open(AutomationProfile profile)
    {
        var name = Path.GetFileNameWithoutExtension(profile.ProcessName);
        var process = Process.GetProcessesByName(name)
            .OrderByDescending(candidate => candidate.MainWindowHandle != IntPtr.Zero)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Process '{profile.ProcessName}' not found.");

        var handle = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, process.Id);
        if (handle == IntPtr.Zero)
        {
            process.Dispose();
            throw new InvalidOperationException("OpenProcess failed. Run Hollowwork at the same privilege level as the game.");
        }

        return new WindowsProcessMemory(process, handle);
    }

    public unsafe bool TryRead(ulong address, Span<byte> buffer)
    {
        if (address == 0 || buffer.Length == 0)
        {
            return false;
        }

        fixed (byte* pointer = buffer)
        {
            return ReadProcessMemory(
                _handle,
                (IntPtr)(long)address,
                (IntPtr)pointer,
                (nuint)buffer.Length,
                out var read) && read == (nuint)buffer.Length;
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
        }

        _process.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool ReadProcessMemory(
        IntPtr process,
        IntPtr address,
        IntPtr buffer,
        nuint size,
        out nuint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

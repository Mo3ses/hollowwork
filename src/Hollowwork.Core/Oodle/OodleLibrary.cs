using System.Reflection;
using System.Runtime.InteropServices;

namespace Hollowwork.Core.Oodle;

/// <summary>
/// Resolves the native Oodle library used by LibBundle3. Hollowwork never ships it:
/// the user provides a compatible <c>liboo2core*.so</c> (Linux) or <c>oo2core*.dll</c> (Windows).
/// </summary>
public static class OodleLibrary
{
    public const string LinuxEnvironmentVariable = "HOLLOWWORK_OODLE";
    public const string WindowsEnvironmentVariable = "HOLLOWWORK_OODLE";

    private static readonly object Gate = new();
    private static readonly List<string> Candidates = [];
    private static bool _registered;
    private static string? _resolvedPath;

    /// <summary>Path of the library that will be used, if it could be found.</summary>
    public static string? ResolvedPath
    {
        get
        {
            lock (Gate)
            {
                return _resolvedPath ?? FindCandidate();
            }
        }
    }

    /// <summary>
    /// Registers the resolver and records a user-provided path. Safe to call repeatedly.
    /// Must run before the first LibBundle3 call.
    /// </summary>
    public static void Configure(string? explicitPath = null)
    {
        lock (Gate)
        {
            foreach (var candidate in BuildCandidates(explicitPath))
            {
                if (!Candidates.Contains(candidate, StringComparer.Ordinal))
                {
                    Candidates.Add(candidate);
                }
            }

            if (_registered)
            {
                return;
            }

            var assembly = typeof(LibBundle3.Index).Assembly;
            NativeLibrary.SetDllImportResolver(assembly, Resolve);
            _registered = true;
        }
    }

    /// <summary>Throws a readable error when no Oodle library can be found.</summary>
    public static void EnsureAvailable()
    {
        Configure();

        lock (Gate)
        {
            var candidate = FindCandidate();
            if (candidate is not null)
            {
                _resolvedPath = candidate;
                return;
            }

            if (NativeLibrary.TryLoad("oo2core", out var handle))
            {
                NativeLibrary.Free(handle);
                return;
            }
        }

        throw new OodleLibraryNotFoundException(
            "Oodle library not found. Hollowwork does not ship it. " +
            $"Set {LinuxEnvironmentVariable} or place liboo2corelinux64.so.9 next to the executable.");
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals("oo2core", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        lock (Gate)
        {
            var candidate = FindCandidate();
            if (candidate is null)
            {
                return IntPtr.Zero;
            }

            _resolvedPath = candidate;
            return NativeLibrary.Load(candidate);
        }
    }

    private static string? FindCandidate()
    {
        foreach (var candidate in Candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildCandidates(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return Path.GetFullPath(explicitPath);
        }

        var environment = Environment.GetEnvironmentVariable(LinuxEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environment))
        {
            yield return Path.GetFullPath(environment);
        }

        var baseDirectory = AppContext.BaseDirectory;
        foreach (var name in PlatformNames())
        {
            yield return Path.Combine(baseDirectory, name);
        }

        var dataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(dataDirectory))
        {
            foreach (var name in PlatformNames())
            {
                yield return Path.Combine(dataDirectory, "Hollowwork", "oo2core", name);
            }
        }
    }

    private static IEnumerable<string> PlatformNames()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return "oo2core.dll";
            yield return "oo2core_9_win64.dll";
            yield return "oo2core_8_win64.dll";
            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            yield return "liboo2corelinux64.so.9";
            yield return "liboo2core.so";
            yield return "liboo2core.so.9";
            yield break;
        }

        yield return "liboo2core.dylib";
    }
}

public sealed class OodleLibraryNotFoundException : Exception
{
    public OodleLibraryNotFoundException(string message) : base(message) { }

    public OodleLibraryNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

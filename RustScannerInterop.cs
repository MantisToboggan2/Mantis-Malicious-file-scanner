using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Mantis_backdoor_scanner;

public sealed class RustScannerInterop
{
    private static readonly object Sync = new();

    private static bool _loaded;
    private static nint _libraryHandle;

    private static BackdoorScanFunction? _scanFile;
    private static BackdoorScanFunction? _scanFolder;
    private static BackdoorScanFunction? _scanZip;
    private static FreeCStringFunction? _freeCString;

    public RustScanResult ScanPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        EnsureLoaded();

        var scanFn = ResolveScanFunction(path);
        var nativePath = Marshal.StringToCoTaskMemUTF8(path);

        try
        {
            var nativeResult = scanFn(nativePath);

            try
            {
                return new RustScanResult(
                    Success: nativeResult.success != 0,
                    FilesScanned: nativeResult.files_scanned,
                    BytesScanned: nativeResult.bytes_scanned,
                    DetectionCount: nativeResult.detection_count,
                    Json: PtrToString(nativeResult.json),
                    Error: PtrToString(nativeResult.error));
            }
            finally
            {
                if (nativeResult.json != nint.Zero)
                {
                    _freeCString!(nativeResult.json);
                }

                if (nativeResult.error != nint.Zero)
                {
                    _freeCString!(nativeResult.error);
                }
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(nativePath);
        }
    }

    private static BackdoorScanFunction ResolveScanFunction(string path)
    {
        if (Directory.Exists(path))
        {
            return _scanFolder!;
        }

        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return _scanZip!;
        }

        return _scanFile!;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (Sync)
        {
            if (_loaded)
            {
                return;
            }

            _libraryHandle = LoadLibraryHandle();
            _scanFile = GetExport<BackdoorScanFunction>("backdoor_scan_file");
            _scanFolder = GetExport<BackdoorScanFunction>("backdoor_scan_folder");
            _scanZip = GetExport<BackdoorScanFunction>("backdoor_scan_zip");
            _freeCString = GetExport<FreeCStringFunction>("backdoor_free_c_string");
            _loaded = true;
        }
    }

    private static T GetExport<T>(string symbol) where T : Delegate
    {
        var ptr = NativeLibrary.GetExport(_libraryHandle, symbol);
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private static nint LoadLibraryHandle()
    {
        var searched = new List<string>();
        foreach (var candidate in GetCandidateLibraryPaths())
        {
            searched.Add(candidate);

            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        if (NativeLibrary.TryLoad("backdoor_scanner", out var fallback))
        {
            return fallback;
        }

        throw new DllNotFoundException($"Could not load backdoor_scanner native library. Searched: {string.Join("; ", searched)}");
    }

    private static IEnumerable<string> GetCandidateLibraryPaths()
    {
        var fileName = GetPlatformLibraryFileName();
        var baseDir = AppContext.BaseDirectory;

        var envPath = Environment.GetEnvironmentVariable("BACKDOOR_SCANNER_NATIVE_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            if (Path.HasExtension(envPath))
            {
                yield return envPath;
            }
            else
            {
                yield return Path.Combine(envPath, fileName);
            }
        }

        yield return Path.Combine(baseDir, fileName);
        yield return Path.Combine(baseDir, "native", fileName);
        yield return Path.Combine(baseDir, "runtimes", GetRuntimeIdentifier(), "native", fileName);

        foreach (var root in EnumerateParentDirectories(baseDir, depth: 6))
        {
            yield return Path.Combine(root, "backdoor_scanner", "target", "debug", fileName);
            yield return Path.Combine(root, "backdoor_scanner", "target", "release", fileName);
            yield return Path.Combine(root, "target", "debug", fileName);
            yield return Path.Combine(root, "target", "release", fileName);
        }
    }

    private static IEnumerable<string> EnumerateParentDirectories(string startDir, int depth)
    {
        var current = Path.GetFullPath(startDir);
        for (var i = 0; i <= depth; i++)
        {
            yield return current;

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                yield break;
            }

            current = parent.FullName;
        }
    }

    private static string GetPlatformLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "backdoor_scanner.dll";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "libbackdoor_scanner.dylib";
        }

        return "libbackdoor_scanner.so";
    }

    private static string GetRuntimeIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => "x64"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"win-{arch}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"osx-{arch}";
        }

        return $"linux-{arch}";
    }

    private static string? PtrToString(nint ptr)
    {
        return ptr == nint.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate RustScanResultNative BackdoorScanFunction(nint path);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeCStringFunction(nint ptr);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RustScanResultNative
    {
        public readonly byte success;
        public readonly uint files_scanned;
        public readonly ulong bytes_scanned;
        public readonly uint detection_count;
        public readonly nint json;
        public readonly nint error;
    }
}

public sealed record RustScanResult(
    bool Success,
    uint FilesScanned,
    ulong BytesScanned,
    uint DetectionCount,
    string? Json,
    string? Error);

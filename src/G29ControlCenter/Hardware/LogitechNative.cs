using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace G29ControlCenter.Hardware;

/// <summary>
/// DirectInput-Eingabestruktur gemäß Logitech Steering Wheel SDK (DIJOYSTATE2ENGINES).
/// Die exakte Feldreihenfolge und Pack=4 stellen ein unverfälschtes Marshaling gegen die x64-Wrapper-DLL sicher.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DIJOYSTATE2ENGINES
{
    public int lX;
    public int lY;
    public int lZ;
    public int lRx;
    public int lRy;
    public int lRz;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] rglSlider;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public uint[] rgdwPOV;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
    public byte[] rgbButtons;

    public int lVX;
    public int lVY;
    public int lVZ;
    public int lVRx;
    public int lVRy;
    public int lVRz;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] rglVSlider;

    public int lAX;
    public int lAY;
    public int lAZ;
    public int lARx;
    public int lARy;
    public int lARz;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] rglASlider;

    public int lFX;
    public int lFY;
    public int lFZ;
    public int lFRx;
    public int lFRy;
    public int lFRz;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] rglFSlider;
}

/// <summary>
/// Aktuelle Treibereinstellungen des Controllers aus dem Logitech Steering Wheel SDK.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct LogiControllerPropertiesData
{
    [MarshalAs(UnmanagedType.I1)]
    public bool forceEnable;

    public int overallGain;
    public int springGain;
    public int damperGain;

    [MarshalAs(UnmanagedType.I1)]
    public bool defaultSpringEnabled;

    public int defaultSpringGain;

    [MarshalAs(UnmanagedType.I1)]
    public bool combinePedals;

    public int wheelRange;

    [MarshalAs(UnmanagedType.I1)]
    public bool gameSettingsEnabled;

    [MarshalAs(UnmanagedType.I1)]
    public bool allowGameSettings;
}

/// <summary>
/// P/Invoke-Deklarationen für das Logitech Steering Wheel SDK (x64).
/// Löst bevorzugt die von Logitech G HUB installierte native Engine (ServerBinary / logi_steering_wheel_x64.dll)
/// auf und fällt optional auf eine lokale LogitechSteeringWheelEnginesWrapper.dll zurück.
/// </summary>
internal static class LogitechNative
{
    public const string WrapperDllName = "LogitechSteeringWheelEnginesWrapper.dll";
    private const string ServerBinarySubKey = @"SOFTWARE\Classes\CLSID\{63BD165D-1584-4E75-AB56-08330350545F}\ServerBinary";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeStateDelegate(IntPtr statePtr);

    private static readonly object _resolverLock = new();
    private static bool _resolverRegistered;
    private static nint _loadedHandle;
    private static FreeStateDelegate? _freeStateEngines;

    public static string? LoadedLibraryPath { get; private set; }
    public static string? LastLoadError { get; private set; }

    static LogitechNative()
    {
        InitializeLibraryResolver();
    }

    public static void InitializeLibraryResolver()
    {
        lock (_resolverLock)
        {
            if (_resolverRegistered)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(LogitechNative).Assembly, ResolveNativeLibrary);
            _resolverRegistered = true;
        }
    }

    public static bool EnsureLoaded(out string diagnosticMessage)
    {
        InitializeLibraryResolver();

        lock (_resolverLock)
        {
            if (_loadedHandle != nint.Zero && !string.IsNullOrWhiteSpace(LoadedLibraryPath))
            {
                diagnosticMessage = LoadedLibraryPath;
                return true;
            }
        }

        nint handle = ResolveNativeLibrary(WrapperDllName, typeof(LogitechNative).Assembly, null);
        if (handle != nint.Zero)
        {
            diagnosticMessage = LoadedLibraryPath ?? WrapperDllName;
            return true;
        }

        diagnosticMessage = LastLoadError ??
            "Weder die Logitech G HUB Engine (logi_steering_wheel_x64.dll) noch eine ladbare Wrapper-DLL konnte geladen werden.";
        return false;
    }

    private static nint ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, WrapperDllName, StringComparison.OrdinalIgnoreCase))
        {
            return nint.Zero;
        }

        lock (_resolverLock)
        {
            if (_loadedHandle != nint.Zero)
            {
                return _loadedHandle;
            }

            foreach (string candidate in EnumerateSdkCandidates())
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                if (NativeLibrary.TryLoad(candidate, out nint handle))
                {
                    _loadedHandle = handle;
                    LoadedLibraryPath = candidate;
                    LastLoadError = null;

                    if (NativeLibrary.TryGetExport(handle, "LogiFreeStateENGINES", out nint freeExport))
                    {
                        _freeStateEngines = Marshal.GetDelegateForFunctionPointer<FreeStateDelegate>(freeExport);
                    }

                    return handle;
                }
            }

            // Letzter Versuch über Standard-Suchpfad für den Fall einer systemweiten Wrapper-DLL
            if (NativeLibrary.TryLoad(WrapperDllName, assembly, searchPath, out nint defaultHandle))
            {
                _loadedHandle = defaultHandle;
                LoadedLibraryPath = WrapperDllName;
                LastLoadError = null;

                if (NativeLibrary.TryGetExport(defaultHandle, "LogiFreeStateENGINES", out nint freeExport))
                {
                    _freeStateEngines = Marshal.GetDelegateForFunctionPointer<FreeStateDelegate>(freeExport);
                }

                return defaultHandle;
            }

            LastLoadError =
                "Logitech Steering Wheel SDK nicht ladbar. Bitte Logitech G HUB installieren oder starten (ServerBinary logi_steering_wheel_x64.dll fehlt).";
            return nint.Zero;
        }
    }

    private static IEnumerable<string> EnumerateSdkCandidates()
    {
        string? regHklm = ReadRegistryServerBinary(RegistryHive.LocalMachine);
        if (!string.IsNullOrWhiteSpace(regHklm))
        {
            yield return regHklm;
        }

        string? regHkcu = ReadRegistryServerBinary(RegistryHive.CurrentUser);
        if (!string.IsNullOrWhiteSpace(regHkcu))
        {
            yield return regHkcu;
        }

        string programFilesWheelSdk = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Logi",
            "wheel_sdk");

        if (Directory.Exists(programFilesWheelSdk))
        {
            List<string> discovered = [];
            try
            {
                foreach (string dir in Directory.GetDirectories(programFilesWheelSdk).OrderByDescending(static d => d, StringComparer.OrdinalIgnoreCase))
                {
                    string dll = Path.Combine(dir, "logi_steering_wheel_x64.dll");
                    if (File.Exists(dll))
                    {
                        discovered.Add(dll);
                    }
                }
            }
            catch
            {
                // Verzeichnisfehler still übergehen
            }

            foreach (string dll in discovered)
            {
                yield return dll;
            }
        }

        yield return Path.Combine(AppContext.BaseDirectory, WrapperDllName);
        yield return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Native", "x64", WrapperDllName));
    }

    private static string? ReadRegistryServerBinary(RegistryHive hive)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using RegistryKey? subKey = baseKey.OpenSubKey(ServerBinarySubKey);
            return subKey?.GetValue(null) as string;
        }
        catch
        {
            return null;
        }
    }

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiSteeringInitializeWithWindow(
        [MarshalAs(UnmanagedType.I1)] bool ignoreXInputControllers,
        nint hwnd);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiSteeringInitialize(
        [MarshalAs(UnmanagedType.I1)] bool ignoreXInputControllers);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void LogiSteeringShutdown();

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiUpdate();

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr LogiGetStateENGINES(int index);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiIsConnected(int index);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiHasForceFeedback(int index);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiGetFriendlyProductName(int index, StringBuilder buffer, int bufferSize);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiGetCurrentControllerProperties(int index, ref LogiControllerPropertiesData properties);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiSetOperatingRange(int index, int range);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiGetOperatingRange(int index, ref int range);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiPlayConstantForce(int index, int magnitudePercentage);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiStopConstantForce(int index);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiPlaySpringForce(
        int index,
        int offsetPercentage,
        int saturationPercentage,
        int coefficientPercentage);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiStopSpringForce(int index);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiPlayDamperForce(int index, int coefficientPercentage);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiStopDamperForce(int index);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiPlayBumpyRoadEffect(int index, int magnitudePercentage);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiStopBumpyRoadEffect(int index);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiPlayDirtRoadEffect(int index, int magnitudePercentage);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiStopDirtRoadEffect(int index);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiPlayFrontalCollisionForce(int index, int magnitudePercentage);

    [DllImport(WrapperDllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LogiPlayLeds(
        int index,
        float currentRpm,
        float rpmFirstLedTurnsOn,
        float rpmRedLine);

    public static bool TryGetState(int index, out DIJOYSTATE2ENGINES state)
    {
        IntPtr ptr = LogiGetStateENGINES(index);
        if (ptr == IntPtr.Zero)
        {
            state = default;
            return false;
        }

        try
        {
            state = Marshal.PtrToStructure<DIJOYSTATE2ENGINES>(ptr);
            return true;
        }
        finally
        {
            _freeStateEngines?.Invoke(ptr);
        }
    }

    [DllImport("winmm.dll", ExactSpelling = true)]
    public static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", ExactSpelling = true)]
    public static extern uint timeEndPeriod(uint uPeriod);
}

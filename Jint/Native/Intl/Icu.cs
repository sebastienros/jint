using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
namespace Jint.Native.Intl;

// ICU (International Components for Unicode) is a native C/C++ library provided by the OS
// that implements BCP-47 locale canonicalization, alias resolution, and other i18n data.
// We use DllImport to bind directly to its functions (e.g. uloc_toLanguageTag) so we can
// reuse the OS-provided ICU implementation instead of reimplementing the spec in C#.
// The wrapper below converts managed strings to UTF-8, calls ICU, and returns the canonical tag.

internal static class ICU
{
    private const string MacLib = "/usr/lib/libicucore.dylib";
    private const string LinuxUc = "icuuc";     // resolves to libicuuc.so[.N]
    private const string LinuxI18n = "icui18n";  // resolves to libicui18n.so[.N]

#if OSX || MACCATALYST || IOS || TVOS
private const string UcLib = MacLib;
private const string I18nLib = MacLib;
#elif LINUX
private const string UcLib = LinuxUc;
private const string I18nLib = LinuxI18n;
#else
    // Windows: prefer bundling (put icuucNN.dll/icuinNN.dll next to your .exe)
    private const string UcLib = "icuuc";   // icuucNN.dll via loader search path
    private const string I18nLib = "icuin"; // icuinNN.dll
#endif

    // ICU error code enum (partial)
    public enum UErrorCode : int
    {
        U_ZERO_ERROR = 0,
        U_ILLEGAL_ARGUMENT_ERROR = 1,
        // ... add more as needed
    }

    [DllImport(UcLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int uloc_countAvailable();

    [DllImport(UcLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr uloc_getAvailable(int n);

    // Example for something in i18n (collation)
    [DllImport(I18nLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int ucol_countAvailable();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string PtrToAnsiString(IntPtr p) => Marshal.PtrToStringAnsi(p)!;


    // Older runtimes: pass IntPtr and pin a UTF-8 buffer manually.
    [DllImport(UcLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uloc_toLanguageTag")]
    private static extern unsafe int uloc_toLanguageTag_ptr(
        byte* localeIdUtf8,                                   // const char* (UTF-8)
        byte[] langtag,                                       // UTF-8 out
        int langtagCapacity,
        [MarshalAs(UnmanagedType.I1)] bool strict,
        ref UErrorCode err);

    public static unsafe int uloc_toLanguageTag(
        string localeId, byte[] langtag, int langtagCapacity, bool strict, ref UErrorCode err)
    {
        // NUL-terminate for C
        byte[] inBytes = Encoding.UTF8.GetBytes(localeId + "\0");
        fixed (byte* p = inBytes)
        {
            return uloc_toLanguageTag_ptr(p, langtag, langtagCapacity, strict, ref err);
        }
    }

    [DllImport(UcLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uloc_forLanguageTag")]
    private static extern unsafe int uloc_forLanguageTag_ptr(
    byte* langtagUtf8,                                   // const char* (UTF-8)
    byte[] localeId,                                     // UTF-8 out
    int localeIdCapacity,
    out int parsedLength,
    ref UErrorCode err);

    public static unsafe int uloc_forLanguageTag(
        string langtag, byte[] localeId, int localeIdCapacity, out int parsedLength, ref UErrorCode err)
    {
        var inBytes = System.Text.Encoding.UTF8.GetBytes(langtag + "\0");
        fixed (byte* p = inBytes)
        {
            return uloc_forLanguageTag_ptr(p, localeId, localeIdCapacity, out parsedLength, ref err);
        }
    }
    [DllImport(UcLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uloc_canonicalize")]
    private static extern unsafe int uloc_canonicalize_ptr(
        byte* localeIdUtf8,       // const char* (UTF-8, NUL-terminated)
        byte[] name,              // out buffer (UTF-8, no trailing NUL guaranteed)
        int nameCapacity,
        ref UErrorCode err);

    public static unsafe int uloc_canonicalize(string localeId, byte[] name, int nameCapacity, ref UErrorCode err)
    {
        // NUL-terminate input for C
        var inBytes = Encoding.UTF8.GetBytes(localeId + "\0");
        fixed (byte* p = inBytes)
        {
            return uloc_canonicalize_ptr(p, name, nameCapacity, ref err);
        }
    }
}


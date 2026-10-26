using System.Runtime.InteropServices;

namespace FreeReturn.Services;

/// <summary>Stops Windows from sleeping while apps are downloading and installing.</summary>
public static partial class KeepAwake
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    public static void Enable() => SetThreadExecutionState(EsContinuous | EsSystemRequired);

    public static void Disable() => SetThreadExecutionState(EsContinuous);

    [LibraryImport("kernel32.dll")]
    private static partial uint SetThreadExecutionState(uint flags);
}

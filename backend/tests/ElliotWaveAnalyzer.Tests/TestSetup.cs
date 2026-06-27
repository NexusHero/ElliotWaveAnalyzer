using System.Runtime.CompilerServices;
using VerifyNUnit;

namespace ElliotWaveAnalyzer.Tests;

/// <summary>
/// One-time Verify configuration. Snapshots live next to the tests under a
/// <c>__snapshots__</c> directory rather than scattered beside the source files.
/// </summary>
internal static class TestSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.UseProjectRelativeDirectory("__snapshots__");
    }
}

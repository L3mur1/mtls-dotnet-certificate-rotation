using System.Runtime.CompilerServices;

namespace MtlsRotationClient;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.IgnoreStackTrace();
        // Opt-in: VERIFY_AUTO=1 accepts new/changed snapshots without DiffEngine UI.
        if (string.Equals(Environment.GetEnvironmentVariable("VERIFY_AUTO"), "1", StringComparison.Ordinal))
        {
            VerifierSettings.AutoVerify();
        }
    }
}
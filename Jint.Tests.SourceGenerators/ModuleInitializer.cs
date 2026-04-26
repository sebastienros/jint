using System.Runtime.CompilerServices;

namespace Jint.Tests.SourceGenerators;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
    }
}

#nullable enable
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Jint.HtmlParser;
using PublicApiGenerator;
using VerifyTests;
using VerifyTests.DiffPlex;
using VerifyNUnit;

namespace Jint.Tests.HtmlParser.Parsing;

/// <summary>Records the implemented Jint.HtmlParser public surface on both shipped frameworks.</summary>
public class PublicApiTest
{
    [Test]
    public async Task PublicApiHasNotChangedUnintentionally()
    {
        var assembly = typeof(Document).Assembly;
        var framework = assembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
            .Cast<TargetFrameworkAttribute>().Single().FrameworkName;
        var targetFramework = framework switch
        {
            ".NETCoreApp,Version=v8.0" => "net8.0",
            ".NETCoreApp,Version=v10.0" => "net10.0",
            _ => throw new InvalidOperationException($"Unexpected parser target framework: {framework}")
        };

        var options = new ApiGeneratorOptions
        {
            ExcludeAttributes =
            [
                "System.Diagnostics.DebuggerDisplayAttribute",
                "System.Reflection.AssemblyMetadataAttribute",
                "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
                "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
                "System.Runtime.CompilerServices.IsReadOnlyAttribute",
                "System.Runtime.CompilerServices.NullableAttribute",
                "System.Runtime.CompilerServices.NullableContextAttribute",
                "System.Runtime.CompilerServices.RefSafetyRulesAttribute",
                "System.Runtime.Versioning.TargetFrameworkAttribute",
            ],
        };

        var publicApi = assembly.GeneratePublicApi(options);
        await Verifier.Verify(publicApi, extension: "txt")
            .UseDirectory("Verify")
            .UseFileName($"PublicApiTest_{targetFramework}")
            .DisableRequireUniquePrefix();
    }
}

internal static class PublicApiDiffFormat
{
    [ModuleInitializer]
    public static void Initialize() => VerifyDiffPlex.Initialize(OutputType.Compact);
}

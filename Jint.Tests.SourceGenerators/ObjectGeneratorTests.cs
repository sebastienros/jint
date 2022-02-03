using static Jint.Tests.SourceGenerators.VerifyHelper;

namespace Jint.Tests.SourceGenerators;

[UsesVerify]
public class ObjectGeneratorTests
{
    [Fact]
    public Task ArrayPrototype()
    {
        return VerifyJintSourceFile("Native/Array/ArrayPrototype.cs");
    }    
    
    [Fact]
    public Task MathInstance()
    {
        return VerifyJintSourceFile("Native/Math/MathInstance.cs");
    }

    private static Task VerifyJintSourceFile(string file)
    {
        var source = File.ReadAllText(ToJintSourcePath(file));
        return Verify(source);
    }

    private static string ToJintSourcePath(string path)
    {
        return "../../../../Jint/" + path;
    }
}

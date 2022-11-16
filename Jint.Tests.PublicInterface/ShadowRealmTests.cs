using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

public class ShadowRealmTests
{
    [Fact]
    public void CanUseViaEngineMethods()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        var shadowRealm = engine.Realm.Intrinsics.ShadowRealm.Construct();

        // lexically scoped (let/const) are visible during single call
        Assert.Equal(123, shadowRealm.Evaluate("const s = 123; const f = () => s; f();"));
        Assert.Equal(true, shadowRealm.Evaluate("typeof f === 'undefined'"));

        // vars hold longer
        Assert.Equal(456, shadowRealm.Evaluate("function foo() { return 456; }; foo();"));
        Assert.Equal(456, shadowRealm.Evaluate("foo();"));

        // not visible in global engine though
        Assert.Equal(true, engine.Evaluate("typeof foo === 'undefined'"));

        // modules
        var importValue = shadowRealm.ImportValue("./modules/format-name.js", "formatName");
        var formatName = (ObjectInstance) importValue.UnwrapIfPromise();
        var result = engine.Invoke(formatName, "John", "Doe").AsString();
        Assert.Equal("John Doe", result);
    }

    private static string GetBasePath()
    {
        var assemblyDirectory = new DirectoryInfo(AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory);

        var current = assemblyDirectory;
        while (current is not null && current.GetDirectories().All(x => x.Name != "Jint.Tests"))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new NullReferenceException($"Could not find tests base path, assemblyPath: {assemblyDirectory}");
        }

        return Path.Combine(current.FullName, "Jint.Tests", "Runtime", "Scripts");
    }
}

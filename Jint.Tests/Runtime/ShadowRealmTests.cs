using Jint.Native.Object;

namespace Jint.Tests.Runtime;

public class ShadowRealmTests
{
    [Fact]
    public void CanUseViaEngineMethods()
    {
        var engine = new Engine(options => options.EnableModules(ModuleTests.GetBasePath()));
        var shadowRealm1 = engine.Realm.Intrinsics.ShadowRealm.Construct();

        // lexically scoped (let/const) are visible during single call
        Assert.Equal(123, shadowRealm1.Evaluate("const s = 123; const f = () => s; f();"));
        Assert.Equal(true, shadowRealm1.Evaluate("typeof f === 'undefined'"));

        // vars hold longer
        Assert.Equal(456, shadowRealm1.Evaluate("function foo() { return 456; }; foo();"));
        Assert.Equal(456, shadowRealm1.Evaluate("foo();"));

        // not visible in global engine though
        Assert.Equal(true, engine.Evaluate("typeof foo === 'undefined'"));

        // modules
        var importValue = shadowRealm1.ImportValue("./modules/format-name.js", "formatName");
        var formatName = (ObjectInstance) importValue.UnwrapIfPromise();
        var result = engine.Invoke(formatName, "John", "Doe").AsString();
        Assert.Equal("John Doe", result);
    }
}

using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

public class ShadowRealmTests
{
    [Fact]
    public void CanUseViaEngineMethods()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

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

    [Fact]
    public void MultipleShadowRealmsDoNotInterfere()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        engine.SetValue("message", "world");
        engine.Evaluate("function hello() {return message}");

        Assert.Equal("world", engine.Evaluate("hello();"));

        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm.SetValue("message", "realm 1");
        shadowRealm.Evaluate("function hello() {return message}");

        var shadowRealm2 = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm2.SetValue("message", "realm 2");
        shadowRealm2.Evaluate("function hello() {return message}");

        // Act & Assert
        Assert.Equal("realm 1", shadowRealm.Evaluate("hello();"));
        Assert.Equal("realm 2", shadowRealm2.Evaluate("hello();"));
    }

    [Fact]
    public void MultipleShadowRealm_SettingGlobalVariable_DoNotInterfere()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        engine.SetValue("message", "hello ");
        engine.Evaluate("(function hello() {message += \"engine\"})();");

        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm.SetValue("message", "hello ");
        shadowRealm.Evaluate("(function hello() {message += \"realm 1\"})();");

        var shadowRealm2 = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm2.SetValue("message", "hello ");
        shadowRealm2.Evaluate("(function hello() {message += \"realm 2\"})();");

        // Act & Assert
        Assert.Equal("hello engine", engine.Evaluate("message"));
        Assert.Equal("hello realm 1", shadowRealm.Evaluate("message"));
        Assert.Equal("hello realm 2", shadowRealm2.Evaluate("message"));
    }

    [Fact]
    public void CanReuseScriptWithShadowRealm()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        engine.SetValue("message", "engine");

        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm.SetValue("message", "realm 1");

        var shadowRealm2 = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm2.SetValue("message", "realm 2");

        var script = Engine.PrepareScript("(function hello() {return \"hello \" + message})();");

        // Act & Assert
        Assert.Equal("hello engine", engine.Evaluate(script));
        Assert.Equal("hello realm 1", shadowRealm.Evaluate(script));
        Assert.Equal("hello realm 2", shadowRealm2.Evaluate(script));
    }

    [Fact]
    public void CanEvaluateScriptWithSuperInsideClassAndObjectMethods()
    {
        // https://github.com/sebastienros/jint/issues/2569
        // PerformShadowRealmEval only forbids super/new.target contained in the script's own body;
        // the Contains static semantics do not descend into nested function or class bodies.
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        Assert.Equal("derived:base", shadowRealm.Evaluate("""
            class Base {
                greet() { return 'base'; }
            }
            class Derived extends Base {
                greet() { return 'derived:' + super.greet(); }
            }
            new Derived().greet();
            """));

        Assert.Equal(3, shadowRealm.Evaluate("""
            class A { constructor() { this.x = 1; } }
            class B extends A { constructor() { super(); this.y = 2; } }
            const b = new B();
            b.x + b.y;
            """));

        Assert.Equal(42, shadowRealm.Evaluate("""
            class C1 { m() { return 40; } }
            class C2 extends C1 { f = super.m() + 2; }
            new C2().f;
            """));

        Assert.Equal(1, shadowRealm.Evaluate("""
            class S1 { static m() { return 1; } }
            class S2 extends S1 { static v; static { S2.v = super.m(); } }
            S2.v;
            """));

        Assert.Equal("[object Object]", shadowRealm.Evaluate("""
            const o = { m() { return super.toString(); } };
            o.m();
            """));

        Assert.Equal(true, shadowRealm.Evaluate("""
            function f() { return new.target === undefined; }
            f();
            """));
    }

    [Fact]
    public void CanEvaluatePreparedScriptWithSuperInsideClassMethods()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        var script = Engine.PrepareScript("class A { hello() { return 'hi'; } } class B extends A { hello() { return super.hello() + '!'; } } new B().hello();");

        Assert.Equal("hi!", shadowRealm.Evaluate(script));
    }

    [Fact]
    public void EvaluateThrowsSyntaxErrorForTopLevelSuperAndNewTarget()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        AssertSyntaxError(shadowRealm, "super.x");
        AssertSyntaxError(shadowRealm, "super();");
        AssertSyntaxError(shadowRealm, "new.target");

        // arrow functions are transparent for super/new.target
        AssertSyntaxError(shadowRealm, "const f = () => super.x;");
        AssertSyntaxError(shadowRealm, "const f = () => new.target;");
    }

    private static void AssertSyntaxError(Native.ShadowRealm.ShadowRealm shadowRealm, string code)
    {
        var ex = Assert.Throws<JavaScriptException>(() => shadowRealm.Evaluate(code));
        Assert.Equal("SyntaxError", ex.Error.AsObject().Get("name").ToString());
    }

    private static string GetBasePath()
    {
        var assemblyDirectory = new DirectoryInfo(AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory);

        var current = assemblyDirectory;
        var binDirectory = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        while (current is not null)
        {
            if (current.FullName.Contains(binDirectory) || current.Name == "bin")
            {
                current = current.Parent;
                continue;
            }

            var testDirectory = current.GetDirectories("Jint.Tests").FirstOrDefault();
            if (testDirectory == null)
            {
                current = current.Parent;
                continue;
            }

            // found it
            current = testDirectory;
            break;
        }

        if (current is null)
        {
            throw new NullReferenceException($"Could not find tests base path, assemblyPath: {assemblyDirectory}");
        }

        return Path.Combine(current.FullName, "Runtime", "Scripts");
    }
}

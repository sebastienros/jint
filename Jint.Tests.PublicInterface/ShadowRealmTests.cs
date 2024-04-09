using Jint.Native.Object;

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

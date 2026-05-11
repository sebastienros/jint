#if !NETFRAMEWORK

using System.Text;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class EngineLimitTests
{

#if RELEASE
    const int FunctionNestingCount = 990;
#else
    const int FunctionNestingCount = 465;
#endif

    [Fact]
    public void ShouldAllowReasonableCallStackDepth()
    {
        if (OperatingSystem.IsMacOS())
        {
            // stack limit differ quite a lot
            return;
        }

        var script = GenerateCallTree(FunctionNestingCount);

        var engine = new Engine();
        engine.Execute(script);
        Assert.Equal(123, engine.Evaluate("func1(123);").AsNumber());
        Assert.Equal(FunctionNestingCount, engine.Evaluate("x").AsNumber());
    }

    [Fact]
    public void ShouldNotStackoverflowWhenStackGuardEnable()
    {
        // Can be more than actual dotnet stacktrace count, It does not hit stackoverflow anymore.
        int functionNestingCount = FunctionNestingCount * 2;

        var script = GenerateCallTree(functionNestingCount);

        var engine = new Engine(option => option.Constraints.MaxExecutionStackCount = functionNestingCount);
        engine.Execute(script);
        Assert.Equal(123, engine.Evaluate("func1(123);").AsNumber());
        Assert.Equal(functionNestingCount, engine.Evaluate("x").AsNumber());
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenStackGuardExceed()
    {
        // Can be more than actual dotnet stacktrace count, It does not hit stackoverflow anymore.
        int functionNestingCount = FunctionNestingCount * 2;

        var script = GenerateCallTree(functionNestingCount);

        var engine = new Engine(option => option.Constraints.MaxExecutionStackCount = 500);
        try
        {
            engine.Execute(script);
            engine.Evaluate("func1(123);");
        }
        catch (JavaScriptException jsException)
        {
            Assert.Equal("Maximum call stack size exceeded", jsException.Message);
        }
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenConstructorStackGuardExceeds()
    {
        var script = GenerateConstructorChain(static i => $"return new C{i + 1}();", "new C0();");

        AssertMaximumCallStackExceeded(script);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenSuperConstructorStackGuardExceeds()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"class C{ConstructorChainCount} {{ constructor() {{ }} }}");
        for (var i = ConstructorChainCount - 1; i >= 0; i--)
        {
            sb.Append("class C").Append(i).Append(" extends C").Append(i + 1)
                .AppendLine(" { constructor() { super(); } }");
        }
        sb.AppendLine("new C0();");

        AssertMaximumCallStackExceeded(sb.ToString());
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenReflectConstructStackGuardExceeds()
    {
        var script = GenerateConstructorChain(static i => $"return Reflect.construct(C{i + 1}, []);", "new C0();");

        AssertMaximumCallStackExceeded(script);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenBoundConstructorStackGuardExceeds()
    {
        var script = GenerateConstructorChain(
            static i => $"return new B{i + 1}();",
            "new B0();",
            afterConstructors: sb =>
            {
                for (var i = 0; i <= ConstructorChainCount; i++)
                {
                    sb.Append("const B").Append(i).Append(" = C").Append(i).AppendLine(".bind(null);");
                }
            });

        AssertMaximumCallStackExceeded(script);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenProxyConstructorStackGuardExceeds()
    {
        var script = GenerateConstructorChain(
            static i => $"return new P{i + 1}();",
            "new P0();",
            afterConstructors: sb =>
            {
                for (var i = 0; i <= ConstructorChainCount; i++)
                {
                    sb.Append("const P").Append(i).Append(" = new Proxy(C").Append(i).AppendLine(", {});");
                }
            });

        AssertMaximumCallStackExceeded(script);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenBoundConstructorWrapperDepthExceeds()
    {
        AssertMaximumCallStackExceeded(
            """
            let f = function() {};
            for (let i = 0; i < 32; i++) {
              f = f.bind(null);
            }
            new f();
            """,
            static options => options.Constraints.MaxBuiltInRecursionDepth = ConstructorStackGuardLimit);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenProxyConstructorWrapperDepthExceeds()
    {
        AssertMaximumCallStackExceeded(
            """
            let f = function() {};
            for (let i = 0; i < 32; i++) {
              f = new Proxy(f, {});
            }
            new f();
            """,
            static options => options.Constraints.MaxBuiltInRecursionDepth = ConstructorStackGuardLimit);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenProxyCallWrapperDepthExceeds()
    {
        AssertMaximumCallStackExceeded(
            """
            let f = function() { return 1; };
            for (let i = 0; i < 32; i++) {
              f = new Proxy(f, {});
            }
            f();
            """,
            static options => options.Constraints.MaxBuiltInRecursionDepth = ConstructorStackGuardLimit);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenReflectApplyProxyWrapperDepthExceeds()
    {
        AssertMaximumCallStackExceeded(
            """
            let f = function() { return 1; };
            for (let i = 0; i < 32; i++) {
              f = new Proxy(f, {});
            }
            Reflect.apply(f, undefined, []);
            """,
            static options => options.Constraints.MaxBuiltInRecursionDepth = ConstructorStackGuardLimit);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenNativeFunctionCallWrapperDepthExceeds()
    {
        var engine = new Engine(static options =>
        {
            options.Constraints.MaxBuiltInRecursionDepth = ConstructorStackGuardLimit;
        });

        ClrFunction function = null!;
        function = new ClrFunction(
            engine,
            "recurse",
            (_, _) => ((ICallable) function).Call(JsValue.Undefined, Arguments.Empty));

        var exception = Assert.Throws<JavaScriptException>(
            () => ((ICallable) function).Call(JsValue.Undefined, Arguments.Empty));

        Assert.Equal("Maximum call stack size exceeded", exception.Message);
    }

    [Fact]
    public void ShouldAllowNativeCallsWhenBuiltInRecursionGuardIsDisabled()
    {
        var engine = new Engine(static options =>
        {
            options.Constraints.MaxBuiltInRecursionDepth = -1;
        });

        var result = engine.Evaluate("""
            const applyResult = (function (value) { return value; }).apply(null, [1]);

            const proxy = new Proxy(function (value) { return value + 1; }, {});
            const proxyResult = proxy(1);

            function C(value) { this.value = value; }
            const Bound = C.bind(null, 3);
            const boundResult = new Bound().value;

            applyResult + proxyResult + boundResult;
            """);

        Assert.Equal(6, result.AsNumber());
    }

    [Fact]
    public void ShouldThrowCyclicReferenceWhenJsonCycleIsReachedAtBuiltInRecursionLimit()
    {
        var engine = new Engine(static options =>
        {
            options.Constraints.MaxBuiltInRecursionDepth = ConstructorStackGuardLimit;
        });

        var script = """
            const root = {};
            let node = root;
            for (let i = 1; i < LIMIT; i++) {
                node.next = {};
                node = node.next;
            }
            node.next = root;
            JSON.stringify(root);
            """.Replace("LIMIT", ConstructorStackGuardLimit.ToString());

        var exception = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
        Assert.Equal("Cyclic reference detected.", exception.Message);
    }

    [Fact]
    public void ShouldThrowRangeErrorWhenJsonDepthExceedsBuiltInRecursionLimit()
    {
        var engine = new Engine(static options =>
        {
            options.Constraints.MaxBuiltInRecursionDepth = ConstructorStackGuardLimit;
        });

        var script = """
            const root = {};
            let node = root;
            for (let i = 0; i <= LIMIT; i++) {
                node.next = {};
                node = node.next;
            }
            JSON.stringify(root);
            """.Replace("LIMIT", ConstructorStackGuardLimit.ToString());

        var exception = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
        Assert.Equal($"JSON serialization exceeds maximum depth of {ConstructorStackGuardLimit}", exception.Message);
        Assert.True(exception.Error.InstanceofOperator(engine.Intrinsics.RangeError));
    }

    [Fact]
    public void ShouldTreatArrayToLocaleStringCyclesAsEmptyWhenCycleIsReachedAtBuiltInRecursionLimit()
    {
        var engine = new Engine(static options =>
        {
            options.Constraints.MaxBuiltInRecursionDepth = 1;
        });

        var result = engine.Evaluate("""
            const a = [];
            a[0] = a;
            a.toLocaleString();
            """);

        Assert.Equal("", result.AsString());
    }

    [Fact]
    public void ShouldThrowRangeErrorWhenArrayToLocaleStringDepthExceedsBuiltInRecursionLimit()
    {
        var engine = new Engine(static options =>
        {
            options.Constraints.MaxBuiltInRecursionDepth = 1;
        });

        var exception = Assert.Throws<JavaScriptException>(() => engine.Execute("""
            const a = [[1]];
            a.toLocaleString();
            """));

        Assert.Equal("Array string conversion exceeds maximum depth of 1", exception.Message);
        Assert.True(exception.Error.InstanceofOperator(engine.Intrinsics.RangeError));
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenArrayConstructorStackGuardExceeds()
    {
        var fromScript = GenerateConstructorChain(static i => $"return new C{i + 1}();", "Array.from.call(C0, [1]);");
        var ofScript = GenerateConstructorChain(static i => $"return new C{i + 1}();", "Array.of.call(C0, 1);");

        AssertMaximumCallStackExceeded(fromScript);
        AssertMaximumCallStackExceeded(ofScript);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenTypedArrayConstructorStackGuardExceeds()
    {
        var script = GenerateConstructorChain(static i => $"return new C{i + 1}();", "Uint8Array.from.call(C0, [1]);");

        AssertMaximumCallStackExceeded(script);
    }

    [Fact]
    public void ShouldThrowJavascriptExceptionWhenPromiseConstructorStackGuardExceeds()
    {
        var script = GenerateConstructorChain(static i => $"return new C{i + 1}();", "Promise.resolve.call(C0, 1);");

        AssertMaximumCallStackExceeded(script);
    }

    private string GenerateCallTree(int functionNestingCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("var x = 1;");
        sb.AppendLine();
        for (var i = 1; i <= functionNestingCount; ++i)
        {
            sb.Append("function func").Append(i).Append("(func").Append(i).AppendLine("Param) {");
            sb.Append("    ");
            if (i != functionNestingCount)
            {
                // just to create a bit more nesting add some constructs
                sb.Append("return x++ >= 1 ? func").Append(i + 1).Append("(func").Append(i).AppendLine("Param): undefined;");
            }
            else
            {
                // use known CLR function to add breakpoint
                sb.Append("return Math.max(0, func").Append(i).AppendLine("Param);");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private const int ConstructorChainCount = 32;
    private const int ConstructorStackGuardLimit = 10;

    private static string GenerateConstructorChain(
        Func<int, string> recursiveBody,
        string entry,
        Action<StringBuilder> afterConstructors = null)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < ConstructorChainCount; i++)
        {
            sb.Append("function C").Append(i).Append("() { ")
                .Append(recursiveBody(i))
                .AppendLine(" }");
        }
        sb.Append("function C").Append(ConstructorChainCount).AppendLine("() { }");
        afterConstructors?.Invoke(sb);
        sb.AppendLine(entry);
        return sb.ToString();
    }

    private static void AssertMaximumCallStackExceeded(string script)
    {
        AssertMaximumCallStackExceeded(
            script,
            static options => options.Constraints.MaxExecutionStackCount = ConstructorStackGuardLimit);
    }

    private static void AssertMaximumCallStackExceeded(string script, Action<Options> configure)
    {
        var engine = new Engine(options =>
        {
            configure(options);
        });

        var exception = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
        Assert.Equal("Maximum call stack size exceeded", exception.Message);
    }
}

#endif

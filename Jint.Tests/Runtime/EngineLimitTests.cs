#if !NETFRAMEWORK
#nullable enable

using System.Runtime.ExceptionServices;
using System.Text;
using Jint.Runtime;

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
        // A default engine has no stack guard (MaxExecutionStackCount is disabled), so this nesting
        // depth runs directly against the native stack. The test runner's worker thread has an
        // unpredictable amount of stack left, which made this test crash the process intermittently
        // (0xC00000FD) — run on a dedicated thread with an explicit stack instead. The explicit stack
        // also makes the test platform-independent (the old macOS skip is no longer needed). The size
        // is deliberately generous: StackOverflowException is uncatchable and would kill the whole test
        // process, so this is a functional smoke test of deep call chains, not a per-frame native-stack
        // budget test (platforms differ too much in frame size for a tight budget to be safe).
        RunOnDedicatedThread(static () =>
        {
            var script = GenerateCallTree(FunctionNestingCount);

            var engine = new Engine();
            engine.Execute(script);
            engine.Evaluate("func1(123);").AsNumber().Should().Be(123);
            engine.Evaluate("x").AsNumber().Should().Be(FunctionNestingCount);
        });
    }

    private static void RunOnDedicatedThread(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(e);
            }
        }, maxStackSize: 16 * 1024 * 1024);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }

    [Fact]
    public void ShouldNotStackoverflowWhenStackGuardEnable()
    {
        // Can be more than actual dotnet stacktrace count, It does not hit stackoverflow anymore.
        int functionNestingCount = FunctionNestingCount * 2;

        var script = GenerateCallTree(functionNestingCount);

        var engine = new Engine(option => option.Constraints.MaxExecutionStackCount = functionNestingCount);
        engine.Execute(script);
        engine.Evaluate("func1(123);").AsNumber().Should().Be(123);
        engine.Evaluate("x").AsNumber().Should().Be(functionNestingCount);
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
            jsException.Message.Should().Be("Maximum call stack size exceeded");
        }
    }

    private static string GenerateCallTree(int functionNestingCount)
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
}

#endif

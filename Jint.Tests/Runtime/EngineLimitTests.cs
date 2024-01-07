#if !NETFRAMEWORK

using System.Text;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class EngineLimitTests
{

#if RELEASE
    const int FunctionNestingCount = 990;
#else
    const int FunctionNestingCount = 495;
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
}

#endif

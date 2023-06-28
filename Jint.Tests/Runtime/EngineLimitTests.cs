#if !NETFRAMEWORK

using System.Text;

namespace Jint.Tests.Runtime;

public class EngineLimitTests
{
    [Fact]
    public void ShouldAllowReasonableCallStackDepth()
    {
        if (OperatingSystem.IsMacOS())
        {
            // stack limit differ quite a lot
            return;
        }

#if RELEASE
        const int FunctionNestingCount = 960;
#else
        const int FunctionNestingCount = 520;
#endif

        // generate call tree
        var sb = new StringBuilder();
        sb.AppendLine("var x = 10;");
        sb.AppendLine();
        for (var i = 1; i <= FunctionNestingCount; ++i)
        {
            sb.Append("function func").Append(i).Append("(func").Append(i).AppendLine("Param) {");
            sb.Append("    ");
            if (i != FunctionNestingCount)
            {
                // just to create a bit more nesting add some constructs
                sb.Append("return x++ > 1 ? func").Append(i + 1).Append("(func").Append(i).AppendLine("Param): undefined;");
            }
            else
            {
                // use known CLR function to add breakpoint
                sb.Append("return Math.max(0, func").Append(i).AppendLine("Param);");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        var engine = new Engine();
        engine.Execute(sb.ToString());
        Assert.Equal(123, engine.Evaluate("func1(123);").AsNumber());
    }

    [Fact]
    public void ShouldNotStackoverflowWhenStackGuardEnable()
    {
        // Can be more than 1000, It does not hit stackoverflow anymore.
        const int FunctionNestingCount = 1000;

        // generate call tree
        var sb = new StringBuilder();
        sb.AppendLine("var x = 1;");
        sb.AppendLine();
        for (var i = 1; i <= FunctionNestingCount; ++i)
        {
            sb.Append("function func").Append(i).Append("(func").Append(i).AppendLine("Param) {");
            sb.Append("    ");
            if (i != FunctionNestingCount)
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

        var engine = new Engine(option => option.Constraints.MaxExecutionStackCount = 1000);
        engine.Execute(sb.ToString());
        Assert.Equal(123, engine.Evaluate("func1(123);").AsNumber());
        Assert.Equal(FunctionNestingCount, engine.Evaluate("x").AsNumber());
    }
}

#endif

#if !NETFRAMEWORK

using System.Text;

namespace Jint.Tests.Runtime;

public class EngineLimitTests
{
    [Fact]
    public void ShouldAllowReasonableCallStackDepth()
    {
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
}

#endif

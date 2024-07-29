using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public static class TestHelpers
{
    public static bool IsLiteral(this Node node, string requiredValue = null)
    {
        return node switch
        {
            Directive directive => requiredValue == null || directive.Value == requiredValue,
            NonSpecialExpressionStatement expr => requiredValue == null || (expr.Expression is StringLiteral literal && literal.Value == requiredValue),
            _ => false
        };
    }

    public static bool ReachedLiteral(this DebugInformation info, string requiredValue)
    {
        return info.CurrentNode.IsLiteral(requiredValue);
    }

    /// <summary>
    /// Initializes engine in debugmode and executes script until debugger statement,
    /// before calling stepHandler for assertions. Also asserts that a break was triggered.
    /// </summary>
    /// <param name="initialization">Action to initialize and execute scripts</param>
    /// <param name="breakHandler">Handler for assertions</param>
    public static void TestAtBreak(Action<Engine> initialization, Action<Engine, DebugInformation> breakHandler)
    {
        var engine = new Engine(options => options
            .DebugMode()
            .DebuggerStatementHandling(DebuggerStatementHandling.Script)
        );

        bool didBreak = false;
        engine.Debugger.Break += (sender, info) =>
        {
            didBreak = true;
            breakHandler(sender as Engine, info);
            return StepMode.None;
        };

        initialization(engine);

        Assert.True(didBreak, "Test script did not break (e.g. didn't reach debugger statement)");
    }

    /// <inheritdoc cref="TestAtBreak()"/>
    public static void TestAtBreak(Action<Engine> initialization, Action<DebugInformation> breakHandler)
    {
        TestAtBreak(engine => initialization(engine), (engine, info) => breakHandler(info));
    }

    /// <summary>
    /// Initializes engine in debugmode and executes script until debugger statement,
    /// before calling stepHandler for assertions. Also asserts that a break was triggered.
    /// </summary>
    /// <param name="script">Script that is basis for testing</param>
    /// <param name="breakHandler">Handler for assertions</param>
    public static void TestAtBreak(string script, Action<Engine, DebugInformation> breakHandler)
    {
        TestAtBreak(engine => engine.Execute(script), breakHandler);
    }

    /// <inheritdoc cref="TestAtBreak()"/>
    public static void TestAtBreak(string script, Action<DebugInformation> breakHandler)
    {
        TestAtBreak(script, (engine, info) => breakHandler(info));
    }
}
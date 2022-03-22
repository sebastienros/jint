using Esprima.Ast;
using Jint.Runtime.Debugger;
using System;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public static class TestHelpers
    {
        public static bool IsLiteral(this Node node, string requiredValue = null)
        {
            switch (node)
            {
                case Directive directive:
                    return requiredValue == null || directive.Directiv == requiredValue;
                case ExpressionStatement expr:
                    return requiredValue == null || (expr.Expression is Literal literal && literal.StringValue == requiredValue);
            }

            return false;
        }

        public static bool ReachedLiteral(this DebugInformation info, string requiredValue)
        {
            return info.CurrentNode.IsLiteral(requiredValue);
        }

        /// <summary>
        /// Initializes engine in debugmode and executes script until debugger statement,
        /// before calling stepHandler for assertions. Also asserts that a break was triggered.
        /// </summary>
        /// <param name="script">Script that is basis for testing</param>
        /// <param name="breakHandler">Handler for assertions</param>
        public static void TestAtBreak(string script, Action<Engine, DebugInformation> breakHandler)
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Script)
            );

            bool didBreak = false;
            engine.DebugHandler.Break += (sender, info) =>
            {
                didBreak = true;
                breakHandler(sender as Engine, info);
                return StepMode.None;
            };

            engine.Execute(script);

            Assert.True(didBreak, "Test script did not break (e.g. didn't reach debugger statement)");
        }

        /// <inheritdoc cref="TestAtBreak()"/>
        public static void TestAtBreak(string script, Action<DebugInformation> breakHandler)
        {
            TestAtBreak(script, (engine, info) => breakHandler(info));
        }
    }
}

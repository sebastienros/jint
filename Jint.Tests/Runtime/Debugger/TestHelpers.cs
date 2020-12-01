using Esprima.Ast;
using Jint.Runtime.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public static class TestHelpers
    {
        public static bool IsLiteral(this Statement statement, string requiredValue = null)
        {
            switch (statement)
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
            return info.CurrentStatement.IsLiteral(requiredValue);
        }

        /// <summary>
        /// Initializes engine in debugmode and executes script until debugger statement,
        /// before calling stepHandler for assertions. Also asserts that a break was triggered.
        /// </summary>
        /// <param name="script">Script that is basis for testing</param>
        /// <param name="breakHandler">Handler for assertions</param>
        public static void TestAtBreak(string script, Action<DebugInformation> breakHandler)
        {
            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint)
            );

            bool didBreak = false;
            engine.Break += (sender, info) =>
            {
                didBreak = true;
                breakHandler(info);
                return StepMode.None;
            };

            engine.Execute(script);

            Assert.True(didBreak);
        }


    }
}

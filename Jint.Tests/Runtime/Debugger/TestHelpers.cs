using Esprima.Ast;
using Jint.Runtime.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    }
}

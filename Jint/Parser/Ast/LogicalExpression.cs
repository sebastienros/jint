using System;

namespace Jint.Parser.Ast
{
    public enum LogicalOperator
    {
        LogicalAnd,
        LogicalOr
    }

    public class LogicalExpression : Expression
    {
        public LogicalOperator Operator;
        public Expression Left;
        public Expression Right;
        
        public static LogicalOperator ParseLogicalOperator(string op)
        {
            switch (op)
            {
                case "&&":
                    return LogicalOperator.LogicalAnd;
                case "||":
                    return LogicalOperator.LogicalOr;

                default:
                    throw new ArgumentOutOfRangeException("Invalid binary operator: " + op);
            }
        }
    }
}
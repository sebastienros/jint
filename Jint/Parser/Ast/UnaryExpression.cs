using System;

namespace Jint.Parser.Ast
{
    public enum UnaryOperator
    {
        Plus,
        Minus,
        BitwiseNot,
        LogicalNot,
        Delete,
        Void,
        TypeOf,
        Increment,
        Decrement,
    }

    public class UnaryExpression : Expression
    {
        public UnaryOperator Operator;
        public Expression Argument;
        public bool Prefix;

        public static UnaryOperator ParseUnaryOperator(string op)
        {
            switch (op)
            {
                case "+":
                    return UnaryOperator.Plus;
                case "-":
                    return UnaryOperator.Minus;
                case "++":
                    return UnaryOperator.Increment;
                case "--":
                    return UnaryOperator.Decrement;
                case "~":
                    return UnaryOperator.BitwiseNot;
                case "!":
                    return UnaryOperator.LogicalNot;
                case "delete":
                    return UnaryOperator.Delete;
                case "void":
                    return UnaryOperator.Void;
                case "typeof":
                    return UnaryOperator.TypeOf;

                default:
                    throw new ArgumentOutOfRangeException("Invalid unary operator: " + op);

            }
        }

        public static string GetUnaryOperatorAsString(UnaryOperator op)
        {
            switch (op)
            {
                case UnaryOperator.Plus:
                    return "+";
                case UnaryOperator.Minus:
                    return "-";
                case UnaryOperator.Increment:
                    return "++";
                case UnaryOperator.Decrement:
                    return "--";
                case UnaryOperator.BitwiseNot:
                    return "~";
                case UnaryOperator.LogicalNot:
                    return "!";
                case UnaryOperator.Delete:
                    return "delete";
                case UnaryOperator.Void:
                    return "void";
                case UnaryOperator.TypeOf:
                    return "typeof";
            }
            return null;
        }
    }
}
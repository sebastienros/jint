using System;

namespace Jint.Parser.Ast
{
    public enum BinaryOperator
    {
        Plus,
        Minus,
        Times,
        Divide,
        Modulo,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        StrictlyEqual,
        StricltyNotEqual,
        BitwiseAnd,
        BitwiseOr,
        BitwiseXOr,
        LeftShift,
        RightShift,
        UnsignedRightShift,
        InstanceOf,
        In,
    }

    public class BinaryExpression : Expression
    {
        public BinaryOperator Operator;
        public Expression Left;
        public Expression Right;

        public static BinaryOperator ParseBinaryOperator(string op)
        {
            switch (op)
            {
                case "+":
                    return BinaryOperator.Plus;
                case "-":
                    return BinaryOperator.Minus;
                case "*":
                    return BinaryOperator.Times;
                case "/":
                    return BinaryOperator.Divide;
                case "%":
                    return BinaryOperator.Modulo;
                case "==":
                    return BinaryOperator.Equal;
                case "!=":
                    return BinaryOperator.NotEqual;
                case ">":
                    return BinaryOperator.Greater;
                case ">=":
                    return BinaryOperator.GreaterOrEqual;
                case "<":
                    return BinaryOperator.Less;
                case "<=":
                    return BinaryOperator.LessOrEqual;
                case "===":
                    return BinaryOperator.StrictlyEqual;
                case "!==":
                    return BinaryOperator.StricltyNotEqual;
                case "&":
                    return BinaryOperator.BitwiseAnd;
                case "|":
                    return BinaryOperator.BitwiseOr;
                case "^":
                    return BinaryOperator.BitwiseXOr;
                case "<<":
                    return BinaryOperator.LeftShift;
                case ">>":
                    return BinaryOperator.RightShift;
                case ">>>":
                    return BinaryOperator.UnsignedRightShift;
                case "instanceof":
                    return BinaryOperator.InstanceOf;
                case "in":
                    return BinaryOperator.In;

                default: 
                    throw new ArgumentOutOfRangeException("Invalid binary operator: " + op);
            }
        }

        public static string GetBinaryOperatorAsString(BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Plus:
                    return "+";
                case BinaryOperator.Minus:
                    return "-";
                case BinaryOperator.Times:
                    return "*";
                case BinaryOperator.Divide:
                    return "/";
                case BinaryOperator.Modulo:
                    return "%";
                case BinaryOperator.Equal:
                    return "==";
                case BinaryOperator.NotEqual:
                    return "!=";
                case BinaryOperator.Greater:
                    return ">";
                case BinaryOperator.GreaterOrEqual:
                    return ">=";
                case BinaryOperator.Less:
                    return "<";
                case BinaryOperator.LessOrEqual:
                    return "<=";
                case BinaryOperator.StrictlyEqual:
                    return "===";
                case BinaryOperator.StricltyNotEqual:
                    return "!==";
                case BinaryOperator.BitwiseAnd:
                    return "&";
                case BinaryOperator.BitwiseOr:
                    return "|";
                case BinaryOperator.BitwiseXOr:
                    return "^";
                case BinaryOperator.LeftShift:
                    return "<<";
                case BinaryOperator.RightShift:
                    return ">>";
                case BinaryOperator.UnsignedRightShift:
                    return ">>>";
                case BinaryOperator.InstanceOf:
                    return "instanceof";
                case BinaryOperator.In:
                    return "in";

            }
            return null;
        }
    }
}
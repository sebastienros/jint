using System;

namespace Jint.Parser.Ast
{
    public enum AssignmentOperator
    {
        Assign,
        PlusAssign,
        MinusAssign,
        TimesAssign,
        DivideAssign,
        ModuloAssign,
        BitwiseAndAssign,
        BitwiseOrAssign,
        BitwiseXOrAssign,
        LeftShiftAssign,
        RightShiftAssign,
        UnsignedRightShiftAssign,
    }

    public class AssignmentExpression : Expression
    {
        public AssignmentOperator Operator;
        public Expression Left;
        public Expression Right;

        public static AssignmentOperator ParseAssignmentOperator(string op)
        {
            switch (op)
            {
                case "=":
                    return AssignmentOperator.Assign;
                case "+=":
                    return AssignmentOperator.PlusAssign;
                case "-=":
                    return AssignmentOperator.MinusAssign;
                case "*=":
                    return AssignmentOperator.TimesAssign;
                case "/=":
                    return AssignmentOperator.DivideAssign;
                case "%=":
                    return AssignmentOperator.ModuloAssign;
                case "&=":
                    return AssignmentOperator.BitwiseAndAssign;
                case "|=":
                    return AssignmentOperator.BitwiseOrAssign;
                case "^=":
                    return AssignmentOperator.BitwiseXOrAssign;
                case "<<=":
                    return AssignmentOperator.LeftShiftAssign;
                case ">>=":
                    return AssignmentOperator.RightShiftAssign;
                case ">>>=":
                    return AssignmentOperator.UnsignedRightShiftAssign;

                default:
                    throw new ArgumentOutOfRangeException("Invalid assignment operator: " + op);
            }
        }

        public static string GetAssignOperatorAsString(AssignmentOperator op)
        {
            switch (op)
            {
                case AssignmentOperator.Assign:
                    return "=";
                case AssignmentOperator.PlusAssign:
                    return "+=";
                case AssignmentOperator.MinusAssign:
                    return "-=";
                case AssignmentOperator.TimesAssign:
                    return "*=";
                case AssignmentOperator.DivideAssign:
                    return "/=";
                case AssignmentOperator.ModuloAssign:
                    return "%=";
                case AssignmentOperator.BitwiseAndAssign:
                    return "&=";
                case AssignmentOperator.BitwiseOrAssign:
                    return "|=";
                case AssignmentOperator.BitwiseXOrAssign:
                    return "^=";
                case AssignmentOperator.LeftShiftAssign:
                    return "<<=";
                case AssignmentOperator.RightShiftAssign:
                    return ">>=";
                case AssignmentOperator.UnsignedRightShiftAssign:
                    return ">>>=";

            }
            return null;
        }
    }
}
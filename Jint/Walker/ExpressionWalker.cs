using System;
using Jint.Parser.Ast;

namespace Jint.Walker
{
    public class ExpressionWalker
    {
        private readonly JintWalker _jintWalker;

        internal ExpressionWalker(JintWalker jintWalker)
        {
            _jintWalker = jintWalker;
        }

        internal void WalkArrayExpression(ArrayExpression arrayExpression)
        {
            foreach (Expression expr in arrayExpression.Elements)
            {
                _jintWalker.WalkExpression(expr);
            }
        }

        internal void WalkBinaryExpression(BinaryExpression expression)
        {
            _jintWalker.WalkExpression(expression.Left);
            _jintWalker.WalkExpression(expression.Right);
        }

        internal void WalkCallExpression(CallExpression callExpression)
        {
            _jintWalker.WalkExpression(callExpression.Callee);
            foreach (Expression arg in callExpression.Arguments)
            {
                _jintWalker.WalkExpression(arg);
            }
        }

        internal void WalkConditionalExpression(ConditionalExpression conditionalExpression)
        {
            _jintWalker.WalkExpression(conditionalExpression.Test);
            _jintWalker.WalkExpression(conditionalExpression.Consequent);
            _jintWalker.WalkExpression(conditionalExpression.Alternate);
        }

        internal void WalkFunctionExpression(FunctionExpression functionExpression)
        {
            if (functionExpression.Id != null) 
                _jintWalker.WalkExpression(functionExpression.Id);

            foreach (var p in functionExpression.Parameters)
            {
                _jintWalker.WalkExpression(p);
            }
            _jintWalker.WalkStatement(functionExpression.Body);
        }

        internal void WalkIdentifier(Identifier identifier)
        {
        }

        internal void WalkLiteral(Literal literal)
        {
        }

        internal void WalkLogicalExpression(LogicalExpression logicalExpression)
        {
            _jintWalker.WalkExpression(logicalExpression.Left);
            _jintWalker.WalkExpression(logicalExpression.Right);
        }

        internal void WalkMemberExpression(MemberExpression memberExpression)
        {
            _jintWalker.WalkExpression(memberExpression.Object);
            _jintWalker.WalkExpression(memberExpression.Property);
        }

        internal void WalkNewExpression(NewExpression newExpression)
        {
            _jintWalker.WalkExpression(newExpression.Callee);
            foreach (Expression exp in newExpression.Arguments)
            {
                _jintWalker.WalkExpression(exp);
            }
        }

        internal void WalkObjectExpression(ObjectExpression objectExpression)
        {
            foreach (Property property in objectExpression.Properties)
            {
                _jintWalker.WalkExpression(property.Value);
            }
        }

        internal void WalkSequenceExpression(SequenceExpression sequenceExpression)
        {
            foreach (Expression expression in sequenceExpression.Expressions)
            {
                _jintWalker.WalkExpression(expression);
            }
        }

        internal void WalkThisExpression(ThisExpression thisExpression)
        {
        }

        internal void WalkUpdateExpression(UpdateExpression updateExpression)
        {
            _jintWalker.WalkExpression(updateExpression.Argument);
        }

        internal void WalkUnaryExpression(UnaryExpression unaryExpression)
        {
            _jintWalker.WalkExpression(unaryExpression.Argument);
        }

        internal void WalkAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            _jintWalker.WalkExpression(assignmentExpression.Left);
            _jintWalker.WalkExpression(assignmentExpression.Right);
        }

        public void WalkVariableDeclaratorExpression(VariableDeclarator variableDeclarator)
        {
            _jintWalker.WalkExpression(variableDeclarator.Id);
            _jintWalker.WalkExpression(variableDeclarator.Init);
        }

        public void WalkVariableDeclarationExpression(VariableDeclaration variableDeclaration)
        {
            foreach (VariableDeclarator v in variableDeclaration.Declarations)
            {
                _jintWalker.WalkExpression(v);
            }
        }
    }
}
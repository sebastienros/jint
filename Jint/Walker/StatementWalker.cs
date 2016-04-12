using System.Collections.Generic;
using System.Linq;
using Jint.Parser.Ast;

namespace Jint.Walker
{
    internal class StatementWalker
    {
        private readonly JintWalker _jintWalker;

        internal StatementWalker(JintWalker jintWalker)
        {
            _jintWalker = jintWalker;
        }

        internal void WalkFunctionDeclaration(FunctionDeclaration functionDeclaration)
        {
            _jintWalker.ExpressionWalker.WalkIdentifier(functionDeclaration.Id);
            foreach (Identifier param in functionDeclaration.Parameters)
            {
                _jintWalker.ExpressionWalker.WalkIdentifier(param);
            }
            _jintWalker.WalkStatement(functionDeclaration.Body);
        }

        internal void WalkForStatement(ForStatement forStatement)
        {
            if (forStatement.Init != null)
            {
                if (forStatement.Init.Type == SyntaxNodes.VariableDeclaration)
                {
                    _jintWalker.WalkStatement(forStatement.Init.As<Statement>());
                }
                else
                {
                    _jintWalker.WalkExpression(forStatement.Init.As<Expression>());
                }
            }
            _jintWalker.WalkExpression(forStatement.Test);
            _jintWalker.WalkExpression(forStatement.Update);
            _jintWalker.WalkStatement(forStatement.Body);
        }

        internal void WalkExpressionStatement(ExpressionStatement stmt)
        {
            _jintWalker.WalkExpression(stmt.Expression);
        }

        internal void WalkEmptyStatement(EmptyStatement stmt)
        {
        }

        internal void WalkDebuggerStatement(DebuggerStatement stmt)
        {
        }

        internal void WalkDoWhileStatement(DoWhileStatement stmt)
        {
            _jintWalker.WalkStatement(stmt.Body);
            _jintWalker.WalkExpression(stmt.Test);
        }

        internal void WalkContinueStatement(ContinueStatement stmt)
        {
        }

        internal void WalkBreakStatement(BreakStatement stmt)
        {
        }

        internal void WalkForInStatement(ForInStatement forInStatement)
        {
            Identifier identifier = forInStatement.Left.Type == SyntaxNodes.VariableDeclaration
              ? forInStatement.Left.As<VariableDeclaration>().Declarations.First().Id
              : forInStatement.Left.As<Identifier>();

            _jintWalker.WalkExpression(identifier);
            _jintWalker.WalkExpression(forInStatement.Right);
            _jintWalker.WalkStatement(forInStatement.Body);
        }

        internal void WalkIfStatement(IfStatement stmt)
        {
            _jintWalker.WalkExpression(stmt.Test);
            _jintWalker.WalkStatement(stmt.Consequent);
            if (stmt.Alternate != null)
            {
                _jintWalker.WalkStatement(stmt.Alternate);
            }
        }

        internal void WalkLabelledStatement(LabelledStatement stmt)
        {
            _jintWalker.WalkStatement(stmt.Body);
        }

        internal void WalkReturnStatement(ReturnStatement stmt)
        {
            _jintWalker.WalkExpression(stmt.Argument);
        }

        internal void WalkSwitchStatement(SwitchStatement stmt)
        {
            _jintWalker.WalkExpression(stmt.Discriminant);
            foreach (SwitchCase c in stmt.Cases)
            {
                _jintWalker.WalkExpression(c.Test);
                WalkStatementList(c.Consequent);
            }
        }

        internal void WalkThrowStatement(ThrowStatement stmt)
        {
            _jintWalker.WalkExpression(stmt.Argument);
        }

        internal void WalkTryStatement(TryStatement stmt)
        {
            _jintWalker.WalkStatement(stmt.Block);
            if (stmt.Handlers.Any())
            {
                foreach (CatchClause catchClause in stmt.Handlers)
                {
                    _jintWalker.WalkStatement(catchClause.Body);
                }
            }
            _jintWalker.WalkStatement(stmt.Finalizer);
        }

        internal void WalkVariableDeclaration(VariableDeclaration stmt)
        {
            foreach (VariableDeclarator declaration in stmt.Declarations)
            {
                _jintWalker.WalkExpression(declaration);
            }
        }

        internal void WalkInSightTagDeclaration(VariableDeclaration stmt)
        {
            foreach (VariableDeclarator declaration in stmt.Declarations)
            {
                _jintWalker.WalkExpression(declaration.Id);
                if (declaration.Init != null)
                {
                    _jintWalker.WalkExpression(declaration.Init);
                }
            }
        }

        internal void WalkWhileStatement(WhileStatement whileStatement)
        {
            _jintWalker.WalkExpression(whileStatement.Test);
            _jintWalker.WalkStatement(whileStatement.Body);
        }

        internal void WalkWithStatement(WithStatement withStatement)
        {
            _jintWalker.WalkExpression(withStatement.Object);
            _jintWalker.WalkStatement(withStatement.Body);
        }

        internal void WalkProgram(Program stmt)
        {
            WalkStatementList(stmt.Body);
        }

        internal void WalkBlockStatement(BlockStatement stmt)
        {
            WalkStatementList(stmt.Body);
        }

        private void WalkStatementList(IEnumerable<Statement> body)
        {
            foreach (Statement statement in body)
            {
                _jintWalker.WalkStatement(statement);
            }
        }
    }
}
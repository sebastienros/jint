using System.Linq;
using Jint.Native;
using Jint.Parser.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime
{
    public class StatementInterpreter
    {
        private readonly Engine _engine;

        public StatementInterpreter(Engine engine)
        {
            _engine = engine;
        }

        private void ExecuteStatement(Statement statement)
        {
            _engine.ExecuteStatement(statement);
        }

        public void ExecuteProgram(Program program)
        {
            foreach (var statement in program.Body)
            {
                ExecuteStatement(statement);
            }
        }

        public void ExecuteVariableDeclaration(VariableDeclaration statement)
        {
            var env = _engine.CurrentExecutionContext.VariableEnvironment.Record;

            foreach (var declaration in statement.Declarations)
            {
                dynamic value = Undefined.Instance;

                if (declaration.Init != null)
                {
                    value = _engine.EvaluateExpression(declaration.Init);
                }

                var dn = declaration.Id.Name;
                var varAlreadyDeclared = env.HasBinding(dn);
                if (!varAlreadyDeclared)
                {
                    env.CreateMutableBinding(declaration.Id.Name, false);
                    env.SetMutableBinding(declaration.Id.Name, value, false);
                }
            }
        }

        public void ExecuteDoWhileStatement(DoWhileStatement doWhileStatement)
        {
            bool test;
            do
            {
                ExecuteStatement(doWhileStatement.Body);
                test = _engine.EvaluateExpression(doWhileStatement.Test);
            } while (test);
        }

        public void ExecuteContinueStatement(ContinueStatement continueStatement)
        {
            _engine.CurrentExecutionContext.Continue = continueStatement;
        }

        public void ExecuteBreakStatement(BreakStatement breakStatement)
        {
            _engine.CurrentExecutionContext.Break = breakStatement;
        }

        public void ExecuteBlockStatement(BlockStatement blockStatement)
        {
            foreach (var statement in blockStatement.Body)
            {
                ExecuteStatement(statement);

                // return has been called, stop execution
                if (!_engine.CurrentExecutionContext.Return.Equals(Undefined.Instance))
                {
                    return;
                }
            }
        }

        public void ExecuteEmptyStatement(EmptyStatement emptyStatement)
        {
        }

        public void ExecuteExpressionStatement(ExpressionStatement expressionStatement)
        {
            _engine.EvaluateExpression(expressionStatement.Expression);
        }

        public void ExecuteReturnStatement(ReturnStatement statement)
        {
            _engine.CurrentExecutionContext.Return = _engine.EvaluateExpression(statement.Argument);
        }

        public void ExecuteFunctionDeclaration(FunctionDeclaration functionDeclaration)
        {
            // create function objects
            http://www.ecma-international.org/ecma-262/5.1/#sec-13.2

            var identifier = functionDeclaration.Id.Name;

            // todo: should be declared in the current context
            _engine.Global.Set(
                identifier, 
                new ScriptFunctionInstance(
                    functionDeclaration.Body, 
                    identifier, 
                    functionDeclaration.Parameters.ToArray(),
                    _engine.Function.Prototype,
                    _engine.Object.Construct(new dynamic[0]),
                    LexicalEnvironment.NewDeclarativeEnvironment(_engine.CurrentExecutionContext.LexicalEnvironment)
                )
            );
        }

        public void ExecuteIfStatement(IfStatement ifStatement)
        {
            var test = _engine.EvaluateExpression(ifStatement.Test);

            if (test)
            {
                _engine.ExecuteStatement(ifStatement.Consequent);
            }
            else if (ifStatement.Alternate != null)
            {
                _engine.ExecuteStatement(ifStatement.Alternate);
            }
        }

        public void ExecuteWhileStatement(WhileStatement whileStatement)
        {
            bool test = _engine.EvaluateExpression(whileStatement.Test);

            while(test)
            {
                ExecuteStatement(whileStatement.Body);
                test = _engine.EvaluateExpression(whileStatement.Test);
            }
        }

        public void ExecuteDebuggerStatement(DebuggerStatement debuggerStatement)
        {
            throw new System.NotImplementedException();
        }

        public void ExecuteForStatement(ForStatement forStatement)
        {
            if (forStatement.Init.Type == SyntaxNodes.VariableDeclaration)
            {
                _engine.ExecuteStatement(forStatement.Init.As<Statement>());
            }
            else
            {
                _engine.EvaluateExpression(forStatement.Init.As<Expression>());
            }

            while (_engine.EvaluateExpression(forStatement.Test))
            {
                _engine.ExecuteStatement(forStatement.Body);
                _engine.EvaluateExpression(forStatement.Update);
            }
        }
    }
}

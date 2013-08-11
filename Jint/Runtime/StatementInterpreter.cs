using System.Linq;
using Jint.Native;
using Jint.Native.Function;
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

        private object ExecuteStatement(Statement statement)
        {
            return _engine.ExecuteStatement(statement);
        }

        public object ExecuteProgram(Program program)
        {
            object result = null;

            foreach (var statement in program.Body)
            {
                result = ExecuteStatement(statement);
            }

            return result;
        }

        public object ExecuteVariableDeclaration(VariableDeclaration statement)
        {
            object result = null;
            var env = _engine.CurrentExecutionContext.VariableEnvironment.Record;

            foreach (var declaration in statement.Declarations)
            {
                object value = Undefined.Instance;

                if (declaration.Init != null)
                {
                    result = value = _engine.EvaluateExpression(declaration.Init);
                }

                var dn = declaration.Id.Name;
                var varAlreadyDeclared = env.HasBinding(dn);
                if (!varAlreadyDeclared)
                {
                    env.CreateMutableBinding(declaration.Id.Name, true);
                    env.SetMutableBinding(declaration.Id.Name, value, false);
                }
            }

            return result;
        }

        public object ExecuteDoWhileStatement(DoWhileStatement doWhileStatement)
        {
            object result = null;

            bool test;
            do
            {
                result = ExecuteStatement(doWhileStatement.Body);
                test = TypeConverter.ToBoolean(_engine.EvaluateExpression(doWhileStatement.Test));
            } while (test);

            return result;
        }

        public object ExecuteContinueStatement(ContinueStatement continueStatement)
        {
            _engine.CurrentExecutionContext.Continue = continueStatement;
            return null;
        }

        public object ExecuteBreakStatement(BreakStatement breakStatement)
        {
            _engine.CurrentExecutionContext.Break = breakStatement;
            return null;
        }

        public object ExecuteBlockStatement(BlockStatement blockStatement)
        {
            object result = null;
            foreach (var statement in blockStatement.Body)
            {
                result = ExecuteStatement(statement);

                // return has been called, stop execution
                if (!_engine.CurrentExecutionContext.Return.Equals(Undefined.Instance))
                {
                    return result;
                }
            }

            return result;
        }

        public object ExecuteEmptyStatement(EmptyStatement emptyStatement)
        {
            return null;
        }

        public object ExecuteExpressionStatement(ExpressionStatement expressionStatement)
        {
            return _engine.EvaluateExpression(expressionStatement.Expression);
        }

        public object ExecuteReturnStatement(ReturnStatement statement)
        {
            return _engine.CurrentExecutionContext.Return = _engine.EvaluateExpression(statement.Argument);
        }

        public object ExecuteFunctionDeclaration(FunctionDeclaration functionDeclaration)
        {
            object result = null;

            // create function objects
            // http://www.ecma-international.org/ecma-262/5.1/#sec-13.2

            var identifier = functionDeclaration.Id.Name;

            // todo: should be declared in the current context
            _engine.Global.Set(
                identifier, 
                result = new ScriptFunctionInstance(
                    _engine,
                    functionDeclaration.Body, 
                    identifier, 
                    functionDeclaration.Parameters.ToArray(),
                    _engine.Function.Prototype,
                    _engine.Object.Construct(Arguments.Empty),
                    LexicalEnvironment.NewDeclarativeEnvironment(_engine.CurrentExecutionContext.LexicalEnvironment)
                )
            );

            return result;
        }

        public object ExecuteIfStatement(IfStatement ifStatement)
        {
            object result = null;
            var test = TypeConverter.ToBoolean(_engine.EvaluateExpression(ifStatement.Test));

            if (test)
            {
                result = _engine.ExecuteStatement(ifStatement.Consequent);
            }
            else if (ifStatement.Alternate != null)
            {
                result = _engine.ExecuteStatement(ifStatement.Alternate);
            }

            return result;
        }

        public object ExecuteWhileStatement(WhileStatement whileStatement)
        {
            object result = null;

            bool test = TypeConverter.ToBoolean(_engine.EvaluateExpression(whileStatement.Test));

            while(test)
            {
                result = ExecuteStatement(whileStatement.Body);
                test = TypeConverter.ToBoolean(_engine.EvaluateExpression(whileStatement.Test));
            }

            return result;
        }

        public object ExecuteDebuggerStatement(DebuggerStatement debuggerStatement)
        {
            throw new System.NotImplementedException();
        }

        public object ExecuteForStatement(ForStatement forStatement)
        {
            object result = null;

            if (forStatement.Init.Type == SyntaxNodes.VariableDeclaration)
            {
                result = _engine.ExecuteStatement(forStatement.Init.As<Statement>());
            }
            else
            {
                result = _engine.EvaluateExpression(forStatement.Init.As<Expression>());
            }

            while (TypeConverter.ToBoolean(_engine.EvaluateExpression(forStatement.Test)))
            {
                _engine.ExecuteStatement(forStatement.Body);
                _engine.EvaluateExpression(forStatement.Update);
            }

            return result;
        }
    }
}

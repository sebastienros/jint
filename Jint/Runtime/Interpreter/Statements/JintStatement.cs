using System.Runtime.CompilerServices;
using Esprima;
using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal abstract class JintStatement<T> : JintStatement where T : Statement
    {
        protected readonly T _statement;

        protected JintStatement(Engine engine, T statement) : base(engine, statement)
        {
            _statement = statement;
        }
    }

    internal abstract class JintStatement
    {
        protected readonly Engine _engine;
        private readonly Statement _statement;

        // require sub-classes to set to false explicitly to skip virtual call
        protected bool _initialized = true;

        protected JintStatement(Engine engine, Statement statement)
        {
            _engine = engine;
            _statement = statement;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Completion Execute()
        {
            _engine._lastSyntaxNode = _statement;

            if (_engine._runBeforeStatementChecks)
            {
                _engine.RunBeforeExecuteStatementChecks(_statement);
            }

            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            return ExecuteInternal();
        }

        protected abstract Completion ExecuteInternal();

        public Location Location => _statement.Location;

        /// <summary>
        /// Opportunity to build one-time structures and caching based on lexical context.
        /// </summary>
        protected virtual void Initialize()
        {
        }

        protected internal static JintStatement Build(Engine engine, Statement statement)
        {
            switch (statement.Type)
            {
                case Nodes.BlockStatement:
                    var statementListItems = ((BlockStatement) statement).Body;
                    return new JintBlockStatement(engine, new JintStatementList(engine, statement, statementListItems));

                case Nodes.ReturnStatement:
                    return new JintReturnStatement(engine, (ReturnStatement) statement);

                case Nodes.VariableDeclaration:
                    return new JintVariableDeclaration(engine, (VariableDeclaration) statement);

                case Nodes.BreakStatement:
                    return new JintBreakStatement(engine, (BreakStatement) statement);

                case Nodes.ContinueStatement:
                    return new JintContinueStatement(engine, (ContinueStatement) statement);

                case Nodes.DoWhileStatement:
                    return new JintDoWhileStatement(engine, (DoWhileStatement) statement);

                case Nodes.EmptyStatement:
                    return new JintEmptyStatement(engine, (EmptyStatement) statement);

                case Nodes.ExpressionStatement:
                    return new JintExpressionStatement(engine, (ExpressionStatement) statement);

                case Nodes.ForStatement:
                    return new JintForStatement(engine, (ForStatement) statement);

                case Nodes.ForInStatement:
                    return new JintForInStatement(engine, (ForInStatement) statement);

                case Nodes.IfStatement:
                    return new JintIfStatement(engine, (IfStatement) statement);

                case Nodes.LabeledStatement:
                    return new JintLabeledStatement(engine, (LabeledStatement) statement);

                case Nodes.SwitchStatement:
                    return new JintSwitchStatement(engine, (SwitchStatement) statement);

                case Nodes.FunctionDeclaration:
                    return new JintFunctionDeclarationStatement(engine, (FunctionDeclaration) statement);

                case Nodes.ThrowStatement:
                    return new JintThrowStatement(engine, (ThrowStatement) statement);

                case Nodes.TryStatement:
                    return new JintTryStatement(engine, (TryStatement) statement);

                case Nodes.WhileStatement:
                    return new JintWhileStatement(engine, (WhileStatement) statement);

                case Nodes.WithStatement:
                    return new JintWithStatement(engine, (WithStatement) statement);

                case Nodes.DebuggerStatement:
                    return new JintDebuggerStatement(engine, (DebuggerStatement) statement);

                case Nodes.Program:
                    return new JintProgram(engine, (Program) statement);

                default:
                    return ExceptionHelper.ThrowArgumentOutOfRangeException<JintStatement>();
            }
        }

        internal static Completion? FastResolve(IStatementListItem statement)
        {
            if (statement is ReturnStatement rs && rs.Argument is Literal l)
            {
                var jsValue = JintLiteralExpression.ConvertToJsValue(l);
                if (jsValue != null)
                {
                    return new Completion(CompletionType.Return, jsValue, null, rs.Location);
                }
            }

            return null;
        }
    }
}
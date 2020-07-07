using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Esprima;
using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal abstract class JintStatement<T> : JintStatement where T : Statement
    {
        internal readonly T _statement;

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
            if (_statement.Type != Nodes.BlockStatement)
            {
                _engine._lastSyntaxNode = _statement;
                _engine.RunBeforeExecuteStatementChecks(_statement);
            }

            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            return ExecuteInternal();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<Completion> ExecuteAsync()
        {
            _engine._lastSyntaxNode = _statement;
            _engine.RunBeforeExecuteStatementChecks(_statement);

            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            return ExecuteInternalAsync();
        }

        protected abstract Completion ExecuteInternal();

        protected abstract Task<Completion> ExecuteInternalAsync();

        public Location Location => _statement.Location;

        /// <summary>
        /// Opportunity to build one-time structures and caching based on lexical context.
        /// </summary>
        protected virtual void Initialize()
        {
        }

        protected internal static JintStatement Build(Engine engine, Statement statement)
        {
            return statement.Type switch
            {
                Nodes.BlockStatement => new JintBlockStatement(engine, (BlockStatement) statement),
                Nodes.ReturnStatement => new JintReturnStatement(engine, (ReturnStatement) statement),
                Nodes.VariableDeclaration => new JintVariableDeclaration(engine, (VariableDeclaration) statement),
                Nodes.BreakStatement => new JintBreakStatement(engine, (BreakStatement) statement),
                Nodes.ContinueStatement => new JintContinueStatement(engine, (ContinueStatement) statement),
                Nodes.DoWhileStatement => new JintDoWhileStatement(engine, (DoWhileStatement) statement),
                Nodes.EmptyStatement => new JintEmptyStatement(engine, (EmptyStatement) statement),
                Nodes.ExpressionStatement => new JintExpressionStatement(engine, (ExpressionStatement) statement),
                Nodes.ForStatement => new JintForStatement(engine, (ForStatement) statement),
                Nodes.ForInStatement => new JintForInForOfStatement(engine, (ForInStatement) statement),
                Nodes.ForOfStatement => new JintForInForOfStatement(engine, (ForOfStatement) statement),
                Nodes.IfStatement => new JintIfStatement(engine, (IfStatement) statement),
                Nodes.LabeledStatement => new JintLabeledStatement(engine, (LabeledStatement) statement),
                Nodes.SwitchStatement => new JintSwitchStatement(engine, (SwitchStatement) statement),
                Nodes.FunctionDeclaration => new JintFunctionDeclarationStatement(engine, (FunctionDeclaration) statement),
                Nodes.ThrowStatement => new JintThrowStatement(engine, (ThrowStatement) statement),
                Nodes.TryStatement => new JintTryStatement(engine, (TryStatement) statement),
                Nodes.WhileStatement => new JintWhileStatement(engine, (WhileStatement) statement),
                Nodes.WithStatement => new JintWithStatement(engine, (WithStatement) statement),
                Nodes.DebuggerStatement => new JintDebuggerStatement(engine, (DebuggerStatement) statement),
                Nodes.Program => new JintScript(engine, statement as Script ?? ExceptionHelper.ThrowArgumentException<Script>("modules not supported")),
                _ => ExceptionHelper.ThrowArgumentOutOfRangeException<JintStatement>(nameof(statement.Type), $"unsupported statement type '{statement.Type}'")
            };
        }

        internal static Completion? FastResolve(StatementListItem statement)
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
using System.Runtime.CompilerServices;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal abstract class JintStatement<T> : JintStatement where T : Statement
    {
        internal new readonly T _statement;

        protected JintStatement(T statement) : base(statement)
        {
            _statement = statement;
        }
    }

    internal abstract class JintStatement
    {
        internal readonly Statement _statement;
        private bool _initialized;

        protected JintStatement(Statement statement)
        {
            _statement = statement;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Completion Execute(EvaluationContext context)
        {
            if (_statement.Type != Nodes.BlockStatement)
            {
                context.PrepareFor(_statement);
                context.RunBeforeExecuteStatementChecks(_statement);
            }

            if (!_initialized)
            {
                Initialize(context);
                _initialized = true;
            }

            if (context.ResumedCompletion.IsAbrupt() && !SupportsResume)
            {
                return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
            }

            return ExecuteInternal(context);
        }

        internal virtual bool SupportsResume => false;

        protected abstract Completion ExecuteInternal(EvaluationContext context);

        public ref readonly Location Location => ref _statement.Location;

        /// <summary>
        /// Opportunity to build one-time structures and caching based on lexical context.
        /// </summary>
        /// <param name="context"></param>
        protected virtual void Initialize(EvaluationContext context)
        {
        }

        protected internal static JintStatement Build(Statement statement)
        {
            JintStatement? result = statement.Type switch
            {
                Nodes.BlockStatement => new JintBlockStatement((BlockStatement) statement),
                Nodes.ReturnStatement => new JintReturnStatement((ReturnStatement) statement),
                Nodes.VariableDeclaration => new JintVariableDeclaration((VariableDeclaration) statement),
                Nodes.BreakStatement => new JintBreakStatement((BreakStatement) statement),
                Nodes.ContinueStatement => new JintContinueStatement((ContinueStatement) statement),
                Nodes.DoWhileStatement => new JintDoWhileStatement((DoWhileStatement) statement),
                Nodes.EmptyStatement => new JintEmptyStatement((EmptyStatement) statement),
                Nodes.ExpressionStatement => new JintExpressionStatement((ExpressionStatement) statement),
                Nodes.ForStatement => new JintForStatement((ForStatement) statement),
                Nodes.ForInStatement => new JintForInForOfStatement((ForInStatement) statement),
                Nodes.ForOfStatement => new JintForInForOfStatement((ForOfStatement) statement),
                Nodes.IfStatement => new JintIfStatement((IfStatement) statement),
                Nodes.LabeledStatement => new JintLabeledStatement((LabeledStatement) statement),
                Nodes.SwitchStatement => new JintSwitchStatement((SwitchStatement) statement),
                Nodes.FunctionDeclaration => new JintFunctionDeclarationStatement((FunctionDeclaration) statement),
                Nodes.ThrowStatement => new JintThrowStatement((ThrowStatement) statement),
                Nodes.TryStatement => new JintTryStatement((TryStatement) statement),
                Nodes.WhileStatement => new JintWhileStatement((WhileStatement) statement),
                Nodes.WithStatement => new JintWithStatement((WithStatement) statement),
                Nodes.DebuggerStatement => new JintDebuggerStatement((DebuggerStatement) statement),
                Nodes.ClassDeclaration => new JintClassDeclarationStatement((ClassDeclaration) statement),
                Nodes.ExportNamedDeclaration => new JintExportNamedDeclaration((ExportNamedDeclaration) statement),
                Nodes.ExportAllDeclaration => new JintExportAllDeclaration((ExportAllDeclaration) statement),
                Nodes.ExportDefaultDeclaration => new JintExportDefaultDeclaration((ExportDefaultDeclaration) statement),
                Nodes.ImportDeclaration => new JintImportDeclaration((ImportDeclaration) statement),
                _ => null
            };

            if (result is null)
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(statement.Type), $"unsupported statement type '{statement.Type}'");
            }

            return result;
        }

        internal static Completion? FastResolve(StatementListItem statement)
        {
            if (statement is ReturnStatement rs && rs.Argument is Literal l)
            {
                var jsValue = JintLiteralExpression.ConvertToJsValue(l);
                if (jsValue is not null)
                {
                    return new Completion(CompletionType.Return, jsValue, rs);
                }
            }

            return null;
        }
    }
}

using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.3
    /// </summary>
    internal sealed class JintForStatement : JintStatement<ForStatement>
    {
        private readonly JintStatement _body;
        private readonly JintStatement _initStatement;
        private readonly JintExpression _initExpression;
        private readonly JintExpression _test;
        private readonly JintExpression _update;

        public JintForStatement(Engine engine, ForStatement statement) : base(engine, statement)
        {
            _body = Build(engine, _statement.Body);

            if (_statement.Init != null)
            {
                if (_statement.Init.Type == Nodes.VariableDeclaration)
                {
                    _initStatement = Build(engine, (Statement) _statement.Init);
                }
                else
                {
                    _initExpression = JintExpression.Build(engine, (Expression) statement.Init);
                }
            }

            if (_statement.Test != null)
            {
                _test = JintExpression.Build(engine, statement.Test);
            }

            if (_statement.Update != null)
            {
                _update = JintExpression.Build(engine, statement.Update);
            }
        }

        protected override Completion ExecuteInternal()
        {
            _initStatement?.Execute();
            _initExpression?.GetValue();

            JsValue v = Undefined.Instance;
            while (true)
            {
                if (_test != null)
                {
                    if (!TypeConverter.ToBoolean(_test.GetValue()))
                    {
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }
                }

                var stmt = _body.Execute();
                if (!ReferenceEquals(stmt.Value, null))
                {
                    v = stmt.Value;
                }

                var stmtType = stmt.Type;
                if (stmtType == CompletionType.Break && (stmt.Identifier == null || stmt.Identifier == _statement?.LabelSet?.Name))
                {
                    return new Completion(CompletionType.Normal, stmt.Value, null, Location);
                }

                if (stmtType != CompletionType.Continue || ((stmt.Identifier != null) && stmt.Identifier != _statement?.LabelSet?.Name))
                {
                    if (stmtType != CompletionType.Normal)
                    {
                        return stmt;
                    }
                }

                _update?.GetValue();
            }
        }
    }
}
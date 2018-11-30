using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter
{
    public class JintStatementList
    {
        private readonly Engine _engine;
        private readonly Statement _statement;
        private readonly List<StatementListItem> _statements;
        private readonly JintStatement[] _jintStatements;

        public JintStatementList(Engine engine, Statement statement, List<StatementListItem> statements)
        {
            _engine = engine;
            _statement = statement;
            _statements = statements;
            _jintStatements = new JintStatement[_statements.Count];
        }

        public Completion Execute()
        {
            if (_statement != null)
            {
                _engine._lastSyntaxNode = _statement;
                if (_engine._runBeforeStatementChecks)
                {
                    _engine.RunBeforeExecuteStatementChecks(_statement);
                }
            }

            JintStatement s = null;
            var c = new Completion(CompletionType.Normal, null, null);
            Completion sl = c;
            try
            {
                var statements = _jintStatements;
                for (var i = 0; i < (uint) statements.Length; i++)
                {
                    s = statements[i] ?? (statements[i] = JintStatement.Build(_engine, (Statement) _statements[i]));
                    c = s.Execute();
                    if (c.Type != CompletionType.Normal)
                    {
                        var executeStatementList = new Completion(
                            c.Type,
                            c.Value ?? sl.Value,
                            c.Identifier,
                            c.Location);

                        return executeStatementList;
                    }

                    sl = c;
                }
            }
            catch (JavaScriptException v)
            {
                var completion = new Completion(CompletionType.Throw, v.Error, null, v.Location ?? s?.Location);
                return completion;
            }
            catch (TypeErrorException e)
            {
                var error = _engine.TypeError.Construct(new JsValue[]
                {
                    e.Message
                });
                return new Completion(CompletionType.Throw, error, null, s?.Location);
            }
            return new Completion(c.Type, c.GetValueOrDefault(), c.Identifier);
        }

        internal Completion? FastResolve()
        {
            if (_statements.Count == 1
                && _statements[0] is ReturnStatement rs
                && rs.Argument is Literal l)
            {
                var jsValue = JintLiteralExpression.ConvertToJsValue(l);
                return new Completion(CompletionType.Return, jsValue, null);
            }

            return null;
        }
    }
}

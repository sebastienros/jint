using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter
{
    public class JintStatementList
    {
        private class Pair
        {
            internal JintStatement Statement;
            internal Completion? Value;
        }

        private readonly Engine _engine;
        private readonly Statement _statement;
        private readonly NodeList<IStatementListItem> _statements;

        private Pair[] _jintStatements;
        private bool _initialized;

        public JintStatementList(Engine engine, Statement statement, NodeList<IStatementListItem> statements)
        {
            _engine = engine;
            _statement = statement;
            _statements = statements;
        }

        private void Initialize()
        {
            _jintStatements = new Pair[_statements.Count];
            for (var i = 0; i < _jintStatements.Length; i++)
            {
                var statement = JintStatement.Build(_engine, (Statement) _statements[i]);
                _jintStatements[i] = new Pair
                {
                    Statement = statement,
                    Value = JintStatement.FastResolve(_statements[i])
                };
            }
        }

        public Completion Execute()
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            if (_statement != null)
            {
                _engine._lastSyntaxNode = _statement;
                if (_engine._runBeforeStatementChecks)
                {
                    _engine.RunBeforeExecuteStatementChecks(_statement);
                }
            }

            JintStatement s = null;
            var c = new Completion(CompletionType.Normal, null, null, _engine._lastSyntaxNode?.Location ?? default);
            Completion sl = c;
            try
            {
                var statements = _jintStatements;
                for (var i = 0; i < (uint) statements.Length; i++)
                {
                    var pair = statements[i];
                    s = pair.Statement;
                    c = pair.Value ?? s.Execute();
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
                var location = v.Location == default ? s.Location : v.Location;
                var completion = new Completion(CompletionType.Throw, v.Error, null, location);
                return completion;
            }
            catch (TypeErrorException e)
            {
                var error = _engine.TypeError.Construct(new JsValue[]
                {
                    e.Message
                });
                return new Completion(CompletionType.Throw, error, null, s.Location);
            }
            catch (RangeErrorException e)
            {
                var error = _engine.RangeError.Construct(new JsValue[]
                {
                    e.Message
                });
                c = new Completion(CompletionType.Throw, error, null, s.Location);
            }
            return new Completion(c.Type, c.GetValueOrDefault(), c.Identifier, c.Location);
        }

        internal Completion? FastResolve()
        {
            return _statements.Count == 1 ? JintStatement.FastResolve(_statements[0]) : null;
        }
    }
}

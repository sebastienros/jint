using System.Collections.Generic;
using Esprima.Ast;

namespace Jint.Runtime
{
    public class JintStatementList
    {
        private readonly Engine _engine;
        private readonly List<StatementListItem> _statements;
        private readonly JintStatement[] _jintStatements;

        public JintStatementList(Engine engine, List<StatementListItem> statements)
        {
            _engine = engine;
            _statements = statements;
            _jintStatements = new JintStatement[_statements.Count];
        }

        public Completion Execute()
        {
            JintStatement s = null;
            var c = new Completion(CompletionType.Normal, null, null);
            Completion sl = c;
            try
            {
                for (var i = 0; i < _statements.Count; i++)
                {
                    s = _jintStatements[i] ?? (_jintStatements[i] = JintStatement.Build(_engine, (Statement) _statements[i]));
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

            return new Completion(c.Type, c.GetValueOrDefault(), c.Identifier);
        }
    }
}

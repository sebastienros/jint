using Jint.Native;
using Jint.Runtime.Interpreter.Statements;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter
{
    internal partial class JintStatementList
    {
        internal async Task<Completion> ExecuteAsync()
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            if (_statement != null)
            {
                _engine._lastSyntaxNode = _statement;
                _engine.RunBeforeExecuteStatementChecks(_statement);
            }

            JintStatement s = null;
            var c = new Completion(CompletionType.Normal, null, null, _engine._lastSyntaxNode?.Location ?? default);
            Completion sl = c;

            // The value of a StatementList is the value of the last value-producing item in the StatementList
            JsValue lastValue = null;
            try
            {
                foreach (var pair in _jintStatements)
                {
                    s = pair.Statement;
                    c = pair.Value ?? await s.ExecuteAsync();
                    if (c.Type != CompletionType.Normal)
                    {
                        return new Completion(
                            c.Type,
                            c.Value ?? sl.Value,
                            c.Identifier,
                            c.Location);
                    }
                    sl = c;
                    lastValue = c.Value ?? lastValue;
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
            return new Completion(c.Type, lastValue ?? JsValue.Undefined, c.Identifier, c.Location);
        }
    }
}
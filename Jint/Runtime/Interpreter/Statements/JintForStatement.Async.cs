using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-forbodyevaluation
    /// </summary>
    internal sealed partial class JintForStatement : JintStatement<ForStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            LexicalEnvironment oldEnv = null;
            LexicalEnvironment loopEnv = null;
            if (_boundNames != null)
            {
                oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                loopEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                var loopEnvRec = loopEnv._record;
                var kind = _initStatement._statement.Kind;
                for (var i = 0; i < _boundNames.Count; i++)
                {
                    var name = _boundNames[i];
                    if (kind == VariableDeclarationKind.Const)
                    {
                        loopEnvRec.CreateImmutableBinding(name, true);
                    }
                    else
                    {
                        loopEnvRec.CreateMutableBinding(name, false);
                    }
                }

                _engine.UpdateLexicalEnvironment(loopEnv);
            }

            try
            {
                if (_initExpression != null)
                {
                    await _initExpression?.GetValueAsync();
                }
                else
                {
                    await _initStatement?.ExecuteAsync();
                }

                return await ForBodyEvaluationAsync();
            }
            finally
            {
                if (oldEnv != null)
                {
                    _engine.UpdateLexicalEnvironment(oldEnv);
                }
            }
        }

        private async Task<Completion> ForBodyEvaluationAsync()
        {
            var v = Undefined.Instance;

            if (_shouldCreatePerIterationEnvironment)
            {
                CreatePerIterationEnvironment();
            }

            while (true)
            {
                if (_test != null)
                {
                    if (!TypeConverter.ToBoolean(await _test.GetValueAsync()))
                    {
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }
                }

                var result = await _body.ExecuteAsync();
                if (!ReferenceEquals(result.Value, null))
                {
                    v = result.Value;
                }

                if (result.Type == CompletionType.Break && (result.Identifier == null || result.Identifier == _statement?.LabelSet?.Name))
                {
                    return new Completion(CompletionType.Normal, result.Value, null, Location);
                }

                if (result.Type != CompletionType.Continue || (result.Identifier != null && result.Identifier != _statement?.LabelSet?.Name))
                {
                    if (result.Type != CompletionType.Normal)
                    {
                        return result;
                    }
                }

                if (_shouldCreatePerIterationEnvironment)
                {
                    CreatePerIterationEnvironment();
                }

                _increment?.GetValue();
            }
        }
    }
}
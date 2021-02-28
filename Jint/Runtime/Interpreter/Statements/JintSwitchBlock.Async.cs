using Esprima;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed partial class JintSwitchBlock
    {
        public async Task<Completion> ExecuteAsync(JsValue input)
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            JsValue v = Undefined.Instance;
            Location l = _engine._lastSyntaxNode.Location;
            JintSwitchCase defaultCase = null;
            bool hit = false;

            for (var i = 0; i < (uint)_jintSwitchBlock.Length; i++)
            {
                var clause = _jintSwitchBlock[i];

                LexicalEnvironment oldEnv = null;
                if (clause.LexicalDeclarations != null)
                {
                    oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var blockEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                    JintStatementList.BlockDeclarationInstantiation(blockEnv, clause.LexicalDeclarations);
                    _engine.UpdateLexicalEnvironment(blockEnv);
                }

                if (clause.Test == null)
                {
                    defaultCase = clause;
                }
                else
                {
                    var clauseSelector = await clause.Test.GetValueAsync();
                    if (JintBinaryExpression.StrictlyEqual(clauseSelector, input))
                    {
                        hit = true;
                    }
                }

                if (hit && clause.Consequent != null)
                {
                    var r = await clause.Consequent.ExecuteAsync();

                    if (oldEnv != null)
                    {
                        _engine.UpdateLexicalEnvironment(oldEnv);
                    }

                    if (r.Type != CompletionType.Normal)
                    {
                        return r;
                    }

                    l = r.Location;
                    v = r.Value ?? Undefined.Instance;
                }
            }

            // do we need to execute the default case ?
            if (hit == false && defaultCase != null)
            {
                LexicalEnvironment oldEnv = null;
                if (defaultCase.LexicalDeclarations != null)
                {
                    oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var blockEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                    JintStatementList.BlockDeclarationInstantiation(blockEnv, defaultCase.LexicalDeclarations);
                    _engine.UpdateLexicalEnvironment(blockEnv);
                }

                var r = await defaultCase.Consequent.ExecuteAsync();

                if (oldEnv != null)
                {
                    _engine.UpdateLexicalEnvironment(oldEnv);
                }
                if (r.Type != CompletionType.Normal)
                {
                    return r;
                }

                l = r.Location;
                v = r.Value ?? Undefined.Instance;
            }

            return new Completion(CompletionType.Normal, v, null, l);
        }
    }
}

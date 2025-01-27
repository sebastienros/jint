using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintSwitchBlock
{
    private readonly NodeList<SwitchCase> _switchBlock;
    private JintSwitchCase[] _jintSwitchBlock = [];
    private bool _initialized;

    public JintSwitchBlock(NodeList<SwitchCase> switchBlock)
    {
        _switchBlock = switchBlock;
    }

    private void Initialize()
    {
        _jintSwitchBlock = new JintSwitchCase[_switchBlock.Count];
        for (var i = 0; i < _jintSwitchBlock.Length; i++)
        {
            _jintSwitchBlock[i] = new JintSwitchCase(_switchBlock[i]);
        }
    }

    public Completion Execute(EvaluationContext context, JsValue input)
    {
        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }

        var v = JsValue.Undefined;
        var l = context.LastSyntaxElement;
        var hit = false;
        var defaultCaseIndex = -1;

        var i = 0;
        Environment? oldEnv = null;
        var temp = _jintSwitchBlock;

        DeclarativeEnvironment? blockEnv = null;

        start:
        for (; i < temp.Length; i++)
        {
            var clause = temp[i];
            if (clause.LexicalDeclarations.Declarations.Count > 0 && oldEnv is null)
            {
                oldEnv = context.Engine.ExecutionContext.LexicalEnvironment;
                blockEnv ??= JintEnvironment.NewDeclarativeEnvironment(context.Engine, oldEnv);
                blockEnv.Clear();

                JintStatementList.BlockDeclarationInstantiation(blockEnv, clause.LexicalDeclarations);
                context.Engine.UpdateLexicalEnvironment(blockEnv);
            }

            if (clause.Test == null)
            {
                defaultCaseIndex = i;
                if (!hit)
                {
                    continue;
                }
            }

            var clauseSelector = clause.Test?.GetValue(context);
            if (clauseSelector == input)
            {
                hit = true;
            }

            if (!hit)
            {
                if (oldEnv is not null)
                {
                    context.Engine.UpdateLexicalEnvironment(oldEnv);
                    oldEnv = null;
                }
                continue;
            }

            var r = clause.Consequent.Execute(context);

            if (r.Type != CompletionType.Normal)
            {
                if (oldEnv is not null)
                {
                    context.Engine.UpdateLexicalEnvironment(oldEnv);
                }

                return r.UpdateEmpty(v);
            }

            l = r._source;
            v = r.Value.IsUndefined() ? v : r.Value;
        }

        // do we need to execute the default case ?
        if (!hit && defaultCaseIndex != -1)
        {
            // jump back to loop and start from default case
            hit = true;
            i = defaultCaseIndex;
            goto start;
        }

        if (oldEnv is not null)
        {
            context.Engine.UpdateLexicalEnvironment(oldEnv);
        }

        return new Completion(CompletionType.Normal, v, l);
    }

    private sealed class JintSwitchCase
    {
        internal readonly JintStatementList Consequent;
        internal readonly JintExpression? Test;
        internal readonly DeclarationCache LexicalDeclarations;

        public JintSwitchCase(SwitchCase switchCase)
        {
            Consequent = new JintStatementList(statement: null, switchCase.Consequent);
            LexicalDeclarations = DeclarationCacheBuilder.Build(switchCase);

            if (switchCase.Test != null)
            {
                Test = JintExpression.Build(switchCase.Test);
            }
        }
    }
}

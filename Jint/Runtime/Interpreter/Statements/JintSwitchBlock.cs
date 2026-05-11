using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;
using Range = Acornima.Range;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintSwitchBlock
{
    private readonly JintSwitchCase[] _jintSwitchBlock;
    private readonly DeclarationCache _lexicalDeclarations;

    public JintSwitchBlock(NodeList<SwitchCase> switchBlock)
    {
        _jintSwitchBlock = new JintSwitchCase[switchBlock.Count];
        List<ScopedDeclaration>? declarations = null;
        var allLexicalScoped = true;

        for (var i = 0; i < _jintSwitchBlock.Length; i++)
        {
            var switchCase = new JintSwitchCase(switchBlock[i]);
            _jintSwitchBlock[i] = switchCase;

            if (switchCase.LexicalDeclarations.Declarations.Count > 0)
            {
                declarations ??= [];
                declarations.AddRange(switchCase.LexicalDeclarations.Declarations);
                allLexicalScoped &= switchCase.LexicalDeclarations.AllLexicalScoped;
            }
        }

        _lexicalDeclarations = new DeclarationCache(declarations ?? [], allLexicalScoped);
    }

    public Completion Execute(EvaluationContext context, JsValue input)
    {
        return Execute(context, input, startIndex: 0, hit: false, defaultCaseIndex: -1, initialValue: JsValue.Undefined);
    }

    public Completion? ExecuteResume(EvaluationContext context, Node suspensionNode)
    {
        var temp = _jintSwitchBlock;
        var suspendable = context.Engine.ExecutionContext.Suspendable;
        SwitchBlockSuspendData? suspendData = null;
        suspendable?.Data.TryGet(this, out suspendData);
        for (var i = 0; i < temp.Length; i++)
        {
            var clause = temp[i];
            if (clause.TestRange is { } testRange && JintStatement.IsNodeInsideRange(suspensionNode, testRange))
            {
                return Execute(
                    context,
                    suspendData?.Input ?? JsValue.Undefined,
                    i,
                    hit: false,
                    defaultCaseIndex: suspendData?.DefaultCaseIndex ?? -1,
                    initialValue: suspendData?.AccumulatedValue ?? JsValue.Undefined);
            }

            if (JintStatement.IsNodeInsideRange(suspensionNode, clause.Range))
            {
                return Execute(
                    context,
                    suspendData?.Input ?? JsValue.Undefined,
                    i,
                    hit: true,
                    defaultCaseIndex: suspendData?.DefaultCaseIndex ?? -1,
                    initialValue: suspendData?.AccumulatedValue ?? JsValue.Undefined);
            }
        }

        return null;
    }

    private Completion Execute(EvaluationContext context, JsValue input, int startIndex, bool hit, int defaultCaseIndex, JsValue initialValue)
    {
        var v = initialValue;
        var l = context.LastSyntaxElement;

        var i = startIndex;
        Environment? oldEnv = null;
        var temp = _jintSwitchBlock;
        var suspendable = context.Engine.ExecutionContext.Suspendable;
        SwitchBlockSuspendData? suspendData = null;
        suspendable?.Data.TryGet(this, out suspendData);

        DeclarativeEnvironment? blockEnv = suspendData?.BlockEnvironment;

start:
        for (; i < temp.Length; i++)
        {
            var clause = temp[i];
            if (_lexicalDeclarations.Declarations.Count > 0 && oldEnv is null)
            {
                oldEnv = suspendData?.OuterEnvironment ?? context.Engine.ExecutionContext.LexicalEnvironment;
                if (blockEnv is null)
                {
                    blockEnv = JintEnvironment.NewDeclarativeEnvironment(context.Engine, oldEnv);
                    JintStatementList.BlockDeclarationInstantiation(blockEnv, _lexicalDeclarations);
                }

                if (!ReferenceEquals(context.Engine.ExecutionContext.LexicalEnvironment, blockEnv))
                {
                    context.Engine.UpdateLexicalEnvironment(blockEnv);
                }
            }

            if (!hit)
            {
                if (clause.Test == null)
                {
                    defaultCaseIndex = i;
                    continue;
                }

                var clauseSelector = clause.Test.GetValue(context);
                if (context.IsSuspended())
                {
                    SaveSuspendData(context, input, defaultCaseIndex, v, blockEnv, oldEnv);
                    if (oldEnv is not null)
                    {
                        context.Engine.UpdateLexicalEnvironment(oldEnv);
                    }

                    var suspendedValue = suspendable?.SuspendedValue ?? JsValue.Undefined;
                    return new Completion(CompletionType.Return, suspendedValue, clause.Test._expression);
                }

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
            }

            var r = clause.Consequent.Execute(context);
            if (context.IsSuspended())
            {
                SaveSuspendData(context, input, defaultCaseIndex, v, blockEnv, oldEnv);
            }

            if (r.Type != CompletionType.Normal)
            {
                if (oldEnv is not null)
                {
                    context.Engine.UpdateLexicalEnvironment(oldEnv);
                }

                if (!context.IsSuspended())
                {
                    suspendable?.Data.Clear(this);
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

        context.Engine.ExecutionContext.Suspendable?.Data.Clear(this);
        return new Completion(CompletionType.Normal, v, l);
    }

    private void SaveSuspendData(
        EvaluationContext context,
        JsValue input,
        int defaultCaseIndex,
        JsValue value,
        DeclarativeEnvironment? blockEnv,
        Environment? oldEnv)
    {
        var suspendable = context.Engine.ExecutionContext.Suspendable;
        if (suspendable is not null)
        {
            var data = suspendable.Data.GetOrCreate<SwitchBlockSuspendData>(this);
            data.Input = input;
            data.DefaultCaseIndex = defaultCaseIndex;
            data.AccumulatedValue = value;
            data.BlockEnvironment = blockEnv;
            data.OuterEnvironment = oldEnv;
        }
    }

    private sealed class JintSwitchCase
    {
        internal readonly JintStatementList Consequent;
        internal readonly JintExpression? Test;
        internal readonly DeclarationCache LexicalDeclarations;
        internal readonly Range Range;
        internal readonly Range? TestRange;

        public JintSwitchCase(SwitchCase switchCase)
        {
            Consequent = new JintStatementList(statement: null, switchCase.Consequent);
            LexicalDeclarations = DeclarationCacheBuilder.Build(switchCase);
            Range = switchCase.Range;
            TestRange = switchCase.Test?.Range;

            if (switchCase.Test != null)
            {
                Test = JintExpression.Build(switchCase.Test);
            }
        }
    }
}

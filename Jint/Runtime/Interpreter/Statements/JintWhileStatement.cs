using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
/// </summary>
internal sealed class JintWhileStatement : JintStatement<WhileStatement>
{
    private string? _labelSetName;
    private ProbablyBlockStatement _body;
    private JintExpression _test = null!;

    public JintWhileStatement(WhileStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _labelSetName = _statement.LabelSet?.Name;
        _body = new ProbablyBlockStatement(_statement.Body);
        _test = JintExpression.Build(_statement.Test);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var v = JsValue.Undefined;
        var engine = context.Engine;

        // Check if we're resuming from inside the loop body
        // If so, we need to handle the lexical environment carefully because
        // the saved context may have a block environment from a previous iteration.
        var suspendableForResume = engine.ExecutionContext.Suspendable;
        var resumingInLoop = false;
        Environment? preBodyEnv = null;

        if (suspendableForResume is { IsResuming: true, LastSuspensionNode: not null })
        {
            var resumeNode = suspendableForResume.LastSuspensionNode as Node
                ?? (suspendableForResume.LastSuspensionNode as JintExpression)?._expression as Node;

            if (resumeNode != null && IsNodeInsideLoopBody(resumeNode))
            {
                resumingInLoop = true;
                // The current lexical environment might be a block environment from a previous iteration.
                // We need to restore the environment that was active before the block was created.
                // This is the outer environment of the current environment (if it's a declarative environment).
                var currentEnv = engine.ExecutionContext.LexicalEnvironment;
                if (currentEnv is DeclarativeEnvironment declEnv)
                {
                    // Save the outer environment to use for subsequent iterations
                    preBodyEnv = declEnv._outerEnv;
                }
            }
        }

        while (true)
        {
            // Only clear completed awaits cache when starting a NEW iteration, not when resuming.
            // When resuming from a nested await (e.g., "while (await await await x)"),
            // we need the cached values of already-completed awaits to continue evaluation.
            // When starting fresh (not resuming), clear the cache to ensure expressions like
            // "while (await p)" evaluate p fresh each time even if p changes.
            var asyncFn = context.Engine.ExecutionContext.Suspendable as AsyncFunctionInstance;
            if (asyncFn is null || !asyncFn._isResuming)
            {
                asyncFn?._completedAwaits?.Clear();
            }

            // If we're resuming from inside the loop body and we've identified the pre-body environment,
            // restore it before executing the body or test. This ensures that when the block creates a new
            // environment for the current iteration, it uses the correct outer environment.
            if (resumingInLoop && preBodyEnv is not null)
            {
                engine.UpdateLexicalEnvironment(preBodyEnv);
                resumingInLoop = false; // Only restore once
            }

            if (context.DebugMode)
            {
                context.Engine.Debugger.OnStep(_test._expression);
            }

            var jsValue = _test.GetValue(context);

            // Check for suspension after evaluating the test expression
            var suspendable = context.Engine.ExecutionContext.Suspendable;
            if (context.IsSuspended())
            {
                var suspendedValue = suspendable?.SuspendedValue ?? JsValue.Undefined;
                return new Completion(CompletionType.Return, suspendedValue, _statement);
            }

            if (!TypeConverter.ToBoolean(jsValue))
            {
                return new Completion(CompletionType.Normal, v, _statement);
            }

            var completion = _body.Execute(context);

            if (!completion.Value.IsEmpty)
            {
                v = completion.Value;
            }

            // Check for suspension - if suspended, we need to exit the loop
            if (context.IsSuspended())
            {
                var suspendedValue = suspendable?.SuspendedValue ?? completion.Value;
                return new Completion(CompletionType.Return, suspendedValue, _statement);
            }

            if (completion.Type != CompletionType.Continue || !string.Equals(context.Target, _labelSetName, StringComparison.Ordinal))
            {
                if (completion.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _labelSetName, StringComparison.Ordinal)))
                {
                    return new Completion(CompletionType.Normal, v, _statement);
                }

                if (completion.Type != CompletionType.Normal)
                {
                    return completion;
                }
            }
        }
    }

    /// <summary>
    /// Checks if the given node is inside this while statement's body or test expression.
    /// Used to determine if we're resuming from a yield/await inside the loop.
    /// </summary>
    private bool IsNodeInsideLoopBody(Node node)
    {
        var nodeRange = node.Range;
        var bodyRange = _statement.Body.Range;

        // Check if inside body
        if (bodyRange.Start <= nodeRange.Start && nodeRange.End <= bodyRange.End)
        {
            return true;
        }

        // Check if inside test expression
        if (_statement.Test != null)
        {
            var testRange = _statement.Test.Range;
            if (testRange.Start <= nodeRange.Start && nodeRange.End <= testRange.End)
            {
                return true;
            }
        }

        return false;
    }
}
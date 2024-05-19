using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-generator-instances
/// </summary>
internal sealed class GeneratorInstance : ObjectInstance
{
    internal GeneratorState _generatorState;
    private ExecutionContext _generatorContext;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private readonly JsValue? _generatorBrand;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    private JintStatementList _generatorBody = null!;

    public JsValue? _nextValue;
    public JsValue? _error;

    public GeneratorInstance(Engine engine) : base(engine)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generatorstart
    /// </summary>
    public JsValue GeneratorStart(JintStatementList generatorBody)
    {
        var genContext = _engine.UpdateGenerator(this);
        _generatorBody = generatorBody;

        _generatorContext = genContext;
        _generatorState = GeneratorState.SuspendedStart;

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generatorresume
    /// </summary>
    public ObjectInstance GeneratorResume(JsValue? value, JsValue? generatorBrand)
    {
        var state = GeneratorValidate(generatorBrand);
        if (state == GeneratorState.Completed)
        {
            return new IteratorResult(_engine, Undefined, JsBoolean.True);
        }

        var genContext = _generatorContext;
        var methodContext = _engine.ExecutionContext;

        // 6. Suspend methodContext.

        _nextValue = value;

        var context = _engine._activeEvaluationContext;
        return ResumeExecution(genContext, context!);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generatorresumeabrupt
    /// </summary>
    public JsValue GeneratorResumeAbrupt(in Completion abruptCompletion, JsValue? generatorBrand)
    {
        var state = GeneratorValidate(generatorBrand);
        if (state == GeneratorState.SuspendedStart)
        {
            _generatorState = GeneratorState.Completed;
            state = GeneratorState.Completed;
        }

        if (state == GeneratorState.Completed)
        {
            if (abruptCompletion.Type == CompletionType.Return)
            {
                return new IteratorResult(_engine, abruptCompletion.Value, JsBoolean.True);
            }

            ExceptionHelper.ThrowJavaScriptException(_engine, abruptCompletion.Value, AstExtensions.DefaultLocation);
        }

        var genContext = _generatorContext;
        var methodContext = _engine.ExecutionContext;

        // Suspend methodContext
        _nextValue = abruptCompletion.Type == CompletionType.Return
            ? abruptCompletion.Value
            : null;

        _error = abruptCompletion.Type == CompletionType.Throw
            ? abruptCompletion.Value
            : null;

        if (_error is not null)
        {
            ExceptionHelper.ThrowJavaScriptException(_engine, _error, AstExtensions.DefaultLocation);
        }

        return ResumeExecution(genContext, new EvaluationContext(_engine));
    }

    private ObjectInstance ResumeExecution(in ExecutionContext genContext, EvaluationContext context)
    {
        _generatorState = GeneratorState.Executing;
        _engine.EnterExecutionContext(genContext);

        var result = _generatorBody.Execute(context);
        _engine.LeaveExecutionContext();

        ObjectInstance? resultValue = null;
        if (result.Type == CompletionType.Normal)
        {
            _generatorState = GeneratorState.Completed;
            resultValue = IteratorResult.CreateValueIteratorPosition(_engine, result.Value, done: JsBoolean.True);
        }
        else if (result.Type == CompletionType.Return)
        {
            if (_generatorState == GeneratorState.SuspendedYield)
            {
                resultValue = IteratorResult.CreateValueIteratorPosition(_engine, result.Value, done: JsBoolean.False);
            }
            else
            {
                _generatorState = GeneratorState.Completed;
                resultValue = IteratorResult.CreateValueIteratorPosition(_engine, result.Value, done: JsBoolean.True);
            }
        }

        if (result.Type == CompletionType.Throw)
        {
            _generatorState = GeneratorState.Completed;
            ExceptionHelper.ThrowJavaScriptException(_engine, result.Value, result);
        }

        return resultValue!;
    }

    private GeneratorState GeneratorValidate(JsValue? generatorBrand)
    {
        if (!ReferenceEquals(generatorBrand, _generatorBrand))
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Generator brand differs from attached brand");
        }

        if (_generatorState == GeneratorState.Executing)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Generator state was unexpectedly executing");
        }

        return _generatorState;
    }
}

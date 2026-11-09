using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Modules;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// A dynamic <c>import()</c> neither of whose operands can suspend the frame it runs in.
/// </summary>
/// <remarks>
/// <see cref="Build"/> sends an <c>import()</c> whose specifier or options may suspend to
/// <see cref="JintSuspendableImportExpression"/>, so this handler evaluates both operands straight through and
/// carries none of the resume bookkeeping.
/// </remarks>
internal sealed class JintImportExpression : JintExpression
{
    private readonly JintExpression _specifierExpression;
    private readonly JintExpression? _optionsExpression;
    private readonly ModuleImportPhase _phase;

    public JintImportExpression(ImportExpression expression) : base(expression)
    {
        _specifierExpression = Build(expression.Source);
        _optionsExpression = expression.Options is not null ? Build(expression.Options) : null;
        _phase = PhaseOf(expression);
    }

    /// <summary>
    /// Builds the handler for <paramref name="expression"/>: <see cref="JintSuspendableImportExpression"/> when an
    /// <c>await</c> or a <c>yield</c> of the enclosing frame can occur in its specifier or its options, this one
    /// otherwise, so the choice costs an <c>import()</c> that cannot suspend nothing at run time.
    /// </summary>
    internal static JintExpression Build(ImportExpression expression)
    {
        return MaySuspendVisitor.MaySuspend(expression)
            ? new JintSuspendableImportExpression(expression)
            : new JintImportExpression(expression);
    }

    /// <summary>
    /// The phase the module request asks for: <c>import.defer(…)</c>, <c>import.source(…)</c> or a plain
    /// <c>import(…)</c>.
    /// </summary>
    internal static ModuleImportPhase PhaseOf(ImportExpression expression)
    {
        return expression.Phase switch
        {
            ImportPhase.Defer => ModuleImportPhase.Defer,
            ImportPhase.Source => ModuleImportPhase.Source,
            _ => ModuleImportPhase.Evaluation,
        };
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-evaluate-import-call
    /// </summary>
    protected override object EvaluateInternal(EvaluationContext context)
    {
        var referrer = context.Engine.GetActiveScriptOrModule();
        var specifier = _specifierExpression.GetValue(context);
        var options = _optionsExpression?.GetValue(context) ?? JsValue.Undefined;
        return EvaluateImportCall(context, referrer, specifier, options, _phase);
    }

    /// <summary>
    /// EvaluateImportCall once both operands are in hand, from step 7 on
    /// (https://tc39.es/ecma262/#sec-evaluate-import-call): converts the specifier, reads the import attributes and
    /// asks the host for the module, rejecting the returned promise rather than throwing for what goes wrong.
    /// </summary>
    internal static JsValue EvaluateImportCall(
        EvaluationContext context,
        IScriptOrModule? referrer,
        JsValue specifier,
        JsValue options,
        ModuleImportPhase phase)
    {
        var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);

        try
        {
            var specifierString = TypeConverter.ToString(specifier);

            var attributes = new List<ModuleImportAttribute>();
            if (!options.IsUndefined())
            {
                if (!options.IsObject())
                {
                    Throw.TypeError(context.Engine.Realm, "Invalid options object");
                    return JsValue.Undefined;
                }

                var attributesObj = options.Get("with");
                if (!attributesObj.IsUndefined())
                {
                    if (attributesObj is not ObjectInstance oi)
                    {
                        Throw.TypeError(context.Engine.Realm, "Invalid options.with object");
                        return JsValue.Undefined;
                    }

                    var entries = oi.EnumerableOwnProperties(ObjectInstance.EnumerableOwnPropertyNamesKind.KeyValue);
                    attributes.Capacity = (int) entries.GetLength();
                    foreach (var entry in entries)
                    {
                        var key = entry.Get("0");
                        var value = entry.Get("1");

                        if (!value.IsString())
                        {
                            Throw.TypeError(context.Engine.Realm, "Invalid option value");
                            return JsValue.Undefined;
                        }

                        attributes.Add(new ModuleImportAttribute(key.ToString(), TypeConverter.ToString(value)));
                    }

                    if (!AllImportAttributesSupported(context.Engine._host, attributes))
                    {
                        Throw.TypeError(context.Engine.Realm, "Unsupported import attributes detected");
                    }

                    attributes.Sort(static (item1, item2) => StringComparer.Ordinal.Compare(item1.Key, item2.Key));
                }
            }

            var moduleRequest = new ModuleRequest(Specifier: specifierString, Attributes: attributes.ToArray()) { Phase = phase };
            if (context.Engine.IsRetired)
            {
                promiseCapability.Reject(context.Engine.Realm.Intrinsics.Error.Construct("The engine has been retired and cannot import modules."));
                return promiseCapability.PromiseInstance;
            }
            var payload = new DynamicImportPayload(context.Engine, moduleRequest, promiseCapability);
            context.Engine._host.LoadImportedModule(referrer, moduleRequest, payload);
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject(e.Error);
        }

        return promiseCapability.PromiseInstance;
    }

    private static bool AllImportAttributesSupported(Host host, List<ModuleImportAttribute> attributes)
    {
        var supported = host.GetSupportedImportAttributes();
        foreach (var pair in attributes)
        {
            if (!supported.Contains(pair.Key))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// A dynamic <c>import()</c> whose specifier or options may suspend the frame. EvaluateImportCall evaluates the
/// specifier once, then the options, and only then converts the specifier and asks the host for the module
/// (https://tc39.es/ecma262/#sec-evaluate-import-call), so a resume in the options must go back into them with the
/// specifier it already has, and a suspension in either operand must stop the call before anything is loaded.
/// </summary>
/// <remarks>
/// <para>
/// A resume replays the statement the frame suspended in. Evaluating the specifier again re-ran every call and
/// getter in it once per suspension in the options (sebastienros/jint#4335), so when the options suspend, the
/// specifier's value is parked on the suspendable keyed by this handler (<see cref="ImportCallSuspendData"/>;
/// handler trees are shared, so it cannot be a field), and the resume that re-enters the handler takes it back
/// instead. Nothing is resolved before the specifier, so a suspension inside it parks nothing.
/// </para>
/// <para>
/// The stop tests the frame's suspendable, where the handler used to test a generator's state: an <c>await</c>,
/// and an async generator's <c>yield</c>, went on past the suspension and asked the host for the module with the
/// suspension's placeholder standing in for the operand. <see cref="JintImportExpression.Build"/> picks this
/// handler at build time, so <see cref="JintImportExpression"/> carries none of it.
/// </para>
/// </remarks>
internal sealed class JintSuspendableImportExpression : JintExpression
{
    private readonly JintExpression _specifierExpression;
    private readonly JintExpression? _optionsExpression;
    private readonly ModuleImportPhase _phase;

    public JintSuspendableImportExpression(ImportExpression expression) : base(expression)
    {
        _specifierExpression = Build(expression.Source);
        _optionsExpression = expression.Options is not null ? Build(expression.Options) : null;
        _phase = JintImportExpression.PhaseOf(expression);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;
        var referrer = engine.GetActiveScriptOrModule();

        JsValue specifier;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out ImportCallSuspendData? parked))
        {
            // Taken back rather than read, so a throw out of the options below leaves nothing behind for the next
            // evaluation of this site to mistake for its own. A second suspension parks it again.
            suspendable.Data.Clear(this);
            specifier = parked!.Specifier;
        }
        else
        {
            specifier = _specifierExpression.GetValue(context);
            if (suspendable is not null && suspendable.IsSuspended)
            {
                // The specifier itself suspended, and resumes through its own handler.
                return specifier;
            }
        }

        var options = JsValue.Undefined;
        if (_optionsExpression is not null)
        {
            options = _optionsExpression.GetValue(context);
            if (suspendable is not null && suspendable.IsSuspended)
            {
                suspendable.Data.GetOrCreate<ImportCallSuspendData>(this).Specifier = specifier;
                return options;
            }
        }

        return JintImportExpression.EvaluateImportCall(context, referrer, specifier, options, _phase);
    }
}

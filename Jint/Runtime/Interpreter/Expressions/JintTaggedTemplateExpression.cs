using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintTaggedTemplateExpression : JintExpression
{
    internal static readonly JsString PropertyRaw = new JsString("raw");

    private readonly JintExpression _tagIdentifier;
    private readonly JintTemplateLiteralExpression _quasi;

    public JintTaggedTemplateExpression(TaggedTemplateExpression expression) : base(expression)
    {
        _tagIdentifier = Build(expression.Tag);
        _quasi = new JintTemplateLiteralExpression(expression.Quasi);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;
        ref readonly var expressions = ref _quasi._expressions;

        ICallable tagger;
        JsValue thisObject;
        JsValue[] args;
        int startIndex;

        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out TaggedTemplateSuspendData? suspendData))
        {
            tagger = suspendData!.Tagger;
            thisObject = suspendData.ThisObject;
            args = suspendData.Args;
            startIndex = suspendData.NextExpressionIndex;
        }
        else
        {
            var identifier = _tagIdentifier.Evaluate(context);
            if (context.IsSuspended())
            {
                // _tagIdentifier (e.g. a member expression) suspended; that expression
                // owns its own resume handling. Don't proceed with sentinel data.
                return JsValue.Undefined;
            }

            var taggerCandidate = engine.GetValue(identifier) as ICallable;
            if (taggerCandidate is null)
            {
                Throw.TypeError(engine.Realm, "Argument must be callable");
            }
            tagger = taggerCandidate;

            thisObject = identifier is Reference reference && reference.IsPropertyReference
                ? reference.Base
                : JsValue.Undefined;

            args = engine._jsValueArrayPool.RentArray(expressions.Length + 1);
            args[0] = GetTemplateObject(context);
            startIndex = 0;
        }

        for (var i = startIndex; i < expressions.Length; ++i)
        {
            args[i + 1] = expressions[i].GetValue(context);

            // Without this break, side effects in interpolations after a suspended
            // one would continue to run during the suspended pass.
            if (context.IsSuspended())
            {
                if (suspendable is not null)
                {
                    var data = suspendable.Data.GetOrCreate<TaggedTemplateSuspendData>(this);
                    data.Tagger = tagger;
                    data.ThisObject = thisObject;
                    data.Args = args;
                    data.NextExpressionIndex = i;
                }
                return JsValue.Undefined;
            }
        }

        var result = tagger.Call(thisObject, args);

        engine._jsValueArrayPool.ReturnArray(args);
        suspendable?.Data.Clear(this);

        return result;
    }

    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/#sec-gettemplateobject
    /// </summary>
    private JsArray GetTemplateObject(EvaluationContext context)
    {
        var realm = context.Engine.Realm;
        var templateRegistry = realm._templateMap;
        if (templateRegistry.TryGetValue(this._expression, out var cached))
        {
            return cached;
        }

        ref readonly var elements = ref _quasi._templateLiteralExpression.Quasis;
        var count = (uint) elements.Count;

        var template = new JsArray(context.Engine, count, length: count);
        var rawObj = new JsArray(context.Engine, count, length: count);
        for (uint i = 0; i < elements.Count; ++i)
        {
            var templateElementValue = elements[(int) i].Value;
            template.SetIndexValue(i, templateElementValue.Cooked ?? JsValue.Undefined, updateLength: false);
            rawObj.SetIndexValue(i, templateElementValue.Raw, updateLength: false);
        }

        rawObj.SetIntegrityLevel(ObjectInstance.IntegrityLevel.Frozen);
        template.DefineOwnProperty(PropertyRaw, new PropertyDescriptor(rawObj, PropertyFlag.OnlyWritable));

        template.SetIntegrityLevel(ObjectInstance.IntegrityLevel.Frozen);

        realm._templateMap[_expression] = template;

        return template;
    }
}

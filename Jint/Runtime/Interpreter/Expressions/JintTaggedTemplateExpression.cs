using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintTaggedTemplateExpression : JintExpression
{
    internal static readonly JsString PropertyRaw = new JsString("raw");

    private JintExpression _tagIdentifier = null!;
    private JintTemplateLiteralExpression _quasi = null!;
    private bool _initialized;

    public JintTaggedTemplateExpression(TaggedTemplateExpression expression) : base(expression)
    {
    }

    private void Initialize()
    {
        var taggedTemplateExpression = (TaggedTemplateExpression) _expression;
        _tagIdentifier = Build(taggedTemplateExpression.Tag);
        _quasi = new JintTemplateLiteralExpression(taggedTemplateExpression.Quasi);
        _quasi.DoInitialize();
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }

        var engine = context.Engine;

        var identifier = _tagIdentifier.Evaluate(context);
        var tagger = engine.GetValue(identifier) as ICallable;
        if (tagger is null)
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, "Argument must be callable");
        }

        ref readonly var expressions = ref _quasi._expressions;

        var args = engine._jsValueArrayPool.RentArray(expressions.Length + 1);

        var template = GetTemplateObject(context);
        args[0] = template;

        for (var i = 0; i < expressions.Length; ++i)
        {
            args[i + 1] = expressions[i].GetValue(context);
        }

        var thisObject = identifier is Reference reference && reference.IsPropertyReference
            ? reference.Base
            : JsValue.Undefined;

        var result = tagger.Call(thisObject, args);

        engine._jsValueArrayPool.ReturnArray(args);

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

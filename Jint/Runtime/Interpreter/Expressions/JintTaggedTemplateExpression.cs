using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintTaggedTemplateExpression : JintExpression
{
    internal static readonly JsString PropertyRaw = new JsString("raw");

    private JintExpression _tagIdentifier = null!;
    private JintTemplateLiteralExpression _quasi = null!;

    public JintTaggedTemplateExpression(TaggedTemplateExpression expression) : base(expression)
    {
        _initialized = false;
    }

    protected override void Initialize(EvaluationContext context)
    {
        var taggedTemplateExpression = (TaggedTemplateExpression) _expression;
        _tagIdentifier = Build(taggedTemplateExpression.Tag);
        _quasi = new JintTemplateLiteralExpression(taggedTemplateExpression.Quasi);
        _quasi.DoInitialize();
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;

        var identifier = _tagIdentifier.Evaluate(context);
        var tagger = engine.GetValue(identifier) as ICallable;
        if (tagger is null)
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, "Argument must be callable");
        }

        var expressions = _quasi._expressions;

        var args = engine._jsValueArrayPool.RentArray(expressions.Length + 1);

        var template = GetTemplateObject(context);
        args[0] = template;

        for (var i = 0; i < expressions.Length; ++i)
        {
            args[i + 1] = expressions[i].GetValue(context);
        }

        var thisObject = identifier is Reference reference && reference.IsPropertyReference()
            ? reference.GetBase()
            : JsValue.Undefined;

        var result = tagger.Call(thisObject, args);

        engine._jsValueArrayPool.ReturnArray(args);

        return result;
    }

    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/#sec-gettemplateobject
    /// </summary>
    private ArrayInstance GetTemplateObject(EvaluationContext context)
    {
        var realm = context.Engine.Realm;
        var templateRegistry = realm._templateMap;
        if (templateRegistry.TryGetValue(this._expression, out var cached))
        {
            return cached;
        }

        var count = (uint) _quasi._templateLiteralExpression.Quasis.Count;
        var template = context.Engine.Realm.Intrinsics.Array.ArrayCreate(count);
        var rawObj = context.Engine.Realm.Intrinsics.Array.ArrayCreate(count);
        for (uint i = 0; i < _quasi._templateLiteralExpression.Quasis.Count; ++i)
        {
            var templateElementValue = _quasi._templateLiteralExpression.Quasis[(int) i].Value;
            template.SetIndexValue(i, templateElementValue.Cooked, updateLength: false);
            rawObj.SetIndexValue(i, templateElementValue.Raw, updateLength: false);
        }

        rawObj.SetIntegrityLevel(ObjectInstance.IntegrityLevel.Frozen);
        template.DefineOwnProperty(PropertyRaw, new PropertyDescriptor(rawObj, PropertyFlag.OnlyWritable));

        template.SetIntegrityLevel(ObjectInstance.IntegrityLevel.Frozen);

        realm._templateMap[_expression] = template;

        return template;
    }
}

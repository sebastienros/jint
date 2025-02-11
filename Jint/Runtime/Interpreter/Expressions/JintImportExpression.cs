using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Modules;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintImportExpression : JintExpression
{
    private JintExpression _specifierExpression;
    private bool _initialized;
    private JintExpression? _optionsExpression;

    public JintImportExpression(ImportExpression expression) : base(expression)
    {
        _specifierExpression = null!;
    }

    /// <summary>
    /// https://tc39.es/proposal-import-attributes/#sec-evaluate-import-call
    /// </summary>
    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            var expression = (ImportExpression) _expression;
            _specifierExpression = Build(expression.Source);
            _optionsExpression = expression.Options is not null ? Build(expression.Options) : null;
            _initialized = true;
        }

        var referrer = context.Engine.GetActiveScriptOrModule();
        var specifier = _specifierExpression.GetValue(context); //.UnwrapIfPromise();
        var options = _optionsExpression?.GetValue(context) ?? JsValue.Undefined;

        var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);

        try
        {
            var specifierString = TypeConverter.ToString(specifier);

            var attributes = new List<ModuleImportAttribute>();
            if (!options.IsUndefined())
            {
                if (!options.IsObject())
                {
                    ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Invalid options object");
                    return JsValue.Undefined;
                }

                var attributesObj = options.Get("with");
                if (!attributesObj.IsUndefined())
                {
                    if (attributesObj is not ObjectInstance oi)
                    {
                        ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Invalid options.with object");
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
                            ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Invalid option value " + value);
                            return JsValue.Undefined;
                        }

                        attributes.Add(new ModuleImportAttribute(key.ToString(), TypeConverter.ToString(value)));
                    }

                    if (!AllImportAttributesSupported(context.Engine._host, attributes))
                    {
                        ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Unsupported import attributes detected");
                    }

                    attributes.Sort(static (item1, item2) => StringComparer.Ordinal.Compare(item1.Key, item2.Key));
                }
            }

            var moduleRequest = new ModuleRequest(Specifier: specifierString, Attributes: attributes.ToArray());
            context.Engine._host.LoadImportedModule(referrer, moduleRequest, promiseCapability);
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(JsValue.Undefined, e.Error);
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

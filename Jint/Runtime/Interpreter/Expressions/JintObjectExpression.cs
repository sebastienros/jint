using Jint.Collections;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// https://tc39.es/ecma262/#sec-object-initializer
/// </summary>
internal sealed class JintObjectExpression : JintExpression
{
    private readonly ExpressionCache _valueExpressions = new();
    private readonly ObjectProperty?[] _properties;

    // Pre-hashed property keys for the fast build path, computed once at construction so every
    // execution skips re-deriving (and re-hashing) a Key from each property-name string. Non-null
    // only when _canBuildFast.
    private readonly Key[]? _fastKeys;

    // True when the static keys are all distinct, so a build can append each property directly
    // with no duplicate-key probing. False for the rare literal with a repeated key (e.g.
    // { a: 1, a: 2 }), which must fall back to the checked build so the later value wins.
    private readonly bool _fastKeysDistinct;

    // check if we can do a shortcut when all are object properties
    // and don't require duplicate checking
    private readonly bool _canBuildFast;

    private sealed class ObjectProperty
    {
        internal readonly string? _key;
        private JsString? _keyJsString;
        internal readonly Acornima.Ast.ObjectProperty _value;
        private JintFunctionDefinition? _functionDefinition;

        public ObjectProperty(string? key, Acornima.Ast.ObjectProperty property)
        {
            _key = key;
            _value = property;
        }

        public JsString? KeyJsString => _keyJsString ??= _key != null ? JsString.Create(_key) : null;

        public JintFunctionDefinition GetFunctionDefinition(Engine engine, Acornima.Ast.ObjectProperty property)
        {
            if (_functionDefinition is not null)
            {
                return _functionDefinition;
            }

            var function = _value.Value as IFunction;
            if (function is null)
            {
                Throw.SyntaxError(engine.Realm);
            }

            _functionDefinition = new JintFunctionDefinition(function, sourceTextNode: property);
            return _functionDefinition;
        }
    }

    private JintObjectExpression(ObjectExpression expression) : base(expression)
    {
        _canBuildFast = true;
        ref readonly var properties = ref expression.Properties;

        var valueExpressions = new Expression[properties.Count];
        _properties = new ObjectProperty[properties.Count];

        for (var i = 0; i < _properties.Length; i++)
        {
            string? propName = null;
            var property = properties[i];
            if (property is Acornima.Ast.ObjectProperty p)
            {
                if (!p.Computed && p.Key is Literal literal)
                {
                    propName = AstExtensions.LiteralKeyToString(literal);
                }
                else if (!p.Computed && p.Key is Identifier identifier)
                {
                    propName = identifier.Name;
                    _canBuildFast &= !string.Equals(propName, "__proto__", StringComparison.Ordinal);
                }

                _properties[i] = new ObjectProperty(propName, p);

                if (p.Kind is PropertyKind.Init)
                {
                    if (p.Value is Expression propertyExpression)
                    {
                        valueExpressions[i] = propertyExpression;
                    }
                    else
                    {
                        _canBuildFast = false;
                    }
                    _canBuildFast &= !p.Value.IsFunctionDefinition();
                }
                else
                {
                    _canBuildFast = false;
                }
            }
            else if (property is SpreadElement spreadElement)
            {
                _canBuildFast = false;
                _properties[i] = null;
                valueExpressions[i] = spreadElement.Argument;
            }
            else
            {
                Throw.ArgumentOutOfRangeException("property", "cannot handle property " + property);
            }

            _canBuildFast &= propName != null;
        }

        _valueExpressions.Initialize(valueExpressions.AsSpan());

        if (_canBuildFast)
        {
            // All keys are static names (guaranteed by _canBuildFast); pre-hash them once.
            var keys = new Key[_properties.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = _properties[i]!._key!;
            }
            _fastKeys = keys;
            _fastKeysDistinct = AreKeysDistinct(keys);
        }
    }

    // O(n^2) but runs once at construction; n is the literal's static property count.
    private static bool AreKeysDistinct(Key[] keys)
    {
        for (var i = 1; i < keys.Length; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (keys[i] == keys[j])
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static JintExpression Build(ObjectExpression expression)
    {
        return expression.Properties.Count == 0
            ? JintEmptyObjectExpression.Instance
            : new JintObjectExpression(expression);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        return _canBuildFast
            ? BuildObjectFast(context)
            : BuildObjectNormal(context);
    }

    /// <summary>
    /// Version that can safely build plain object with only normal init/data fields fast.
    /// </summary>
    private JsValue BuildObjectFast(EvaluationContext context)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;

        // Common case: not inside a generator/async frame (so no property value can suspend the build),
        // all keys distinct, and few enough to use the list backing. Append each property in O(1) via a
        // local tail cursor — no suspension bookkeeping, no duplicate-key probing, and the same
        // allocations as the checked path (no retained tail pointer). Larger or duplicate-keyed literals,
        // and any literal evaluated inside a generator, fall through to the general path below.
        if (suspendable is null
            && _fastKeysDistinct
            && _properties.Length < PropertyDictionary.FixedSizeCutoverPoint)
        {
            return BuildObjectFastListBacked(context, engine);
        }

        ObjectInstance obj;
        PropertyDictionary properties;
        int startIndex;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out ObjectExpressionSuspendData? suspendData))
        {
            obj = suspendData!.Target!;
            properties = suspendData.FastProperties!;
            startIndex = suspendData.NextIndex;
        }
        else
        {
            obj = new JsObject(engine);
            properties = new PropertyDictionary(_properties.Length, checkExistingKeys: true);
            startIndex = 0;
        }

        for (var i = startIndex; i < _properties.Length; i++)
        {
            var propValue = _valueExpressions.GetValue(context, i);

            // Check for generator suspension after each property evaluation
            if (context.IsSuspended())
            {
                if (suspendable is not null)
                {
                    var data = suspendable.Data.GetOrCreate<ObjectExpressionSuspendData>(this);
                    data.Target = obj;
                    data.FastProperties = properties;
                    data.NextIndex = i;
                }
                return JsValue.Undefined;
            }

            properties[_fastKeys![i]] = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
        }

        obj.SetProperties(properties);
        suspendable?.Data.Clear(this);
        return obj;
    }

    /// <summary>
    /// Builds a small object literal whose keys are all distinct, outside any suspendable frame, by
    /// linking the property nodes directly. Each append is O(1) (a local tail cursor, no re-walk), the
    /// pre-hashed keys are reused, and the resulting list-backed dictionary allocates exactly what the
    /// general fast path would — there is no retained tail pointer.
    /// </summary>
    private JsObject BuildObjectFastListBacked(EvaluationContext context, Engine engine)
    {
        var keys = _fastKeys!;

        ListDictionary<PropertyDescriptor>.DictionaryNode? head = null;
        ListDictionary<PropertyDescriptor>.DictionaryNode? tail = null;
        for (var i = 0; i < keys.Length; i++)
        {
            var propValue = _valueExpressions.GetValue(context, i);
            var node = new ListDictionary<PropertyDescriptor>.DictionaryNode
            {
                Key = keys[i],
                Value = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable),
            };

            if (head is null)
            {
                head = tail = node;
            }
            else
            {
                tail!.Next = node;
                tail = node;
            }
        }

        // checkExistingKeys: true matches the general fast path so any post-construction add still
        // dedups; it costs nothing here because the chain is built directly, never via the add path.
        var list = new ListDictionary<PropertyDescriptor>(head!, keys.Length, checkExistingKeys: true);
        var obj = new JsObject(engine);
        obj.SetProperties(new PropertyDictionary(list));
        return obj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object-initializer-runtime-semantics-propertydefinitionevaluation
    /// </summary>
    private object BuildObjectNormal(EvaluationContext context)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;

        ObjectInstance obj;
        int startIndex;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out ObjectExpressionSuspendData? suspendData))
        {
            obj = suspendData!.Target!;
            startIndex = suspendData.NextIndex;
        }
        else
        {
            obj = engine.Realm.Intrinsics.Object.Construct(_properties.Length);
            startIndex = 0;
        }

        for (var i = startIndex; i < _properties.Length; i++)
        {
            var objectProperty = _properties[i];

            if (objectProperty is null)
            {
                // spread
                var spreadValue = _valueExpressions.GetValue(context, i);

                // Check for generator suspension
                if (context.IsSuspended())
                {
                    SaveObjectExpressionSuspendState(suspendable, obj, i);
                    return JsValue.Undefined;
                }

                if (spreadValue is ObjectInstance source)
                {
                    source.CopyDataProperties(obj, excludedItems: null);
                }

                continue;
            }

            var property = objectProperty._value;

            if (property.Method)
            {
                ClassDefinition.MethodDefinitionEvaluation(engine, obj, property, enumerable: true);
                continue;
            }

            JsValue? propName = objectProperty.KeyJsString;
            if (propName is null)
            {
                var value = property.TryGetKey(engine);
                if (context.IsAbrupt())
                {
                    return value;
                }

                // Check for generator suspension after evaluating computed property key
                if (context.IsSuspended())
                {
                    SaveObjectExpressionSuspendState(suspendable, obj, i);
                    return value;
                }

                propName = TypeConverter.ToPropertyKey(value);
            }

            if (property.Kind == PropertyKind.Init)
            {
                if (property.Value is not Expression)
                {
                    Throw.SyntaxError(engine.Realm, $"Invalid property value: expected Expression but found {property.Value.GetType().Name}.");
                }
                var propValue = _valueExpressions.GetValue(context, i)!;

                // Check for generator suspension
                if (context.IsSuspended())
                {
                    SaveObjectExpressionSuspendState(suspendable, obj, i);
                    return JsValue.Undefined;
                }

                if (string.Equals(objectProperty._key, "__proto__", StringComparison.Ordinal) && !objectProperty._value.Computed && !objectProperty._value.Shorthand)
                {
                    if (propValue.IsObject() || propValue.IsNull())
                    {
                        obj.SetPrototypeOf(propValue);
                    }
                    continue;
                }

                if (_valueExpressions.IsAnonymousFunctionDefinition(i))
                {
                    var closure = (Function) propValue;
                    closure.SetFunctionName(propName);
                }

                obj.CreateDataPropertyOrThrow(propName, propValue);
            }
            else if (property.Kind is PropertyKind.Get or PropertyKind.Set)
            {
                var function = objectProperty.GetFunctionDefinition(engine, property);
                var closure = engine.Realm.Intrinsics.Function.OrdinaryFunctionCreate(
                    engine.Realm.Intrinsics.Function.PrototypeObject,
                    function,
                    function.ThisMode,
                    engine.ExecutionContext.LexicalEnvironment,
                    engine.ExecutionContext.PrivateEnvironment);

                closure.SetFunctionName(propName, property.Kind == PropertyKind.Get ? "get" : "set");
                closure.MakeMethod(obj);

                var propDesc = new GetSetPropertyDescriptor(
                    get: property.Kind == PropertyKind.Get ? closure : null,
                    set: property.Kind == PropertyKind.Set ? closure : null,
                    PropertyFlag.Enumerable | PropertyFlag.Configurable);

                obj.DefinePropertyOrThrow(propName, propDesc);
            }
        }

        suspendable?.Data.Clear(this);
        return obj;
    }

    private void SaveObjectExpressionSuspendState(ISuspendable? suspendable, ObjectInstance obj, int nextIndex)
    {
        if (suspendable is not null)
        {
            var data = suspendable.Data.GetOrCreate<ObjectExpressionSuspendData>(this);
            data.Target = obj;
            data.NextIndex = nextIndex;
        }
    }

    internal sealed class JintEmptyObjectExpression : JintExpression
    {
        public static JintEmptyObjectExpression Instance = new(new ObjectExpression(NodeList.From(Array.Empty<Node>())));

        private JintEmptyObjectExpression(Expression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            return new JsObject(context.Engine);
        }

        public override JsValue GetValue(EvaluationContext context)
        {
            return new JsObject(context.Engine);
        }
    }
}

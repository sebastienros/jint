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

    // True when no static key is digit-leading, so a hidden-class shape built from these keys is not
    // guaranteed to be torn down. An integer-like own key reorders ahead of the string keys on first
    // enumeration and forces the object to deopt to a dictionary, so such literals skip the
    // shape-backed build and take the dictionary path directly — matching the spread / JSON.parse /
    // constructor shape paths, which pre-exclude digit-leading keys for the same reason.
    private readonly bool _fastKeysShapeable;

    // The hidden-class shape this literal builds, cached so every execution after the first reuses it
    // (an O(1) build: validate the prototype, allocate a slot array, fill it). Re-derived when the
    // active realm's Object.prototype differs from the one the cached shape is anchored to (a prepared
    // script run across engines/realms); only the last-seen prototype is retained.
    private Shape? _cachedShape;
    private ObjectInstance? _cachedShapeProto;

    // check if we can do a shortcut when all are object properties
    // and don't require duplicate checking
    private readonly bool _canBuildFast;

    // True when no property value contains a lexical yield/await (function boundaries excluded),
    // so evaluating the values can never suspend this frame and the shape-backed build is safe
    // even inside a generator/async function. Computed only when _canBuildFast.
    private readonly bool _valuesCannotSuspend;

    // True when the general build path (BuildObjectNormal) should seed its target in incremental
    // shape-building mode: every property is a spread or a plain init data property (whose stores flow
    // through CreateDataProperty / SetPrototypeOf, both shape-compatible) and no static string key is
    // digit-leading. Getters/setters/methods route through DefinePropertyOrThrow, which deopts a shaped
    // object — don't start a shape that is guaranteed to be torn down. Computed keys stay allowed: the
    // runtime integer-like pre-check catches index-like values and symbol keys land in _symbols
    // without deopt. Only consulted when !_canBuildFast.
    private readonly bool _shapedNormalTarget;

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
        _shapedNormalTarget = true;
        ref readonly var properties = ref expression.Properties;

        // A shape-building target only pays off for the copy idiom (`{ ...src }`): the spread's
        // CopyDataProperties streams keys through CreateDataProperty, interning a shared layout. A
        // non-fast literal WITHOUT a spread reaches BuildObjectNormal for other reasons (a
        // function-valued or computed property, a duplicate key, `__proto__:`); those are one-off
        // shapes with no reuse and drag their own untested deopt edges (e.g. a later
        // data-to-accessor redefine), so they keep the plain dictionary target.
        var hasSpread = false;

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

                // Accessors and methods deopt a shaped target (DefinePropertyOrThrow); a digit-leading
                // static key would deopt at the runtime integer-like pre-check. Static `__proto__:`
                // stays allowed — it routes to SetPrototypeOf, which leaves the shape untouched.
                if (p.Kind != PropertyKind.Init
                    || p.Method
                    || (propName is not null && propName.Length > 0 && char.IsDigit(propName[0])))
                {
                    _shapedNormalTarget = false;
                }
            }
            else if (property is SpreadElement spreadElement)
            {
                // Spread copies via CreateDataProperty — shape-compatible.
                _canBuildFast = false;
                hasSpread = true;
                _properties[i] = null;
                valueExpressions[i] = spreadElement.Argument;
            }
            else
            {
                Throw.ArgumentOutOfRangeException("property", "cannot handle property " + property);
            }

            _canBuildFast &= propName != null;
        }

        if (!hasSpread)
        {
            _shapedNormalTarget = false;
        }

        _valueExpressions.Initialize(valueExpressions.AsSpan());

        if (_canBuildFast)
        {
            // All keys are static names (guaranteed by _canBuildFast); pre-hash them once.
            var keys = new Key[_properties.Length];
            _fastKeysShapeable = true;
            for (var i = 0; i < keys.Length; i++)
            {
                var name = _properties[i]!._key!;
                keys[i] = name;

                // A digit-leading key (an array index or an index-like string) would deopt the shape
                // on first enumeration; keep such literals on the dictionary build instead.
                if (name.Length > 0 && char.IsDigit(name[0]))
                {
                    _fastKeysShapeable = false;
                }
            }
            _fastKeys = keys;
            _fastKeysDistinct = AreKeysDistinct(keys);

            _valuesCannotSuspend = true;
            for (var i = 0; i < valueExpressions.Length; i++)
            {
                if (MaySuspendVisitor.MaySuspend(valueExpressions[i]))
                {
                    _valuesCannotSuspend = false;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Detects whether evaluating a node could suspend the current generator/async frame: any
    /// yield or await reachable without crossing into a nested function (whose suspensions belong
    /// to its own frame). Class nodes are descended conservatively — their computed member keys
    /// evaluate in the enclosing frame.
    /// </summary>
    private static class MaySuspendVisitor
    {
        public static bool MaySuspend(Node node)
        {
            if (node.Type is NodeType.YieldExpression or NodeType.AwaitExpression)
            {
                return true;
            }

            if (node.Type is NodeType.FunctionDeclaration or NodeType.FunctionExpression or NodeType.ArrowFunctionExpression)
            {
                return false;
            }

            foreach (var childNode in node.ChildNodes)
            {
                if (MaySuspend(childNode))
                {
                    return true;
                }
            }

            return false;
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

        // Common case: all keys distinct and either outside any generator/async frame or the property
        // values provably cannot suspend (no lexical yield/await). Build (or reuse) a shared hidden-class
        // shape and a flat slot array — no per-property PropertyDescriptor, no dictionary. Duplicate-keyed
        // literals (e.g. { a:1, a:2 }) and literals whose values may suspend mid-build fall through to the
        // general dictionary path below.
        if (_fastKeysDistinct && _fastKeysShapeable && (suspendable is null || _valuesCannotSuspend))
        {
            return BuildObjectFastShapeBacked(context, engine);
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
    /// Builds an object literal whose keys are all distinct, outside any suspendable frame, as a
    /// hidden-class shape plus a flat slot array. The shape (property names -> slot indices, shared
    /// across all objects of this layout under the same prototype) is derived once and cached on the
    /// node, so every later execution just allocates a single right-sized store array and fills it — no
    /// PropertyDescriptor and no dictionary are allocated per object.
    /// </summary>
    private JsObject BuildObjectFastShapeBacked(EvaluationContext context, Engine engine)
    {
        var keys = _fastKeys!;
        var proto = engine.Realm.Intrinsics.Object.PrototypeObject;

        var shape = _cachedShape;
        if (shape is null || !ReferenceEquals(_cachedShapeProto, proto))
        {
            shape = engine.GetEmptyShape(proto);
            for (var i = 0; i < keys.Length; i++)
            {
                shape = shape.Add(keys[i]);
            }

            _cachedShape = shape;
            _cachedShapeProto = proto;
        }

        // In-object properties: install the shape, then fill slots directly. A layout within the in-object
        // capacity allocates no slot array — the object itself is the only allocation.
        var obj = new JsObject(engine);
        obj.InitShape(shape);
        for (var i = 0; i < keys.Length; i++)
        {
            obj.SetSlot(i, _valueExpressions.GetValue(context, i));
        }

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
            // Shape-building target: spreads and plain init keys flow through CreateDataProperty, so
            // each add transitions a shared interned hidden class instead of filling a dictionary.
            // Unlike BuildObjectFast's pre-installed-shape approach this incremental path is
            // suspension-safe — a key becomes visible only once TryShapeAdd has stored its value and
            // installed the transitioned shape, so a generator suspended between property definitions
            // observes exactly the properties defined so far; the ShapeBuilding flag rides on the
            // object carried in the suspend data, and resumes keep extending the same shape.
            obj = _shapedNormalTarget
                ? engine.Realm.Intrinsics.Object.ConstructShapeBuilding()
                : engine.Realm.Intrinsics.Object.Construct(_properties.Length);
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

                // PropertyDefinitionEvaluation for `...AssignmentExpression` performs
                // CopyDataProperties(object, fromValue, « ») — https://tc39.es/ecma262/#sec-copydataproperties
                // step 1 returns early for undefined/null sources and step 2 ToObject's any other
                // primitive, so e.g. spreading a string copies its index properties.
                if (spreadValue is ObjectInstance source)
                {
                    source.CopyDataProperties(obj, excludedItems: null);
                }
                else if (!spreadValue.IsNullOrUndefined())
                {
                    TypeConverter.ToObject(engine.Realm, spreadValue).CopyDataProperties(obj, excludedItems: null);
                }

                continue;
            }

            var property = objectProperty._value;

            if (property.Method)
            {
                ClassDefinition.MethodDefinitionEvaluation(engine, obj, property, enumerable: true);

                // Check for generator suspension after evaluating a computed method key (mirrors the
                // non-method path below) — without this the build loop would keep executing subsequent
                // properties while the generator is suspended.
                if (context.IsSuspended())
                {
                    SaveObjectExpressionSuspendState(suspendable, obj, i);
                    return JsValue.Undefined;
                }

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

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

internal sealed class ClassDefinition
{
    private static readonly MethodDefinition _superConstructor;
    internal static CallExpression _defaultSuperCall;

    internal static readonly MethodDefinition _emptyConstructor;

    internal readonly string? _className;
    private readonly Expression? _superClass;
    private readonly ClassBody _body;

    static ClassDefinition()
    {
        var parser = new Parser(Engine.BaseParserOptions);

        // generate missing constructor AST only once
        static MethodDefinition CreateConstructorMethodDefinition(Parser parser, string source)
        {
            var script = parser.ParseScriptGuarded(new Engine().Realm, source);
            var classDeclaration = (ClassDeclaration) script.Body[0];
            return (MethodDefinition) classDeclaration.Body.Body[0];
        }

        _superConstructor = CreateConstructorMethodDefinition(parser, "class temp extends X { constructor(...args) { super(...args); } }");
        _defaultSuperCall = (CallExpression) ((NonSpecialExpressionStatement) _superConstructor.Value.Body.Body[0]).Expression;
        _emptyConstructor = CreateConstructorMethodDefinition(parser, "class temp { constructor() {} }");
    }

    private readonly IClass _class;

    public ClassDefinition(
        string? className,
        IClass @class)
    {
        _className = className;
        _superClass = @class.SuperClass;
        _body = @class.Body;
        _class = @class;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation
    /// </summary>
    public JsValue BuildConstructor(EvaluationContext context, Environment env, string? classBinding = null)
    {
        // A class definition is always strict mode code.
        using var _ = new StrictModeScope(true, true);

        var engine = context.Engine;
        var classEnv = JintEnvironment.NewDeclarativeEnvironment(engine, env);

        if (_className is not null)
        {
            classEnv.CreateImmutableBinding(_className, true);
        }

        var outerPrivateEnvironment = engine.ExecutionContext.PrivateEnvironment;
        var classPrivateEnvironment = JintEnvironment.NewPrivateEnvironment(engine, outerPrivateEnvironment);

        // Step: Evaluate all decorator expressions in the current environment (before entering class scope)
        // Decorator expressions are evaluated left-to-right, top-to-bottom in source order.
        var hasDecorators = HasDecorators();
        JsValue[]? classDecoratorValues = null;
        List<ElementDecoratorPair>? elementDecorators = null;

        if (hasDecorators)
        {
            // Evaluate class-level decorators
            classDecoratorValues = EvaluateDecorators(context, _class.Decorators);

            // Evaluate element-level decorators
            ref readonly var bodyElements = ref _body.Body;
            elementDecorators = new List<ElementDecoratorPair>(bodyElements.Count);
            for (var i = 0; i < bodyElements.Count; i++)
            {
                var elem = (IClassElement) bodyElements[i];
                JsValue[] decs;
                if (elem is MethodDefinition md && md.Decorators.Count > 0)
                {
                    decs = EvaluateDecorators(context, md.Decorators);
                }
                else if (elem is PropertyDefinition pd && pd.Decorators.Count > 0)
                {
                    decs = EvaluateDecorators(context, pd.Decorators);
                }
                else if (elem is AccessorProperty ap2 && ap2.Decorators.Count > 0)
                {
                    decs = EvaluateDecorators(context, ap2.Decorators);
                }
                else
                {
                    decs = System.Array.Empty<JsValue>();
                }
                elementDecorators.Add(new ElementDecoratorPair(elem, decs));
            }
        }

        ObjectInstance? protoParent = null;
        ObjectInstance? constructorParent = null;
        if (_superClass is null)
        {
            protoParent = engine.Realm.Intrinsics.Object.PrototypeObject;
            constructorParent = engine.Realm.Intrinsics.Function.PrototypeObject;
        }
        else
        {
            engine.UpdateLexicalEnvironment(classEnv);
            var superclass = JintExpression.Build(_superClass).GetValue(context);
            engine.UpdateLexicalEnvironment(env);

            if (superclass.IsNull())
            {
                protoParent = null;
                constructorParent = engine.Realm.Intrinsics.Function.PrototypeObject;
            }
            else if (!superclass.IsConstructor)
            {
                Throw.TypeError(engine.Realm, "super class is not a constructor");
            }
            else
            {
                var temp = superclass.Get(CommonProperties.Prototype);
                if (temp is ObjectInstance protoParentObject)
                {
                    protoParent = protoParentObject;
                }
                else if (temp.IsNull())
                {
                    // OK
                }
                else
                {
                    Throw.TypeError(engine.Realm, "cannot resolve super class prototype chain");
                    return default;
                }

                constructorParent = (ObjectInstance) superclass;
            }
        }

        ObjectInstance proto = new JsObject(engine) { _prototype = protoParent };

        var privateBoundIdentifiers = new HashSet<PrivateIdentifier>(PrivateIdentifierNameComparer._instance);
        MethodDefinition? constructor = null;
        ref readonly var elements = ref _body.Body;
        var classBody = elements;
        for (var i = 0; i < classBody.Count; ++i)
        {
            var element = classBody[i];
            if (element is MethodDefinition { Kind: PropertyKind.Constructor } c)
            {
                constructor = c;
            }

            privateBoundIdentifiers.Clear();
            element.PrivateBoundIdentifiers(privateBoundIdentifiers);
            foreach (var name in privateBoundIdentifiers)
            {
                classPrivateEnvironment.Names.Add(name, new PrivateName(name));
            }
        }

        constructor ??= _superClass != null
            ? _superConstructor
            : _emptyConstructor;

        engine.UpdateLexicalEnvironment(classEnv);
        engine.UpdatePrivateEnvironment(classPrivateEnvironment);

        ScriptFunction F;
        try
        {
            var constructorInfo = constructor.DefineMethod(proto, constructorParent, _class);
            F = constructorInfo.Closure;

            F.SetFunctionName(_className ?? classBinding ?? "");

            F.MakeConstructor(writableProperty: false, proto);
            F._constructorKind = _superClass is null ? ConstructorKind.Base : ConstructorKind.Derived;
            F.MakeClassConstructor();
            proto.CreateMethodProperty(CommonProperties.Constructor, F);

            var instancePrivateMethods = new List<PrivateElement>();
            var staticPrivateMethods = new List<PrivateElement>();
            var instanceFields = new List<ClassFieldDefinition>();
            var staticElements = new List<object>();

            // Collect extra initializers from decorators
            List<ICallable>? instanceExtraInitializers = hasDecorators ? new List<ICallable>() : null;
            List<ICallable>? staticExtraInitializers = hasDecorators ? new List<ICallable>() : null;

            var elementIndex = 0;
            foreach (IClassElement e in elements)
            {
                if (e is MethodDefinition { Kind: PropertyKind.Constructor })
                {
                    elementIndex++;
                    continue;
                }

                var isStatic = e.Static;
                var target = !isStatic ? proto : F;
                var currentDecorators = elementDecorators is not null && elementIndex < elementDecorators.Count
                    ? elementDecorators[elementIndex].Decorators
                    : [];

                // Handle auto-accessors specially since they produce both a field definition and optionally a private element
                if (e is AccessorProperty ap)
                {
                    var result = AutoAccessorDefinitionEvaluation(engine, target, ap, isStatic);

                    // Check for generator suspension after evaluating computed property key
                    if (engine.ExecutionContext.Suspended)
                    {
                        return JsValue.Undefined;
                    }

                    // Apply accessor decorators if present
                    if (currentDecorators.Length > 0)
                    {
                        var propName = ap.GetKey(engine);
                        var isPrivateAccessor = ap.Key is PrivateIdentifier;
                        var extraInits = isStatic ? staticExtraInitializers! : instanceExtraInitializers!;

                        JsValue currentGetter;
                        JsValue currentSetter;

                        if (result.PrivateElement is not null)
                        {
                            currentGetter = result.PrivateElement.Get ?? JsValue.Undefined;
                            currentSetter = result.PrivateElement.Set ?? JsValue.Undefined;
                        }
                        else
                        {
                            // For public auto-accessors, retrieve getter/setter from the property we just defined
                            var desc = target.GetOwnProperty(propName);
                            currentGetter = desc?.Get ?? JsValue.Undefined;
                            currentSetter = desc?.Set ?? JsValue.Undefined;
                        }

                        var accessorResult = ApplyAccessorDecorators(engine, currentDecorators, currentGetter, currentSetter, propName, isStatic, isPrivateAccessor, extraInits);

                        // Update getter/setter if decorators returned replacements
                        if (result.PrivateElement is not null)
                        {
                            result.PrivateElement.Get = accessorResult.Getter;
                            result.PrivateElement.Set = accessorResult.Setter;
                        }
                        else
                        {
                            target.DefinePropertyOrThrow(propName, new GetSetPropertyDescriptor(
                                accessorResult.Getter is ICallable ? accessorResult.Getter : null,
                                accessorResult.Setter is ICallable ? accessorResult.Setter : null,
                                PropertyFlag.Configurable));
                        }

                        // If decorators returned init functions, wrap the field initializer
                        if (accessorResult.InitFunctions is { Count: > 0 })
                        {
                            var fieldDef = result.FieldDefinition;
                            var originalInit = fieldDef.Initializer;
                            var capturedInits = accessorResult.InitFunctions.ToArray();

                            var wrappedInit = new ClrFunction(engine, "", (thisObj, _) =>
                            {
                                var initValue = originalInit is not null
                                    ? engine.Call(originalInit, thisObj, Arguments.Empty)
                                    : JsValue.Undefined;

                                for (var j = 0; j < capturedInits.Length; j++)
                                {
                                    initValue = engine.Call((JsValue) capturedInits[j], thisObj, new JsValue[] { initValue });
                                }
                                return initValue;
                            }, 0, PropertyFlag.Configurable);

                            fieldDef.Initializer = wrappedInit;
                        }
                    }

                    // Add the backing field initializer
                    if (!isStatic)
                    {
                        instanceFields.Add(result.FieldDefinition);
                    }
                    else
                    {
                        staticElements.Add(result.FieldDefinition);
                    }

                    // Add private element if present (for private auto-accessors)
                    if (result.PrivateElement is not null)
                    {
                        var container = !isStatic ? instancePrivateMethods : staticPrivateMethods;
                        container.Add(result.PrivateElement);
                    }

                    elementIndex++;
                    continue;
                }

                var element = ClassElementEvaluation(engine, target, e);

                // Check for generator suspension after evaluating class element
                if (engine.ExecutionContext.Suspended)
                {
                    return JsValue.Undefined;
                }

                // Apply decorators to methods, getters, setters
                if (currentDecorators.Length > 0 && e is MethodDefinition md)
                {
                    var propName = md.GetKey(engine);
                    var isPrivateMethod = md.Key is PrivateIdentifier;
                    var extraInits = isStatic ? staticExtraInitializers! : instanceExtraInitializers!;

                    if (md.Kind == PropertyKind.Get || md.Kind == PropertyKind.Set)
                    {
                        var kind = md.Kind == PropertyKind.Get ? "getter" : "setter";
                        if (element is PrivateElement pe && pe.Kind == PrivateElementKind.Accessor)
                        {
                            var decoratedValue = md.Kind == PropertyKind.Get ? pe.Get! : pe.Set!;
                            var replacement = ApplyMethodDecorators(engine, currentDecorators, decoratedValue, kind, propName, isStatic, isPrivateMethod, extraInits);
                            if (md.Kind == PropertyKind.Get)
                            {
                                pe.Get = replacement;
                            }
                            else
                            {
                                pe.Set = replacement;
                            }
                        }
                        else
                        {
                            // Public getter/setter: get from the property descriptor
                            var desc = target.GetOwnProperty(propName);
                            if (desc is not null)
                            {
                                var decoratedValue = md.Kind == PropertyKind.Get ? desc.Get ?? JsValue.Undefined : desc.Set ?? JsValue.Undefined;
                                var replacement = ApplyMethodDecorators(engine, currentDecorators, decoratedValue, kind, propName, isStatic, isPrivateMethod, extraInits);
                                target.DefinePropertyOrThrow(propName, new GetSetPropertyDescriptor(
                                    md.Kind == PropertyKind.Get ? replacement : desc.Get,
                                    md.Kind == PropertyKind.Set ? replacement : desc.Set,
                                    PropertyFlag.Configurable));
                            }
                        }
                    }
                    else
                    {
                        // Regular method
                        if (element is PrivateElement pe)
                        {
                            var replacement = ApplyMethodDecorators(engine, currentDecorators, pe.Value!, "method", propName, isStatic, isPrivateMethod, extraInits);
                            pe.Value = replacement;
                        }
                        else
                        {
                            // Public method: get from the property descriptor
                            var desc = target.GetOwnProperty(propName);
                            if (desc?.Value is not null)
                            {
                                var replacement = ApplyMethodDecorators(engine, currentDecorators, desc.Value, "method", propName, isStatic, isPrivateMethod, extraInits);
                                target.DefinePropertyOrThrow(propName, new PropertyDescriptor(replacement, PropertyFlag.NonEnumerable));
                            }
                        }
                    }
                }

                // Apply decorators to fields
                if (currentDecorators.Length > 0 && e is PropertyDefinition pd)
                {
                    if (element is ClassFieldDefinition fieldDef)
                    {
                        var propName = pd.GetKey(engine);
                        var isPrivateField = pd.Key is PrivateIdentifier;
                        var extraInits = isStatic ? staticExtraInitializers! : instanceExtraInitializers!;
                        ApplyFieldDecorators(engine, currentDecorators, fieldDef, propName, isStatic, isPrivateField, extraInits);
                    }
                }

                if (element is PrivateElement privateElement)
                {
                    var container = !isStatic ? instancePrivateMethods : staticPrivateMethods;
                    var index = container.FindIndex(x => string.Equals(x.Key.Description, privateElement.Key.Description, StringComparison.Ordinal));
                    if (index != -1)
                    {
                        var pe = container[index];
                        var combined = privateElement.Get is null
                            ? new PrivateElement { Key = pe.Key, Kind = PrivateElementKind.Accessor, Get = pe.Get, Set = privateElement.Set }
                            : new PrivateElement { Key = pe.Key, Kind = PrivateElementKind.Accessor, Get = privateElement.Get, Set = pe.Set };

                        container[index] = combined;
                    }
                    else
                    {
                        container.Add(privateElement);
                    }
                }
                else if (element is ClassFieldDefinition classFieldDefinition)
                {
                    if (!isStatic)
                    {
                        instanceFields.Add(classFieldDefinition);
                    }
                    else
                    {
                        staticElements.Add(element);
                    }
                }
                else if (element is ClassStaticBlockDefinition)
                {
                    staticElements.Add(element);
                }

                elementIndex++;
            }

            if (_className is not null)
            {
                classEnv.InitializeBinding(_className, F, DisposeHint.Normal);
            }

            // Store extra initializers so they run during instance/static initialization
            if (instanceExtraInitializers is { Count: > 0 })
            {
                // Add a field definition that runs the extra initializers
                var capturedInits = instanceExtraInitializers.ToArray();
                var initRunner = new ClrFunction(engine, "", (thisObj, _) =>
                {
                    for (var j = 0; j < capturedInits.Length; j++)
                    {
                        engine.Call((JsValue) capturedInits[j], thisObj, Arguments.Empty);
                    }
                    return JsValue.Undefined;
                }, 0, PropertyFlag.Configurable);
                instanceFields.Add(new ClassFieldDefinition
                {
                    Name = JsValue.Undefined,
                    Initializer = initRunner
                });
            }

            F._privateMethods = instancePrivateMethods;
            F._fields = instanceFields;

            for (var i = 0; i < staticPrivateMethods.Count; i++)
            {
                F.PrivateMethodOrAccessorAdd(staticPrivateMethods[i]);
            }

            for (var i = 0; i < staticElements.Count; i++)
            {
                var elementRecord = staticElements[i];
                if (elementRecord is ClassFieldDefinition classFieldDefinition)
                {
                    ObjectInstance.DefineField(F, classFieldDefinition);
                }
                else
                {
                    engine.Call(((ClassStaticBlockDefinition) elementRecord).BodyFunction, F);
                }
            }

            // Run static extra initializers
            RunExtraInitializers(engine, F, staticExtraInitializers);
        }
        finally
        {
            engine.UpdateLexicalEnvironment(env);
            engine.UpdatePrivateEnvironment(outerPrivateEnvironment);
        }

        // Apply class decorators (after class is fully constructed)
        if (classDecoratorValues is { Length: > 0 })
        {
            var classExtraInitializers = new List<ICallable>();
            // Apply class decorators in reverse order
            JsValue classValue = F;
            for (var i = classDecoratorValues.Length - 1; i >= 0; i--)
            {
                var decorator = classDecoratorValues[i];
                if (decorator is not ICallable callable)
                {
                    Throw.TypeError(engine.Realm, "A decorator must be a function");
                    return F;
                }

                var decoratorContext = CreateDecoratorContext(engine, "class", new JsString(_className ?? classBinding ?? ""), isStatic: false, isPrivate: false, classExtraInitializers);
                var result = engine.Call((JsValue) callable, JsValue.Undefined, new JsValue[] { classValue, decoratorContext });

                if (!result.IsUndefined())
                {
                    if (result is not ICallable)
                    {
                        Throw.TypeError(engine.Realm, "A class decorator must return either undefined or a function");
                        return F;
                    }
                    classValue = result;
                }
            }

            // Run class extra initializers on the (possibly replaced) class
            RunExtraInitializers(engine, classValue, classExtraInitializers);

            return classValue;
        }

        return F;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-static-semantics-classelementevaluation
    /// </summary>
    private static object? ClassElementEvaluation(Engine engine, ObjectInstance target, IClassElement e)
    {
        return e switch
        {
            PropertyDefinition p => ClassFieldDefinitionEvaluation(engine, target, p),
            MethodDefinition m => MethodDefinitionEvaluation(engine, target, m, enumerable: false),
            StaticBlock s => ClassStaticBlockDefinitionEvaluation(engine, target, s),
            AccessorProperty ap => null, // handled directly in BuildConstructor
            _ => null
        };
    }

    /// <summary>
    /// Result of auto-accessor definition evaluation.
    /// Contains the field initializer and an optional private element (for private auto-accessors).
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct AutoAccessorResult(
        ClassFieldDefinition FieldDefinition,
        PrivateElement? PrivateElement);

    /// <summary>
    /// https://tc39.es/proposal-decorators/#sec-autoaccessordefinitionevaluation
    /// Evaluates an auto-accessor property and returns a field initializer plus optional private element.
    /// </summary>
    private static AutoAccessorResult AutoAccessorDefinitionEvaluation(
        Engine engine,
        ObjectInstance target,
        AccessorProperty accessorProperty,
        bool isStatic)
    {
        var name = accessorProperty.GetKey(engine);

        // Check for generator suspension after evaluating computed property key
        if (engine.ExecutionContext.Suspended)
        {
            return new AutoAccessorResult(
                new ClassFieldDefinition { Name = JsValue.Undefined, Initializer = null },
                null);
        }

        var isPrivate = accessorProperty.Key is PrivateIdentifier;

        // Create a unique backing storage private name
        var backingName = new PrivateName(isPrivate ? $"{name}_backing" : $"accessor_backing");

        if (isPrivate)
        {
            // For private auto-accessors, use PrivateGet/PrivateSet for the backing field
            var privateEnv = engine.ExecutionContext.PrivateEnvironment;
            var privateName = privateEnv!.Names[(PrivateIdentifier) accessorProperty.Key];

            var getter = new ClrFunction(engine, "get", (thisObj, _) =>
            {
                return thisObj.AsObject().PrivateGet(backingName);
            }, 0, PropertyFlag.Configurable);

            var setter = new ClrFunction(engine, "set", (thisObj, args) =>
            {
                thisObj.AsObject().PrivateSet(backingName, args.At(0));
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            // Create field initializer that sets the backing private field
            ScriptFunction? initializer = null;
            if (accessorProperty.Value is not null)
            {
                var intrinsics = engine.Realm.Intrinsics;
                var env = engine.ExecutionContext.LexicalEnvironment;
                var privEnv = engine.ExecutionContext.PrivateEnvironment;

                var definition = new JintFunctionDefinition(new ClassFieldFunction(accessorProperty.Value));
                initializer = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.Function.PrototypeObject, definition, FunctionThisMode.Global, env, privEnv);
                initializer.MakeMethod(target);
                initializer._classFieldInitializerName = name;
            }

            var fieldDef = new ClassFieldDefinition { Name = backingName, Initializer = initializer };
            var pe = new PrivateElement
            {
                Key = privateName,
                Kind = PrivateElementKind.Accessor,
                Get = getter,
                Set = setter
            };

            return new AutoAccessorResult(fieldDef, pe);
        }
        else
        {
            // For public auto-accessors, use a private backing name for the storage
            // This ensures that static auto-accessors are per-class (derived classes
            // calling inherited getter/setter will throw TypeError because they don't
            // have the backing private element).
            var getter = new ClrFunction(engine, "get", (thisObj, _) =>
            {
                return thisObj.AsObject().PrivateGet(backingName);
            }, 0, PropertyFlag.Configurable);

            var setter = new ClrFunction(engine, "set", (thisObj, args) =>
            {
                thisObj.AsObject().PrivateSet(backingName, args.At(0));
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            // Define the accessor property (getter/setter) on the target
            target.DefinePropertyOrThrow(name, new GetSetPropertyDescriptor(getter, setter, PropertyFlag.Configurable));

            // Create field initializer for the backing storage
            ScriptFunction? initializer = null;
            if (accessorProperty.Value is not null)
            {
                var intrinsics = engine.Realm.Intrinsics;
                var env = engine.ExecutionContext.LexicalEnvironment;
                var privEnv = engine.ExecutionContext.PrivateEnvironment;

                var definition = new JintFunctionDefinition(new ClassFieldFunction(accessorProperty.Value));
                initializer = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.Function.PrototypeObject, definition, FunctionThisMode.Global, env, privEnv);
                initializer.MakeMethod(target);
                initializer._classFieldInitializerName = name;
            }

            var fieldDef = new ClassFieldDefinition { Name = backingName, Initializer = initializer };
            return new AutoAccessorResult(fieldDef, null);
        }
    }

    /// <summary>
    /// /https://tc39.es/ecma262/#sec-runtime-semantics-classfielddefinitionevaluation
    /// </summary>
    private static ClassFieldDefinition ClassFieldDefinitionEvaluation(Engine engine, ObjectInstance homeObject, PropertyDefinition fieldDefinition)
    {
        var name = fieldDefinition.GetKey(engine);

        // Check for generator suspension after evaluating computed property key
        if (engine.ExecutionContext.Suspended)
        {
            return new ClassFieldDefinition { Name = JsValue.Undefined, Initializer = null };
        }

        ScriptFunction? initializer = null;
        if (fieldDefinition.Value is not null)
        {
            var intrinsics = engine.Realm.Intrinsics;
            var env = engine.ExecutionContext.LexicalEnvironment;
            var privateEnv = engine.ExecutionContext.PrivateEnvironment;

            var definition = new JintFunctionDefinition(new ClassFieldFunction(fieldDefinition.Value));
            initializer = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.Function.PrototypeObject, definition, FunctionThisMode.Global, env, privateEnv);

            initializer.MakeMethod(homeObject);
            initializer._classFieldInitializerName = name;
        }

        return new ClassFieldDefinition { Name = name, Initializer = initializer };
    }

    private sealed class ClassFieldFunction : Node, IFunction
    {
        private readonly NodeList<Node> _nodeList;
        private readonly FunctionBody _statement;

        public ClassFieldFunction(Expression expression) : base(NodeType.ExpressionStatement)
        {
            var nodeList = NodeList.From<Statement>(new ReturnStatement(expression));
            _statement = new FunctionBody(nodeList, strict: true);
        }

        protected override object Accept(AstVisitor visitor) => throw new NotImplementedException();

        public Identifier? Id => null;

        public ref readonly NodeList<Node> Params => ref _nodeList;

        public StatementOrExpression Body => _statement;
        public bool Generator => false;
        public bool Expression => false;
        public bool Async => false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-classstaticblockdefinitionevaluation
    /// </summary>
    private static ClassStaticBlockDefinition ClassStaticBlockDefinitionEvaluation(Engine engine, ObjectInstance homeObject, StaticBlock o)
    {
        var intrinsics = engine.Realm.Intrinsics;

        var definition = new JintFunctionDefinition(new ClassStaticBlockFunction(o));

        var lex = engine.ExecutionContext.LexicalEnvironment;
        var privateEnv = engine.ExecutionContext.PrivateEnvironment;

        var bodyFunction = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.Function.PrototypeObject, definition, FunctionThisMode.Global, lex, privateEnv);

        bodyFunction.MakeMethod(homeObject);

        return new ClassStaticBlockDefinition { BodyFunction = bodyFunction };
    }

    private sealed class ClassStaticBlockFunction : Node, IFunction
    {
        private readonly FunctionBody _statement;
        private readonly NodeList<Node> _params;

        public ClassStaticBlockFunction(StaticBlock staticBlock) : base(NodeType.StaticBlock)
        {
            _statement = new FunctionBody(staticBlock.Body, strict: true);
            _params = new NodeList<Node>();
        }

        protected override object Accept(AstVisitor visitor) => throw new NotImplementedException();

        public Identifier? Id => null;
        public ref readonly NodeList<Node> Params => ref _params;
        public StatementOrExpression Body => _statement;
        public bool Generator => false;
        public bool Expression => false;
        public bool Async => false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-methoddefinitionevaluation
    /// </summary>
    internal static PrivateElement? MethodDefinitionEvaluation<T>(
        Engine engine,
        ObjectInstance obj,
        T method,
        bool enumerable) where T : IProperty
    {
        var function = method.Value as IFunction;
        if (function is null)
        {
            Throw.SyntaxError(obj.Engine.Realm);
        }

        if (method.Kind != PropertyKind.Get && method.Kind != PropertyKind.Set && !function.Generator)
        {
            var methodDef = method.DefineMethod(obj);
            methodDef.Closure.SetFunctionName(methodDef.Key);
            return DefineMethodProperty(obj, methodDef.Key, methodDef.Closure, enumerable);
        }

        var sourceTextStart = method is MethodDefinition { Static: true } ? ~method.RangeRef.Start : method.RangeRef.Start;
        var sourceTextEnd = method.RangeRef.End;

        var definition = new JintFunctionDefinition(function, sourceTextStart, sourceTextEnd);
        var intrinsics = engine.Realm.Intrinsics;

        var value = method.TryGetKey(engine);

        // Check for generator suspension after evaluating computed property key
        if (engine.ExecutionContext.Suspended)
        {
            return null;
        }

        var propKey = TypeConverter.ToPropertyKey(value);
        var env = engine.ExecutionContext.LexicalEnvironment;
        var privateEnv = engine.ExecutionContext.PrivateEnvironment;

        if (function.Generator)
        {
            Prototype functionPrototype = function.Async
                ? intrinsics.AsyncGeneratorFunction.PrototypeObject
                : intrinsics.GeneratorFunction.PrototypeObject;

            var closure = intrinsics.Function.OrdinaryFunctionCreate(functionPrototype!, definition, definition.ThisMode, env, privateEnv);
            closure.MakeMethod(obj);
            closure.SetFunctionName(propKey);

            ObjectInstance closurePrototype = function.Async
                ? intrinsics.AsyncGeneratorFunction.PrototypeObject.PrototypeObject
                : intrinsics.GeneratorFunction.PrototypeObject.PrototypeObject;

            var prototype = ObjectInstance.OrdinaryObjectCreate(engine, closurePrototype);
            closure.DefinePropertyOrThrow(CommonProperties.Prototype, new PropertyDescriptor(prototype, PropertyFlag.Writable));
            return DefineMethodProperty(obj, propKey, closure, enumerable);
        }
        else
        {
            var getter = method.Kind == PropertyKind.Get;

            var closure = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.Function.PrototypeObject, definition, definition.ThisMode, env, privateEnv);
            closure.MakeMethod(obj);
            closure.SetFunctionName(propKey, getter ? "get" : "set");

            if (method.Key is PrivateIdentifier privateIdentifier)
            {
                return new PrivateElement
                {
                    Key = privateEnv!.Names[privateIdentifier],
                    Kind = PrivateElementKind.Accessor,
                    Get = getter ? closure : null,
                    Set = !getter ? closure : null
                };
            }

            var propDesc = new GetSetPropertyDescriptor(
                getter ? closure : null,
                !getter ? closure : null,
                PropertyFlag.Configurable);

            obj.DefinePropertyOrThrow(propKey, propDesc);
        }

        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-definemethodproperty
    /// </summary>
    private static PrivateElement? DefineMethodProperty(ObjectInstance homeObject, JsValue key, ScriptFunction closure, bool enumerable)
    {
        if (key.IsPrivateName())
        {
            return new PrivateElement { Key = (PrivateName) key, Kind = PrivateElementKind.Method, Value = closure };
        }

        var desc = new PropertyDescriptor(closure, enumerable ? PropertyFlag.ConfigurableEnumerableWritable : PropertyFlag.NonEnumerable);
        homeObject.DefinePropertyOrThrow(key, desc);
        return null;
    }

    /// <summary>
    /// Pairs a class element with its evaluated decorator values.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct ElementDecoratorPair(IClassElement Element, JsValue[] Decorators);

    /// <summary>
    /// Evaluates all decorator expressions in source order (left-to-right).
    /// Returns array of evaluated decorator functions.
    /// </summary>
    private static JsValue[] EvaluateDecorators(EvaluationContext context, in NodeList<Decorator> decorators)
    {
        if (decorators.Count == 0)
        {
            return [];
        }

        var result = new JsValue[decorators.Count];
        for (var i = 0; i < decorators.Count; i++)
        {
            result[i] = JintExpression.Build(decorators[i].Expression).GetValue(context);
        }
        return result;
    }

    /// <summary>
    /// Creates the decorator context object passed as the second argument to a decorator.
    /// https://tc39.es/proposal-decorators/#sec-createdecoratorcontextobject
    /// </summary>
    private static JsObject CreateDecoratorContext(
        Engine engine,
        string kind,
        JsValue name,
        bool isStatic,
        bool isPrivate,
        List<ICallable> extraInitializers)
    {
        var context = new JsObject(engine);
        context.FastSetDataProperty("kind", new JsString(kind));
        context.FastSetDataProperty("name", isPrivate && name is PrivateName pn ? (JsValue) new JsString(pn.Description) : name);
        context.FastSetDataProperty("static", isStatic ? JsBoolean.True : JsBoolean.False);
        context.FastSetDataProperty("private", isPrivate ? JsBoolean.True : JsBoolean.False);

        var addInitializer = new ClrFunction(engine, "addInitializer", (_, args) =>
        {
            var init = args.At(0);
            if (init is not ICallable initCallable)
            {
                Throw.TypeError(engine.Realm, "An initializer must be a function");
                return JsValue.Undefined;
            }
            extraInitializers.Add(initCallable);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        context.FastSetDataProperty("addInitializer", addInitializer);

        return context;
    }

    /// <summary>
    /// Applies decorators to a method, getter, or setter.
    /// Returns the potentially replaced function value.
    /// </summary>
    private static JsValue ApplyMethodDecorators(
        Engine engine,
        JsValue[] decorators,
        JsValue value,
        string kind,
        JsValue name,
        bool isStatic,
        bool isPrivate,
        List<ICallable> extraInitializers)
    {
        // Apply decorators in reverse order (last decorator first)
        for (var i = decorators.Length - 1; i >= 0; i--)
        {
            var decorator = decorators[i];
            if (decorator is not ICallable callable)
            {
                Throw.TypeError(engine.Realm, "A decorator must be a function");
                return value;
            }

            var context = CreateDecoratorContext(engine, kind, name, isStatic, isPrivate, extraInitializers);
            var result = engine.Call((JsValue) callable, JsValue.Undefined, new JsValue[] { value, context });

            if (!result.IsUndefined())
            {
                if (result is not ICallable)
                {
                    Throw.TypeError(engine.Realm, "A decorator must return either undefined or a function");
                    return value;
                }
                value = result;
            }
        }

        return value;
    }

    /// <summary>
    /// Applies decorators to a field definition.
    /// Returns the potentially wrapped initializer.
    /// </summary>
    private static void ApplyFieldDecorators(
        Engine engine,
        JsValue[] decorators,
        ClassFieldDefinition fieldDefinition,
        JsValue name,
        bool isStatic,
        bool isPrivate,
        List<ICallable> extraInitializers)
    {
        var initFunctions = new List<ICallable>();

        // Apply decorators in reverse order
        for (var i = decorators.Length - 1; i >= 0; i--)
        {
            var decorator = decorators[i];
            if (decorator is not ICallable callable)
            {
                Throw.TypeError(engine.Realm, "A decorator must be a function");
                return;
            }

            var context = CreateDecoratorContext(engine, "field", name, isStatic, isPrivate, extraInitializers);
            var result = engine.Call((JsValue) callable, JsValue.Undefined, new JsValue[] { JsValue.Undefined, context });

            if (!result.IsUndefined())
            {
                if (result is not ICallable initCallable)
                {
                    Throw.TypeError(engine.Realm, "A decorator must return either undefined or a function");
                    return;
                }
                initFunctions.Add(initCallable);
            }
        }

        if (initFunctions.Count > 0)
        {
            var originalInitializer = fieldDefinition.Initializer;
            var capturedInits = initFunctions.ToArray();

            // Wrap the initializer to chain through decorator init functions
            // Init functions are applied in the order they were collected
            // (which is reverse decorator order = inner decorator first)
            var wrappedInitializer = new ClrFunction(engine, "", (thisObj, _) =>
            {
                var initValue = originalInitializer is not null
                    ? engine.Call(originalInitializer, thisObj, Arguments.Empty)
                    : JsValue.Undefined;

                for (var j = 0; j < capturedInits.Length; j++)
                {
                    initValue = engine.Call((JsValue) capturedInits[j], thisObj, new JsValue[] { initValue });
                }
                return initValue;
            }, 0, PropertyFlag.Configurable);

            fieldDefinition.Initializer = wrappedInitializer;
        }
    }

    /// <summary>
    /// Applies decorators to an auto-accessor.
    /// Returns potentially replaced getter/setter and optional init function.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct AccessorDecoratorResult(
        JsValue Getter,
        JsValue Setter,
        List<ICallable>? InitFunctions);

    private static AccessorDecoratorResult ApplyAccessorDecorators(
        Engine engine,
        JsValue[] decorators,
        JsValue getter,
        JsValue setter,
        JsValue name,
        bool isStatic,
        bool isPrivate,
        List<ICallable> extraInitializers)
    {
        var currentGetter = getter;
        var currentSetter = setter;
        List<ICallable>? initFunctions = null;

        // Apply decorators in reverse order
        for (var i = decorators.Length - 1; i >= 0; i--)
        {
            var decorator = decorators[i];
            if (decorator is not ICallable callable)
            {
                Throw.TypeError(engine.Realm, "A decorator must be a function");
                return new AccessorDecoratorResult(currentGetter, currentSetter, initFunctions);
            }

            var context = CreateDecoratorContext(engine, "accessor", name, isStatic, isPrivate, extraInitializers);
            var valueObj = ObjectInstance.OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
            valueObj.FastSetDataProperty("get", currentGetter);
            valueObj.FastSetDataProperty("set", currentSetter);

            var result = engine.Call((JsValue) callable, JsValue.Undefined, new JsValue[] { valueObj, context });

            if (!result.IsUndefined())
            {
                if (result is not ObjectInstance resultObj)
                {
                    Throw.TypeError(engine.Realm, "A decorator must return either undefined or an object");
                    return new AccessorDecoratorResult(currentGetter, currentSetter, initFunctions);
                }

                var newGet = resultObj.Get("get");
                if (!newGet.IsUndefined())
                {
                    currentGetter = newGet;
                }

                var newSet = resultObj.Get("set");
                if (!newSet.IsUndefined())
                {
                    currentSetter = newSet;
                }

                var initValue = resultObj.Get("init");
                if (!initValue.IsUndefined())
                {
                    if (initValue is not ICallable initCallable)
                    {
                        Throw.TypeError(engine.Realm, "An accessor decorator's init must be a function");
                        return new AccessorDecoratorResult(currentGetter, currentSetter, initFunctions);
                    }
                    initFunctions ??= [];
                    initFunctions.Add(initCallable);
                }
            }
        }

        return new AccessorDecoratorResult(currentGetter, currentSetter, initFunctions);
    }

    /// <summary>
    /// Checks if any class element or the class itself has decorators.
    /// </summary>
    private bool HasDecorators()
    {
        if (_class.Decorators.Count > 0) return true;

        ref readonly var elements = ref _body.Body;
        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (element is MethodDefinition md && md.Decorators.Count > 0) return true;
            if (element is PropertyDefinition pd && pd.Decorators.Count > 0) return true;
            if (element is AccessorProperty ap && ap.Decorators.Count > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Runs extra initializers registered via addInitializer.
    /// </summary>
    private static void RunExtraInitializers(Engine engine, JsValue target, List<ICallable>? initializers)
    {
        if (initializers is null) return;
        for (var i = 0; i < initializers.Count; i++)
        {
            engine.Call((JsValue) initializers[i], target, Arguments.Empty);
        }
    }
}

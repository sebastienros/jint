using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
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

    public ClassDefinition(
        string? className,
        Expression? superClass,
        ClassBody body)
    {
        _className = className;
        _superClass = superClass;
        _body = body;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation
    /// </summary>
    public JsValue BuildConstructor(EvaluationContext context, Environment env)
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
                ExceptionHelper.ThrowTypeError(engine.Realm, "super class is not a constructor");
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
                    ExceptionHelper.ThrowTypeError(engine.Realm, "cannot resolve super class prototype chain");
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
            var constructorInfo = constructor.DefineMethod(proto, constructorParent);
            F = constructorInfo.Closure;

            F.SetFunctionName(_className ?? "");

            F.MakeConstructor(writableProperty: false, proto);
            F._constructorKind = _superClass is null ? ConstructorKind.Base : ConstructorKind.Derived;
            F.MakeClassConstructor();
            proto.CreateMethodProperty(CommonProperties.Constructor, F);

            var instancePrivateMethods = new List<PrivateElement>();
            var staticPrivateMethods = new List<PrivateElement>();
            var instanceFields = new List<ClassFieldDefinition>();
            var staticElements = new List<object>();

            foreach (IClassElement e in elements)
            {
                if (e is MethodDefinition { Kind: PropertyKind.Constructor })
                {
                    continue;
                }

                var isStatic = e.Static;

                var target = !isStatic ? proto : F;
                var element = ClassElementEvaluation(engine, target, e);
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
            }

            if (_className is not null)
            {
                classEnv.InitializeBinding(_className, F);
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
        }
        finally
        {
            engine.UpdateLexicalEnvironment(env);
            engine.UpdatePrivateEnvironment(outerPrivateEnvironment);
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
            // AccessorProperty ap => throw new NotImplementedException(), // not implemented yet
            _ => null
        };
    }

    /// <summary>
    /// /https://tc39.es/ecma262/#sec-runtime-semantics-classfielddefinitionevaluation
    /// </summary>
    private static ClassFieldDefinition ClassFieldDefinitionEvaluation(Engine engine, ObjectInstance homeObject, PropertyDefinition fieldDefinition)
    {
        var name = fieldDefinition.GetKey(engine);

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
            ExceptionHelper.ThrowSyntaxError(obj.Engine.Realm);
        }

        if (method.Kind != PropertyKind.Get && method.Kind != PropertyKind.Set && !function.Generator)
        {
            var methodDef = method.DefineMethod(obj);
            methodDef.Closure.SetFunctionName(methodDef.Key);
            return DefineMethodProperty(obj, methodDef.Key, methodDef.Closure, enumerable);
        }

        var getter = method.Kind == PropertyKind.Get;

        var definition = new JintFunctionDefinition(function);
        var intrinsics = engine.Realm.Intrinsics;

        var value = method.TryGetKey(engine);
        var propKey = TypeConverter.ToPropertyKey(value);
        var env = engine.ExecutionContext.LexicalEnvironment;
        var privateEnv = engine.ExecutionContext.PrivateEnvironment;

        if (function.Generator)
        {
            var closure = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.GeneratorFunction.PrototypeObject, definition, definition.ThisMode, env, privateEnv);
            closure.MakeMethod(obj);
            closure.SetFunctionName(propKey);
            var prototype = ObjectInstance.OrdinaryObjectCreate(engine, intrinsics.GeneratorFunction.PrototypeObject.PrototypeObject);
            closure.DefinePropertyOrThrow(CommonProperties.Prototype, new PropertyDescriptor(prototype, PropertyFlag.Writable));
            return DefineMethodProperty(obj, propKey, closure, enumerable);
        }
        else
        {
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
}

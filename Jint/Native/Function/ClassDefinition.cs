using System.Runtime.CompilerServices;
using Jint.Native.AsyncFunction;
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

    /// <summary>The only statement of <see cref="_superConstructor"/>: <c>super(...args);</c>.</summary>
    private static readonly Statement _defaultSuperCallStatement;

    private static readonly MethodDefinition _emptyConstructor;

    internal readonly string? _className;
    private readonly Expression? _superClass;
    private readonly ClassBody _body;

    /// <summary>
    /// Whether a decorator, the heritage or a computed key can suspend the generator or async frame the class is
    /// evaluated in, decided at build time so a class that cannot suspend never consults the frame's suspend data.
    /// </summary>
    internal readonly bool _maySuspend;

    /// <summary>
    /// The heritage's handler, built on the first evaluation and kept: what a suspension inside it parks is keyed by
    /// the handlers it is made of, which a tree rebuilt for the resume would not find.
    /// </summary>
    private JintExpression? _superClassExpression;

    /// <summary>
    /// Whether the class or any of its elements is decorated, and whether an element is, decided at build time so a
    /// class without decorators never looks for any.
    /// </summary>
    private readonly bool _hasDecorators;

    private readonly bool _hasElementDecorators;

    /// <summary>
    /// The handlers of the class's own decorator expressions, in source order, built on the first evaluation of a
    /// decorated class and kept for the reason <see cref="_superClassExpression"/> is: a decorator that suspended
    /// resumes through the same handlers, so what a sequence, a call or a <c>yield*</c> inside it parked is found and
    /// taken back.
    /// </summary>
    private JintExpression[]? _classDecoratorSites;

    /// <summary>
    /// The handlers of each element's decorator expressions, by element index, null for an element without any; built
    /// and kept as <see cref="_classDecoratorSites"/> are.
    /// </summary>
    private JintExpression[]?[]? _elementDecoratorSites;

    static ClassDefinition()
    {
        var parser = new Parser(Engine.BaseParserOptions);

        // generate missing constructor AST only once
        static MethodDefinition CreateConstructorMethodDefinition(Parser parser, string source)
        {
            var script = parser.ParseScript(source);
            var classDeclaration = (ClassDeclaration) script.Body[0];
            return (MethodDefinition) classDeclaration.Body.Body[0];
        }

        _superConstructor = CreateConstructorMethodDefinition(parser, "class temp extends X { constructor(...args) { super(...args); } }");
        _defaultSuperCallStatement = _superConstructor.Value.Body.Body[0];
        _defaultSuperCall = (CallExpression) ((NonSpecialExpressionStatement) _defaultSuperCallStatement).Expression;
        _emptyConstructor = CreateConstructorMethodDefinition(parser, "class temp { constructor() {} }");
    }

    /// <summary>
    /// Whether <paramref name="function"/> is one of the two constructors Jint synthesizes for a class
    /// that declares none.
    /// </summary>
    /// <remarks>
    /// Both are parsed once, at type initialization, from a source no host was ever handed, and every class
    /// borrowing one shares that single AST. The debugger is therefore transparent to them: it raises no
    /// return point for either, and <see cref="IsSynthesizedConstructorStatement"/> suppresses the one
    /// statement the derived form has, so stepping crosses an implicit constructor as one unit and lands in
    /// the base constructor's body.
    /// </remarks>
    internal static bool IsSynthesizedConstructor(IFunction function)
        => ReferenceEquals(function, _superConstructor.Value) || ReferenceEquals(function, _emptyConstructor.Value);

    /// <summary>
    /// Whether <paramref name="node"/> is the <c>super(...args);</c> statement of the constructor Jint
    /// synthesizes for a derived class that declares none.
    /// </summary>
    internal static bool IsSynthesizedConstructorStatement(Node node)
        => ReferenceEquals(node, _defaultSuperCallStatement);

    private readonly IClass _class;

    public ClassDefinition(
        string? className,
        IClass @class)
    {
        _className = className;
        _superClass = @class.SuperClass;
        _body = @class.Body;
        _class = @class;
        // An await or a yield of the enclosing frame can occur in the decorators, the heritage and the computed keys:
        // the parts of a class evaluated in that frame.
        _maySuspend = @class.AnyPartEvaluatedInEnclosingFrame(elementDecorators: true, MaySuspendVisitor.MaySuspend);

        ref readonly var elements = ref _body.Body;
        for (var i = 0; i < elements.Count && !_hasElementDecorators; i++)
        {
            _hasElementDecorators = DecoratorsOf(elements[i]).Count > 0;
        }

        _hasDecorators = _hasElementDecorators || @class.Decorators.Count > 0;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation
    /// </summary>
    public JsValue BuildConstructor(EvaluationContext context, Environment env, string? classBinding = null)
    {
        // A class definition is always strict mode code. The class body (superclass expression,
        // element decorators, computed property keys) is evaluated directly in the enclosing execution
        // context - no new context is pushed - so establish strict on the current context and
        // restore it on exit. Restoring the PREVIOUS value (not false) is required because the
        // enclosing frame may be strict or a generator, and computed keys can be yield expressions
        // that suspend and re-enter. The class's own decorators are the exception, evaluated as the
        // code around the class is (EvaluateClassDecorators).
        var previousStrict = context.Engine.ReplaceTopStrict(true);
        try
        {
            return _maySuspend
                ? BuildConstructorCore<CanSuspend>(context, env, classBinding, previousStrict)
                : BuildConstructorCore<CannotSuspend>(context, env, classBinding, previousStrict);
        }
        finally
        {
            context.Engine.ReplaceTopStrict(previousStrict);
        }
    }

    /// <remarks>
    /// Compiled once per <typeparamref name="TMode"/>. Every class whose decorators, heritage and computed keys hold no
    /// <c>await</c> or <c>yield</c> takes <see cref="CannotSuspend"/>, where <c>maySuspend</c> is a constant false:
    /// every branch of the resume (taking parked state back, parking it, stopping at a suspension) folds away, and
    /// that code is not emitted, and the per-element suspension test the class had before it could resume is gone.
    /// Keeping the resume code in the same body made the JIT stop inlining the constructor's creation, which cost
    /// <c>ClassBenchmark.ReEvaluateClassDeclarations</c> about 2%. With this split the <c>ScriptFunction</c>
    /// constructor and the method-property definitions are inlined again; <c>MakeConstructor</c> is not. So keep
    /// resume-only code behind <c>maySuspend</c> and exception regions in out-of-line helpers, and check a change here
    /// with <c>DOTNET_JitDisasm</c> and that benchmark row. Behind <c>maySuspend</c> is not enough for anything the
    /// JIT could inline: it inlines before it folds the mode, so a property read or a suspension test in a resume
    /// branch still spends inlining budget in the <see cref="CannotSuspend"/> code: a parked-state read and an
    /// <c>IsSuspended()</c> test there cost two <c>Engine.Realm</c> reads their inlining (sebastienros/jint#4399). Put
    /// them in the helper the branch calls. A decorated class is the same case: behind <c>decorators is not null</c>
    /// there are only calls, to <see cref="DecoratorEvaluation.AddElement"/>, <see cref="DecoratorEvaluation.AddAutoAccessor"/>
    /// and <see cref="FinishDecoratedClass"/>, which do the rest (sebastienros/jint#4413). And no lambda here: one
    /// capturing a local of this method makes C# allocate its closure on entry, for every class.
    /// </remarks>
    private JsValue BuildConstructorCore<TMode>(EvaluationContext context, Environment env, string? classBinding, bool previousStrict)
        where TMode : struct, ISuspendMode
    {
        var engine = context.Engine;
        var maySuspend = default(TMode).MaySuspend;

        // A frame resuming into a class that suspended in a decorator, its heritage or a computed key continues it where
        // it stopped, in the class scope it built the first time (sebastienros/jint#4395, sebastienros/jint#4399).
        var resumed = maySuspend ? TakeSuspendData(engine) : null;

        DeclarativeEnvironment classEnv;
        PrivateEnvironment classPrivateEnvironment;
        var outerPrivateEnvironment = engine.ExecutionContext.PrivateEnvironment;
        DecoratorEvaluation? decorators = null;

        if (!maySuspend || resumed is null)
        {
            classEnv = JintEnvironment.NewDeclarativeEnvironment(engine, env);

            if (_className is not null)
            {
                classEnv.CreateImmutableBinding(_className, true);
            }

            classPrivateEnvironment = JintEnvironment.NewPrivateEnvironment(engine, outerPrivateEnvironment);

            // The class's own decorators are evaluated in the scope around the class, before the heritage; an
            // element's are evaluated with that element, below.
            if (_hasDecorators)
            {
                decorators = StartDecorators(context, previousStrict);

                // A decorator that suspended: nothing of the class exists yet. The decorators evaluated so far are
                // parked, and the resume continues at the one that suspended (sebastienros/jint#4399).
                if (maySuspend && context.IsSuspended())
                {
                    Park(engine, env, outerPrivateEnvironment, previousStrict, classEnv, classPrivateEnvironment, decorators);
                    return JsValue.Undefined;
                }
            }
        }
        else
        {
            classEnv = resumed.ClassEnvironment;
            classPrivateEnvironment = resumed.ClassPrivateEnvironment;
            decorators = resumed.Decorators;

            if (ContinueDecorators(context, resumed, env, outerPrivateEnvironment, previousStrict))
            {
                return JsValue.Undefined;
            }
        }

        ObjectInstance proto;
        ObjectInstance? constructorParent = null;
        MethodDefinition? constructor = null;
        ref readonly var elements = ref _body.Body;

        // A computed key or an element decorator suspended: the heritage was evaluated and the prototype made the
        // first time.
        var resumedIntoElements = maySuspend && resumed is { Prototype: not null };
        if (resumedIntoElements)
        {
            proto = resumed!.Prototype!;
        }
        else
        {
            ObjectInstance? protoParent = null;
            if (_superClass is null)
            {
                protoParent = engine.Realm.Intrinsics.Object.PrototypeObject;
                constructorParent = engine.Realm.Intrinsics.Function.PrototypeObject;
            }
            else
            {
                var superclass = EvaluateHeritage(context, env, classEnv);

                // A suspended heritage hands back a placeholder, not its value: the class is defined on the resume.
                if (maySuspend && context.IsSuspended())
                {
                    Park(engine, env, outerPrivateEnvironment, previousStrict, classEnv, classPrivateEnvironment, decorators);
                    return JsValue.Undefined;
                }

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

            proto = new JsObject(engine) { _prototype = protoParent };

            var privateBoundIdentifiers = new HashSet<PrivateIdentifier>(PrivateIdentifierNameComparer._instance);
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
        }

        engine.UpdateLexicalEnvironment(classEnv);
        engine.UpdatePrivateEnvironment(classPrivateEnvironment);

        ScriptFunction F;
        try
        {
            List<PrivateElement> instancePrivateMethods;
            List<PrivateElement> staticPrivateMethods;
            List<ClassFieldDefinition> instanceFields;
            List<object> staticElements;
            var elementIndex = 0;

            if (resumedIntoElements)
            {
                F = resumed!.Constructor!;
                instancePrivateMethods = resumed.InstancePrivateMethods!;
                staticPrivateMethods = resumed.StaticPrivateMethods!;
                instanceFields = resumed.InstanceFields!;
                staticElements = resumed.StaticElements!;
                elementIndex = resumed.ElementIndex;
            }
            else
            {
                // The definition is keyed on the class BODY, not on the constructor's function node and not on
                // the class node. Not the function node, because a class with no explicit constructor borrows a
                // synthesized one that every such class shares. Not the class node, because a class expression is
                // also a field initializer's own value node, which the field-initializer cache already keys on
                // (`B = class {}` would collide). A ClassBody is unique per class and is never a cache key elsewhere.
                var constructorInfo = constructor!.DefineMethod(proto, constructorParent, sourceTextNode: _class, definitionCacheKey: _body);
                F = constructorInfo.Closure;

                F.SetFunctionName(_className ?? classBinding ?? "");

                F.MakeConstructor(writableProperty: false, proto);
                F._constructorKind = _superClass is null ? ConstructorKind.Base : ConstructorKind.Derived;
                F.MakeClassConstructor();
                proto.CreateMethodProperty(CommonProperties.Constructor, F);

                instancePrivateMethods = new List<PrivateElement>();
                staticPrivateMethods = new List<PrivateElement>();
                instanceFields = new List<ClassFieldDefinition>();
                staticElements = new List<object>();
            }

            for (; elementIndex < elements.Count; elementIndex++)
            {
                var e = (IClassElement) elements[elementIndex];
                if (e is MethodDefinition { Kind: PropertyKind.Constructor })
                {
                    continue;
                }

                var isStatic = e.Static;
                var target = !isStatic ? proto : F;

                // ClassElementEvaluation evaluates an element's decorators just before its key, in the class scope. A
                // decorator that suspended: parked once the loop is left, at this element's index.
                if (decorators is not null && !EvaluateElementDecorators(context, decorators, elementIndex))
                {
                    break;
                }

                // Handle auto-accessors specially since they produce both a field definition and optionally a private element
                if (e is AccessorProperty ap)
                {
                    var result = AutoAccessorDefinitionEvaluation(engine, target, ap, isStatic);

                    // A computed key that suspended: parked once the loop is left, at this element's index.
                    if (maySuspend && context.IsSuspended())
                    {
                        break;
                    }

                    // A decorated class keeps the element for FinishDecoratedClass, which decorates it once every
                    // element has been evaluated.
                    if (decorators is not null)
                    {
                        decorators.AddAutoAccessor(target, elementIndex, isStatic, result);
                        continue;
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

                    continue;
                }

                var element = ClassElementEvaluation(engine, target, e, out var definedKey);

                // A computed key that suspended: parked once the loop is left, at this element's index.
                if (maySuspend && context.IsSuspended())
                {
                    break;
                }

                if (decorators is not null)
                {
                    decorators.AddElement(target, elementIndex, e, element, definedKey);
                    continue;
                }

                if (element is PrivateElement privateElement)
                {
                    var container = !isStatic ? instancePrivateMethods : staticPrivateMethods;
                    var index = IndexOfPrivateElement(container, privateElement.Key);
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

            // An element decorator or a computed key suspended the frame: the elements before it are defined, it and the
            // rest are not.
            if (maySuspend && context.IsSuspended())
            {
                ParkElements(Park(engine, env, outerPrivateEnvironment, previousStrict, classEnv, classPrivateEnvironment, decorators),
                    proto, F, elementIndex, instancePrivateMethods, staticPrivateMethods, instanceFields, staticElements);
                return JsValue.Undefined;
            }

            // A decorated class is finished out of line: its decorators are applied, the class binding initialized with
            // what they return, and its static half run against that.
            if (decorators is not null)
            {
                return FinishDecoratedClass(engine, decorators, F, proto, classEnv, classBinding);
            }

            if (_className is not null)
            {
                classEnv.InitializeBinding(_className, F, DisposeHint.Normal);
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
    /// Evaluates the heritage with the class scope as the running LexicalEnvironment, steps 8.a-8.d of
    /// https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation: the outer environment is back before
    /// the result is used, or a throw leaves the evaluation.
    /// </summary>
    /// <remarks>
    /// Out of line so the exception region and its temporaries stay out of <see cref="BuildConstructorCore{TMode}"/>, whose
    /// inlining they would otherwise crowd.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue EvaluateHeritage(EvaluationContext context, Environment env, DeclarativeEnvironment classEnv)
    {
        var engine = context.Engine;
        engine.UpdateLexicalEnvironment(classEnv);
        try
        {
            return (_superClassExpression ??= JintExpression.Build(_superClass!)).GetValue(context);
        }
        finally
        {
            engine.UpdateLexicalEnvironment(env);
        }
    }

    /// <summary>
    /// Starts one evaluation's decorators: evaluates the class's own, in source order, in the scope around the class,
    /// as the class declaration or expression does before ClassDefinitionEvaluation
    /// (https://github.com/tc39/ecma262/pull/2417). Its elements' are evaluated with them, by
    /// <see cref="EvaluateElementDecorators"/>.
    /// </summary>
    /// <remarks>
    /// Out of line for the same reason as <see cref="EvaluateHeritage"/>: decorated classes are rare, and this code would
    /// otherwise crowd the inlining of <see cref="BuildConstructorCore{TMode}"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private DecoratorEvaluation StartDecorators(EvaluationContext context, bool previousStrict)
    {
        var sites = _classDecoratorSites ??= BuildDecoratorSites(_class.Decorators);
        var decorators = new DecoratorEvaluation(sites.Length > 0 ? new DecoratorDefinition[sites.Length] : [], _hasElementDecorators);
        EvaluateClassDecorators(context, decorators, start: 0, previousStrict);
        return decorators;
    }

    /// <summary>
    /// Evaluates the class's own decorators from the one at <paramref name="start"/>, each into its slot of
    /// <see cref="DecoratorEvaluation.ClassDecorators"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The class's decorator list precedes <c>class</c>, outside the ClassTail, and is evaluated as the code around the
    /// class is: sloppy in sloppy code. The spec text calls every part of a class strict mode code
    /// (https://tc39.es/ecma262/#sec-strict-mode-code), but test262 parses <c>@yield() class C {}</c> as sloppy code
    /// (<c>decorator-call-expr-identifier-reference-yield.js</c>, flagged noStrict), and so does Jint's parser; what
    /// the parser accepted as sloppy code runs as sloppy code. Everything after the list, the elements' decorators
    /// included, is strict.
    /// </para>
    /// <para>
    /// Once one suspends the frame, none after it is evaluated and its value is not stored: its index is kept in
    /// <see cref="DecoratorEvaluation.PendingClassDecorator"/>, which <see cref="Park"/> parks, and the resume calls this
    /// again from that index. The decorators before it are not evaluated again, and the one that suspended resumes
    /// through the handlers it suspended in, so the part of it before its <c>await</c> or <c>yield</c> is not evaluated
    /// again either. A suspension or a throw leaves the strictness to <see cref="BuildConstructor"/>, which restores the
    /// frame's own on the way out.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EvaluateClassDecorators(EvaluationContext context, DecoratorEvaluation decorators, int start, bool previousStrict)
    {
        var sites = _classDecoratorSites!;
        if (start >= sites.Length)
        {
            return;
        }

        var engine = context.Engine;
        engine.ReplaceTopStrict(previousStrict);

        var values = decorators.ClassDecorators;
        for (var i = start; i < sites.Length; i++)
        {
            var decorator = EvaluateDecorator(context, sites[i]);

            if (_maySuspend && context.IsSuspended())
            {
                decorators.PendingClassDecorator = i;
                return;
            }

            values[i] = decorator;
        }

        decorators.PendingClassDecorator = -1;
        engine.ReplaceTopStrict(true);
    }

    /// <summary>
    /// Evaluates the decorators of the element at <paramref name="elementIndex"/>, ClassElementEvaluation's
    /// DecoratorListEvaluation (https://github.com/tc39/ecma262/pull/2417): after the heritage and the elements before
    /// it, with the class scope and the class's PrivateEnvironment as the running ones, just before the element's key.
    /// Answers false when one suspended the frame; otherwise <see cref="DecoratorEvaluation.DecoratorsAt"/> answers
    /// them for the element.
    /// </summary>
    /// <remarks>
    /// <see cref="DecoratorEvaluation"/> keeps the element's values and the index of the next one to evaluate, so a
    /// resume into this element, parked at its index like a suspended key, continues at the decorator that suspended;
    /// and once they are all evaluated, a resume into the element's key answers them without evaluating any again.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool EvaluateElementDecorators(EvaluationContext context, DecoratorEvaluation decorators, int elementIndex)
    {
        if (!decorators.HasElementDecorators)
        {
            return true;
        }

        var sites = (_elementDecoratorSites ??= BuildElementDecoratorSites())[elementIndex];
        if (sites is null)
        {
            return true;
        }

        if (decorators.Element != elementIndex)
        {
            decorators.Element = elementIndex;
            decorators.NextElementDecorator = 0;
            decorators.ElementDecorators = new DecoratorDefinition[sites.Length];
        }

        var values = decorators.ElementDecorators;
        for (var i = decorators.NextElementDecorator; i < sites.Length; i++)
        {
            var decorator = EvaluateDecorator(context, sites[i]);

            if (_maySuspend && context.IsSuspended())
            {
                decorators.NextElementDecorator = i;
                return false;
            }

            values[i] = decorator;
        }

        decorators.NextElementDecorator = sites.Length;
        return true;
    }

    /// <summary>
    /// DecoratorEvaluation (https://github.com/tc39/ecma262/pull/2417): the decorator's value and its [[Receiver]], the
    /// reference it was read through. The decorator is called with that reference's this value, as EvaluateCall
    /// (https://tc39.es/ecma262/#sec-evaluatecall) would call it: the base of a property reference (<c>obj</c> for
    /// <c>@obj.dec</c> and for <c>@(obj.dec)</c>), the object of a <c>with</c> that resolved an identifier, and
    /// <c>undefined</c> for anything else, a call's result (<c>@make()</c>) included.
    /// </summary>
    private static DecoratorDefinition EvaluateDecorator(EvaluationContext context, JintExpression expression)
    {
        var engine = context.Engine;
        var result = expression.Evaluate(context);

        // A suspended expression hands back a placeholder, which the caller discards.
        if (context.IsSuspended())
        {
            engine._referencePool.Return(result as Reference);
            return default;
        }

        if (result is not Reference reference)
        {
            return new DecoratorDefinition((JsValue) result, JsValue.Undefined);
        }

        JsValue receiver;
        if (reference.IsPropertyReference)
        {
            receiver = reference.ThisValue;
        }
        else if (reference.Base is Environment baseEnvironment)
        {
            receiver = baseEnvironment.WithBaseObject();
        }
        else
        {
            receiver = JsValue.Undefined;
        }

        return new DecoratorDefinition(engine.GetValue(reference, returnReferenceToPool: true), receiver);
    }

    private static JintExpression[] BuildDecoratorSites(in NodeList<Decorator> decorators)
    {
        if (decorators.Count == 0)
        {
            return [];
        }

        var sites = new JintExpression[decorators.Count];
        for (var i = 0; i < sites.Length; i++)
        {
            sites[i] = JintExpression.Build(decorators[i].Expression);
        }

        return sites;
    }

    private JintExpression[]?[] BuildElementDecoratorSites()
    {
        ref readonly var elements = ref _body.Body;
        var sites = new JintExpression[]?[elements.Count];
        for (var i = 0; i < sites.Length; i++)
        {
            var decoratorList = DecoratorsOf(elements[i]);
            if (decoratorList.Count > 0)
            {
                sites[i] = BuildDecoratorSites(in decoratorList);
            }
        }

        return sites;
    }

    private static NodeList<Decorator> DecoratorsOf(Node element) => element switch
    {
        MethodDefinition method => method.Decorators,
        PropertyDefinition field => field.Decorators,
        AccessorProperty accessor => accessor.Decorators,
        _ => default,
    };

    /// <summary>
    /// A decorator's value and the receiver it is called with: the spec text's DecoratorDefinition Record, whose
    /// [[Receiver]] is the reference the value was read through (https://github.com/tc39/ecma262/pull/2417).
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct DecoratorDefinition(JsValue Decorator, JsValue Receiver);

    /// <summary>
    /// One evaluation's decorators: the class's own, evaluated before the heritage, those of the element being
    /// evaluated, evaluated just before its key, and every element evaluated so far, which
    /// <see cref="FinishDecoratedClass"/> decorates once the last one is. Parked with the rest of the class when a
    /// decorator, the heritage or a computed key suspends the frame.
    /// </summary>
    private sealed class DecoratorEvaluation
    {
        public DecoratorEvaluation(DecoratorDefinition[] classDecorators, bool hasElementDecorators)
        {
            ClassDecorators = classDecorators;
            HasElementDecorators = hasElementDecorators;
        }

        /// <summary>
        /// The class's own decorators, in source order. While <see cref="PendingClassDecorator"/> is set, only the
        /// slots before it are filled.
        /// </summary>
        public DecoratorDefinition[] ClassDecorators { get; }

        /// <summary>
        /// The index of the class decorator that suspended; -1 once every one has a value.
        /// </summary>
        public int PendingClassDecorator { get; set; } = -1;

        /// <summary>
        /// Whether any element is decorated: a class decorated only on itself skips the per-element lookup.
        /// </summary>
        public bool HasElementDecorators { get; }

        /// <summary>
        /// The index of the element whose decorators <see cref="ElementDecorators"/> holds; -1 before the first.
        /// </summary>
        public int Element { get; set; } = -1;

        /// <summary>
        /// The index of that element's next decorator to evaluate: the one that suspended, or the element's decorator
        /// count once all of them have values.
        /// </summary>
        public int NextElementDecorator { get; set; }

        public DecoratorDefinition[] ElementDecorators { get; set; } = [];

        /// <summary>
        /// The elements evaluated so far, in source order, the constructor aside.
        /// </summary>
        public List<DecoratedElement> Elements { get; } = [];

        /// <summary>
        /// The decorators <see cref="EvaluateElementDecorators"/> evaluated for the element at
        /// <paramref name="elementIndex"/>; empty for an element without any, which never becomes
        /// <see cref="Element"/>.
        /// </summary>
        public DecoratorDefinition[] DecoratorsAt(int elementIndex) => Element == elementIndex ? ElementDecorators : [];

        /// <summary>
        /// Keeps a method, getter, setter, field or static block of a decorated class as ClassElementEvaluation
        /// answered it (https://github.com/tc39/ecma262/pull/2417). A public method, getter or setter is already
        /// defined on <paramref name="target"/>, so its function is read back from there.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddElement(ObjectInstance target, int elementIndex, IClassElement e, object? element, JsValue definedKey)
        {
            var record = new DecoratedElement { IsStatic = e.Static, Decorators = DecoratorsAt(elementIndex) };
            switch (element)
            {
                case ClassFieldDefinition field:
                    record.Kind = DecoratedElementKind.Field;
                    record.Key = field.Name;
                    record.Field = field;
                    break;
                case ClassStaticBlockDefinition staticBlock:
                    record.Kind = DecoratedElementKind.StaticBlock;
                    record.IsStatic = true;
                    record.StaticBlock = staticBlock;
                    break;
                default:
                    var method = (MethodDefinition) e;
                    var privateElement = element as PrivateElement;
                    record.Key = privateElement?.Key ?? definedKey;
                    var descriptor = privateElement is null ? target.GetOwnProperty(definedKey) : null;
                    if (method.Kind == PropertyKind.Get)
                    {
                        record.Kind = DecoratedElementKind.Getter;
                        record.Get = privateElement is not null ? privateElement.Get : descriptor!.Get;
                    }
                    else if (method.Kind == PropertyKind.Set)
                    {
                        record.Kind = DecoratedElementKind.Setter;
                        record.Set = privateElement is not null ? privateElement.Set : descriptor!.Set;
                    }
                    else
                    {
                        record.Kind = DecoratedElementKind.Method;
                        record.Value = privateElement is not null ? privateElement.Value : descriptor!.Value;
                    }

                    break;
            }

            Elements.Add(record);
        }

        /// <summary>
        /// Keeps an auto-accessor of a decorated class: its key, its getter and setter, defined on
        /// <paramref name="target"/> already when the key is public, and its backing field.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddAutoAccessor(ObjectInstance target, int elementIndex, bool isStatic, AutoAccessorResult result)
        {
            var record = new DecoratedElement
            {
                Kind = DecoratedElementKind.Accessor,
                IsStatic = isStatic,
                Decorators = DecoratorsAt(elementIndex),
                Field = result.FieldDefinition,
            };

            if (result.PrivateElement is { } privateElement)
            {
                record.Key = privateElement.Key;
                record.Get = privateElement.Get;
                record.Set = privateElement.Set;
            }
            else
            {
                record.Key = result.PropertyName;
                var descriptor = target.GetOwnProperty(result.PropertyName);
                record.Get = descriptor.Get;
                record.Set = descriptor.Set;
            }

            Elements.Add(record);
        }
    }

    private enum DecoratedElementKind : byte
    {
        Field,
        Method,
        Getter,
        Setter,
        Accessor,
        StaticBlock,
    }

    /// <summary>
    /// An element of a decorated class between its evaluation and the application of its decorators: the decorators
    /// spec text's ClassElementDefinition Record, or a static block's ClassStaticBlockDefinition Record
    /// (https://github.com/tc39/ecma262/pull/2417).
    /// </summary>
    private sealed class DecoratedElement
    {
        public DecoratedElementKind Kind { get; set; }

        public bool IsStatic { get; set; }

        /// <summary>
        /// [[Key]]: a property key, or a <see cref="PrivateName"/>. Undefined for a static block.
        /// </summary>
        public JsValue Key { get; set; } = JsValue.Undefined;

        /// <summary>
        /// [[Decorators]], in source order; they are applied last to first.
        /// </summary>
        public DecoratorDefinition[] Decorators { get; set; } = [];

        /// <summary>[[Value]], of a method.</summary>
        public JsValue? Value { get; set; }

        /// <summary>[[Get]], of a getter or an accessor.</summary>
        public JsValue? Get { get; set; }

        /// <summary>[[Set]], of a setter or an accessor.</summary>
        public JsValue? Set { get; set; }

        /// <summary>
        /// A field's definition, or an accessor's backing field (its [[BackingStorageKey]]), with the initializer the
        /// element itself declares.
        /// </summary>
        public ClassFieldDefinition? Field { get; set; }

        /// <summary>
        /// The initializers the field's or accessor's decorators returned, in the order they run, after the
        /// element's own: [[Initializers]] without it.
        /// </summary>
        public List<ICallable>? Initializers { get; set; }

        /// <summary>
        /// [[ExtraInitializers]] of a field or an accessor: what its decorators passed to <c>addInitializer</c>.
        /// </summary>
        public List<ICallable>? ExtraInitializers { get; set; }

        public ClassStaticBlockDefinition? StaticBlock { get; set; }
    }

    /// <summary>
    /// Continues the class's own decorators at the one that suspended, when that is where the class stopped, and
    /// parks it again if another one suspends. Answers whether the frame is suspended again.
    /// </summary>
    /// <remarks>
    /// The JIT inlines before it folds the suspension mode, so the <see cref="CannotSuspend"/> instantiation still
    /// imports the resume branch that calls this: a single call that cannot be inlined adds nothing there for the
    /// inliner to weigh, where the reads and the suspension test this makes would take inlining budget from the
    /// constructor's creation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool ContinueDecorators(EvaluationContext context, ClassSuspendData resumed, Environment env, PrivateEnvironment? outerPrivateEnvironment, bool previousStrict)
    {
        var decorators = resumed.Decorators;
        if (decorators is not { PendingClassDecorator: >= 0 })
        {
            return false;
        }

        // Through the handlers the decorator suspended in, so the part of it before its await or yield is not evaluated
        // again either.
        EvaluateClassDecorators(context, decorators, decorators.PendingClassDecorator, previousStrict);
        if (!context.IsSuspended())
        {
            return false;
        }

        Park(context.Engine, env, outerPrivateEnvironment, previousStrict, resumed.ClassEnvironment, resumed.ClassPrivateEnvironment, decorators);
        return true;
    }

    /// <summary>
    /// Takes back what this class parked when its frame suspended inside it, if the frame is resuming into it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ClassSuspendData? TakeSuspendData(Engine engine)
    {
        var suspendable = engine.ExecutionContext.Suspendable;
        if (suspendable is { IsResuming: true } && suspendable.Data.TryGet(_body, out ClassSuspendData? data))
        {
            suspendable.Data.Clear(_body);
            return data;
        }

        return null;
    }

    /// <summary>
    /// Parks what the class has built when a decorator, its heritage or a computed key suspended the frame, keyed by the
    /// class body: one frame cannot be inside the same class twice at once.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ClassSuspendData Park(
        Engine engine,
        Environment env,
        PrivateEnvironment? outerPrivateEnvironment,
        bool previousStrict,
        DeclarativeEnvironment classEnv,
        PrivateEnvironment classPrivateEnvironment,
        DecoratorEvaluation? decorators)
    {
        var suspendable = engine.ExecutionContext.Suspendable!;
        var data = suspendable.Data.GetOrCreate<ClassSuspendData>(_body);
        data.ClassEnvironment = classEnv;
        data.ClassPrivateEnvironment = classPrivateEnvironment;
        data.Decorators = decorators;
        data.Prototype = null;

        RestoreEntryContext(engine, env, outerPrivateEnvironment, previousStrict);
        return data;
    }

    /// <summary>
    /// Puts the context a suspended async function will resume in back to the one the class was entered with.
    /// </summary>
    /// <remarks>
    /// An async function captures its context afresh at every await, which here is from inside the class: the class
    /// scope, the class's private environment, strict mode code. Its resume replays the statement holding the class,
    /// and whatever that statement evaluates before reaching the class again (the reference a
    /// <c>let C = class C …</c> initializes, the environment the class's own scope is created in) has to be evaluated
    /// where it was the first time. A generator re-enters the context it started with on every resume, so it needs no
    /// such repair.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RestoreEntryContext(Engine engine, Environment env, PrivateEnvironment? outerPrivateEnvironment, bool previousStrict)
    {
        if (engine.ExecutionContext.Suspendable is AsyncFunctionInstance asyncFunction)
        {
            asyncFunction._savedContext = asyncFunction._savedContext
                .UpdateLexicalEnvironment(env)
                .UpdatePrivateEnvironment(outerPrivateEnvironment)
                .UpdateStrict(previousStrict);
        }
    }

    /// <summary>
    /// Adds to what <see cref="Park"/> parked what a suspended element decorator or computed key leaves: the prototype,
    /// the constructor, the elements gathered so far and the index of the element whose decorator or key suspended.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ParkElements(
        ClassSuspendData data,
        ObjectInstance proto,
        ScriptFunction constructor,
        int elementIndex,
        List<PrivateElement> instancePrivateMethods,
        List<PrivateElement> staticPrivateMethods,
        List<ClassFieldDefinition> instanceFields,
        List<object> staticElements)
    {
        data.Prototype = proto;
        data.Constructor = constructor;
        data.ElementIndex = elementIndex;
        data.InstancePrivateMethods = instancePrivateMethods;
        data.StaticPrivateMethods = staticPrivateMethods;
        data.InstanceFields = instanceFields;
        data.StaticElements = staticElements;
    }

    /// <summary>
    /// Whether a <see cref="BuildConstructorCore{TMode}"/> instantiation takes part in a resume, as a constant: a struct
    /// type argument gets code of its own, into which the property's value is inlined and folded.
    /// </summary>
    private interface ISuspendMode
    {
        bool MaySuspend { get; }
    }

    /// <summary>
    /// The instantiation for a class whose decorators, heritage or computed keys can suspend its frame.
    /// </summary>
    private readonly struct CanSuspend : ISuspendMode
    {
        public bool MaySuspend => true;
    }

    /// <summary>
    /// The instantiation for every other class.
    /// </summary>
    private readonly struct CannotSuspend : ISuspendMode
    {
        public bool MaySuspend => false;
    }

    /// <summary>
    /// What a class definition had built when its frame suspended in a decorator, its heritage or a computed key
    /// (https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation), so the resume continues there
    /// instead of evaluating the decorators, the heritage and the keys before the suspended one again, in a new class
    /// scope.
    /// </summary>
    private sealed class ClassSuspendData : SuspendData
    {
        public DeclarativeEnvironment ClassEnvironment { get; set; } = null!;

        public PrivateEnvironment ClassPrivateEnvironment { get; set; } = null!;

        /// <summary>
        /// The decorators evaluated so far, the class's own and the current element's, where their evaluation
        /// stopped, and the elements a decorated class has evaluated; null for a class without any decorator.
        /// </summary>
        public DecoratorEvaluation? Decorators { get; set; }

        /// <summary>
        /// Null when the heritage suspended; the class's prototype once an element decorator or a computed key did, with
        /// the fields below.
        /// </summary>
        public ObjectInstance? Prototype { get; set; }

        public ScriptFunction? Constructor { get; set; }

        /// <summary>
        /// The index of the element whose decorator or computed key suspended.
        /// </summary>
        public int ElementIndex { get; set; }

        public List<PrivateElement>? InstancePrivateMethods { get; set; }

        public List<PrivateElement>? StaticPrivateMethods { get; set; }

        public List<ClassFieldDefinition>? InstanceFields { get; set; }

        public List<object>? StaticElements { get; set; }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-static-semantics-classelementevaluation
    /// <paramref name="definedKey"/> returns the evaluated property key for method definitions
    /// (undefined otherwise) so decorator bookkeeping can reuse it instead of re-evaluating.
    /// </summary>
    private static object? ClassElementEvaluation(Engine engine, ObjectInstance target, IClassElement e, out JsValue definedKey)
    {
        switch (e)
        {
            case PropertyDefinition p:
                definedKey = JsValue.Undefined;
                return ClassFieldDefinitionEvaluation(engine, target, p);
            case MethodDefinition m:
                return MethodDefinitionEvaluation(engine, target, m, enumerable: false, out definedKey);
            case StaticBlock s:
                definedKey = JsValue.Undefined;
                return ClassStaticBlockDefinitionEvaluation(engine, target, s);
            default:
                // AccessorProperty is handled directly in BuildConstructor
                definedKey = JsValue.Undefined;
                return null;
        }
    }

    /// <summary>
    /// Result of auto-accessor definition evaluation.
    /// Contains the field initializer and an optional private element (for private auto-accessors).
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct AutoAccessorResult(
        ClassFieldDefinition FieldDefinition,
        PrivateElement? PrivateElement,
        JsValue PropertyName);

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

        // A computed key that suspended produced no key: defining the accessor would leave an "undefined" property on
        // a target the resume reuses. IsSuspended rather than Suspended, which sees a generator parked at a yield only.
        if (accessorProperty.Computed && engine.ExecutionContext.IsSuspended)
        {
            return new AutoAccessorResult(
                new ClassFieldDefinition { Name = JsValue.Undefined, Initializer = null },
                null,
                JsValue.Undefined);
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

                if (!engine.TryGetFunctionDefinition(accessorProperty.Value, out var definition))
                {
                    engine.CacheFunctionDefinition(accessorProperty.Value, definition = new JintFunctionDefinition(new ClassFieldFunction(accessorProperty.Value)));
                }

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

            return new AutoAccessorResult(fieldDef, pe, name);
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

                if (!engine.TryGetFunctionDefinition(accessorProperty.Value, out var definition))
                {
                    engine.CacheFunctionDefinition(accessorProperty.Value, definition = new JintFunctionDefinition(new ClassFieldFunction(accessorProperty.Value)));
                }

                initializer = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.Function.PrototypeObject, definition, FunctionThisMode.Global, env, privEnv);
                initializer.MakeMethod(target);
                initializer._classFieldInitializerName = name;
            }

            var fieldDef = new ClassFieldDefinition { Name = backingName, Initializer = initializer };
            return new AutoAccessorResult(fieldDef, null, name);
        }
    }

    /// <summary>
    /// /https://tc39.es/ecma262/#sec-runtime-semantics-classfielddefinitionevaluation
    /// </summary>
    private static ClassFieldDefinition ClassFieldDefinitionEvaluation(Engine engine, ObjectInstance homeObject, PropertyDefinition fieldDefinition)
    {
        var name = fieldDefinition.GetKey(engine);

        // A computed key that suspended, on an await as much as on a yield, produced no key.
        if (fieldDefinition.Computed && engine.ExecutionContext.IsSuspended)
        {
            return new ClassFieldDefinition { Name = JsValue.Undefined, Initializer = null };
        }

        ScriptFunction? initializer = null;
        if (fieldDefinition.Value is not null)
        {
            var intrinsics = engine.Realm.Intrinsics;
            var env = engine.ExecutionContext.LexicalEnvironment;
            var privateEnv = engine.ExecutionContext.PrivateEnvironment;

            if (!engine.TryGetFunctionDefinition(fieldDefinition.Value, out var definition))
            {
                engine.CacheFunctionDefinition(fieldDefinition.Value, definition = new JintFunctionDefinition(new ClassFieldFunction(fieldDefinition.Value)));
            }

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
            // The synthesized nodes carry the initializer expression's own position. Without it they keep
            // the default (line 0, no source file), which is what the debugger reports when it steps onto
            // a field initializer or its return point - a location no editor can open.
            var returnStatement = new ReturnStatement(expression)
            {
                Location = expression.Location,
                Range = expression.Range,
            };
            var nodeList = NodeList.From<Statement>(returnStatement);
            _statement = new FunctionBody(in nodeList, strict: true)
            {
                Location = expression.Location,
                Range = expression.Range,
            };
            Location = expression.Location;
            Range = expression.Range;
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

        if (!engine.TryGetFunctionDefinition(o, out var definition))
        {
            engine.CacheFunctionDefinition(o, definition = new JintFunctionDefinition(new ClassStaticBlockFunction(o)));
        }

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
            // See ClassFieldFunction: the synthesized body takes the static block's own position, so the
            // return point the debugger pauses at is the block's closing brace rather than line 0.
            _statement = new FunctionBody(staticBlock.Body, strict: true)
            {
                Location = staticBlock.Location,
                Range = staticBlock.Range,
            };
            _params = new NodeList<Node>();
            Location = staticBlock.Location;
            Range = staticBlock.Range;
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
        => MethodDefinitionEvaluation(engine, obj, method, enumerable, out _);

    /// <summary>
    /// <paramref name="definedKey"/> returns the property key the method was defined under, so callers
    /// (e.g. decorator bookkeeping) can reuse it — a computed key expression must be evaluated exactly once.
    /// </summary>
    internal static PrivateElement? MethodDefinitionEvaluation<T>(
        Engine engine,
        ObjectInstance obj,
        T method,
        bool enumerable,
        out JsValue definedKey) where T : IProperty
    {
        definedKey = JsValue.Undefined;

        var function = method.Value as IFunction;
        if (function is null)
        {
            Throw.SyntaxError(obj.Engine.Realm);
        }

        if (method.Kind != PropertyKind.Get && method.Kind != PropertyKind.Set && !function.Generator)
        {
            var methodDef = method.DefineMethod(obj, functionPrototype: null, sourceTextNode: method);

            // A computed key that suspended (`{ [f() + await g()]() {} }`) produced no key at all —
            // DefineMethod turned the placeholder undefined into the property key "undefined", and an
            // object literal reuses the same target object across the resume, so defining it would leave
            // a property the replay can never take back. IsSuspended rather than Suspended: the latter
            // only sees a generator parked at a yield, and this shape suspends on an await too.
            if (engine.ExecutionContext.IsSuspended)
            {
                return null;
            }

            definedKey = methodDef.Key;
            methodDef.Closure.SetFunctionName(methodDef.Key);
            return DefineMethodProperty(obj, methodDef.Key, methodDef.Closure, enumerable);
        }

        if (!engine.TryGetFunctionDefinition((Node) function, out var definition))
        {
            engine.CacheFunctionDefinition((Node) function, definition = new JintFunctionDefinition(function, sourceTextNode: method));
        }

        var intrinsics = engine.Realm.Intrinsics;

        // Read once: on a method shared across reference types, every IProperty read is an interface call.
        var computed = method.Computed;
        var value = method.Key.TryGetKey(engine, computed);

        // A computed key that suspended produced no key, as in the branch above: defining the accessor or the
        // generator would leave an "undefined" property on a target the resume reuses, an object literal's or a
        // class's. Only a computed key can suspend, which keeps the check off every other definition.
        if (computed && engine.ExecutionContext.IsSuspended)
        {
            return null;
        }

        var propKey = TypeConverter.ToPropertyKey(value);
        definedKey = propKey;
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
    /// The index of the private element in <paramref name="container"/> whose name has the description of
    /// <paramref name="key"/>, or -1. The names are compared by description, not by identity: a private getter and a
    /// private setter of one name each have their own <see cref="PrivateName"/>.
    /// </summary>
    private static int IndexOfPrivateElement(List<PrivateElement> container, PrivateName key)
    {
        for (var i = 0; i < container.Count; i++)
        {
            if (string.Equals(container[i].Key.Description, key.Description, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The rest of ClassDefinitionEvaluation for a decorated class, once every element has been evaluated
    /// (https://github.com/tc39/ecma262/pull/2417, sebastienros/jint#4413): applies the decorators of the static
    /// methods, getters, setters and accessors, then of the instance ones, defining each as it goes, then of the static
    /// fields, then of the instance fields; keeps the instance half on the constructor and adds the static private
    /// methods; applies the class's own decorators; initializes the class binding with the class they answer; and
    /// runs the static half against that class, the static methods' extra initializers first and the class's last.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Out of line, as everything a decorated class does beyond evaluating its decorators is: a class without any
    /// never reaches it, and <see cref="BuildConstructorCore{TMode}"/> only calls it.
    /// </para>
    /// <para>
    /// A public method, getter or setter was defined on its home object when it was evaluated, as an undecorated
    /// class's is, and each is defined again here, decorated, in source order, which leaves the home object as
    /// DefineMethodProperty element by element would: nothing can reach the prototype or the constructor between the
    /// two, and the second definition of a key replaces the first in place. The one difference is a first definition
    /// that throws, such as <c>@d static ['prototype']() {}</c> in a decorated class. Jint throws it in the element
    /// loop, when the element is evaluated. The spec text throws it here, after it has evaluated every later key and
    /// decorator and applied the decorators of the static elements before it. That is where Jint threw it before
    /// sebastienros/jint#4413 as well.
    /// </para>
    /// <para>
    /// The instance initializers <c>addInitializer</c> collects all run after the instance fields, as they did before:
    /// the spec text runs a method's before the fields and a field's right after it, which needs a hook in
    /// <see cref="ObjectInstance.InitializeInstanceElements"/> that every class construction would pay for
    /// (sebastienros/jint#4421).
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue FinishDecoratedClass(Engine engine, DecoratorEvaluation decorators, ScriptFunction constructor, ObjectInstance proto, DeclarativeEnvironment classEnv, string? classBinding)
    {
        var elements = decorators.Elements;

        // A getter and a setter of one private name have a PrivateName each, and what a lookup by the name finds is the
        // first one: the one the private methods are added under and the one a decorator's access has to use.
        var privateEnvironment = engine.ExecutionContext.PrivateEnvironment!;
        foreach (var element in elements)
        {
            if (element.Key is PrivateName privateName)
            {
                element.Key = privateEnvironment.ResolvePrivateIdentifier(privateName.Description) ?? privateName;
            }
        }

        var staticMethodExtraInitializers = ApplyDecoratorsAndDefineMethods(engine, elements, constructor, isStatic: true);
        var instanceMethodExtraInitializers = ApplyDecoratorsAndDefineMethods(engine, elements, proto, isStatic: false);
        ApplyFieldDecorators(engine, elements, isStatic: true);
        ApplyFieldDecorators(engine, elements, isStatic: false);

        constructor._privateMethods = InitializePrivateMethods(elements, isStatic: false);
        constructor._fields = InstanceFields(engine, elements, instanceMethodExtraInitializers);

        var staticPrivateMethods = InitializePrivateMethods(elements, isStatic: true);
        if (staticPrivateMethods is not null)
        {
            for (var i = 0; i < staticPrivateMethods.Count; i++)
            {
                constructor.PrivateMethodOrAccessorAdd(staticPrivateMethods[i]);
            }
        }

        List<ICallable>? classExtraInitializers = null;
        var classValue = ApplyDecoratorsToClassDefinition(engine, constructor, decorators.ClassDecorators, new JsString(_className ?? classBinding ?? ""), ref classExtraInitializers);

        if (_className is not null)
        {
            classEnv.InitializeBinding(_className, classValue, DisposeHint.Normal);
        }

        // The static half runs against the class the decorators answered: its fields are defined on it and its static
        // blocks get it as their this.
        var receiver = (ObjectInstance) classValue;
        RunInitializers(engine, receiver, staticMethodExtraInitializers);

        foreach (var element in elements)
        {
            if (!element.IsStatic)
            {
                continue;
            }

            if (element.Kind == DecoratedElementKind.StaticBlock)
            {
                engine.Call(element.StaticBlock!.BodyFunction, receiver);
            }
            else if (element.Kind is DecoratedElementKind.Field or DecoratedElementKind.Accessor)
            {
                ObjectInstance.DefineField(receiver, FieldDefinition(engine, element));
                RunInitializers(engine, receiver, element.ExtraInitializers);
            }
        }

        RunInitializers(engine, receiver, classExtraInitializers);

        return classValue;
    }

    /// <summary>
    /// ApplyDecoratorsAndDefineMethod for every static or every instance method, getter, setter and accessor, in
    /// source order (https://github.com/tc39/ecma262/pull/2417). Answers the extra initializers the methods',
    /// getters' and setters' decorators added; an accessor keeps its own.
    /// </summary>
    private static List<ICallable>? ApplyDecoratorsAndDefineMethods(Engine engine, List<DecoratedElement> elements, ObjectInstance homeObject, bool isStatic)
    {
        List<ICallable>? methodExtraInitializers = null;
        foreach (var element in elements)
        {
            if (element.IsStatic != isStatic || element.Kind is DecoratedElementKind.Field or DecoratedElementKind.StaticBlock)
            {
                continue;
            }

            if (element.Decorators.Length > 0)
            {
                var extraInitializers = element.Kind == DecoratedElementKind.Accessor
                    ? element.ExtraInitializers ??= []
                    : methodExtraInitializers ??= [];
                ApplyDecoratorsToElementDefinition(engine, element, extraInitializers);
            }

            DefineMethodProperty(homeObject, element);
        }

        return methodExtraInitializers;
    }

    /// <summary>
    /// ApplyDecoratorsToElementDefinition for every static or every instance field, in source order.
    /// </summary>
    private static void ApplyFieldDecorators(Engine engine, List<DecoratedElement> elements, bool isStatic)
    {
        foreach (var element in elements)
        {
            if (element.IsStatic == isStatic && element.Kind == DecoratedElementKind.Field && element.Decorators.Length > 0)
            {
                ApplyDecoratorsToElementDefinition(engine, element, element.ExtraInitializers ??= []);
            }
        }
    }

    /// <summary>
    /// DefineMethodProperty (https://tc39.es/ecma262/#sec-definemethodproperty) as the decorators spec text has it:
    /// defines a public method, getter, setter or accessor from its element record, and leaves a private one to
    /// <see cref="InitializePrivateMethods"/>.
    /// </summary>
    private static void DefineMethodProperty(ObjectInstance homeObject, DecoratedElement element)
    {
        if (element.Key is PrivateName)
        {
            return;
        }

        PropertyDescriptor descriptor = element.Kind switch
        {
            DecoratedElementKind.Method => new PropertyDescriptor(element.Value, PropertyFlag.NonEnumerable),
            DecoratedElementKind.Getter => new GetSetPropertyDescriptor(element.Get, null, PropertyFlag.Configurable),
            DecoratedElementKind.Setter => new GetSetPropertyDescriptor(null, element.Set, PropertyFlag.Configurable),
            _ => new GetSetPropertyDescriptor(element.Get, element.Set, PropertyFlag.Configurable),
        };

        homeObject.DefinePropertyOrThrow(element.Key, descriptor);
    }

    /// <summary>
    /// InitializePrivateMethods (https://github.com/tc39/ecma262/pull/2417): the private methods, getters, setters and
    /// accessors of the static or the instance half, with a getter and a setter of one name combined; null for none.
    /// </summary>
    private static List<PrivateElement>? InitializePrivateMethods(List<DecoratedElement> elements, bool isStatic)
    {
        List<PrivateElement>? privateMethods = null;
        foreach (var element in elements)
        {
            if (element.IsStatic != isStatic || element.Key is not PrivateName key || element.Kind is DecoratedElementKind.Field or DecoratedElementKind.StaticBlock)
            {
                continue;
            }

            privateMethods ??= [];
            if (element.Kind == DecoratedElementKind.Method)
            {
                privateMethods.Add(new PrivateElement { Key = key, Kind = PrivateElementKind.Method, Value = element.Value });
            }
            else if (element.Kind == DecoratedElementKind.Accessor)
            {
                privateMethods.Add(new PrivateElement { Key = key, Kind = PrivateElementKind.Accessor, Get = element.Get, Set = element.Set });
            }
            else
            {
                var getter = element.Kind == DecoratedElementKind.Getter ? element.Get : null;
                var setter = element.Kind == DecoratedElementKind.Setter ? element.Set : null;
                var index = IndexOfPrivateElement(privateMethods, key);
                if (index == -1)
                {
                    privateMethods.Add(new PrivateElement { Key = key, Kind = PrivateElementKind.Accessor, Get = getter, Set = setter });
                }
                else
                {
                    var existing = privateMethods[index];
                    privateMethods[index] = new PrivateElement { Key = existing.Key, Kind = PrivateElementKind.Accessor, Get = existing.Get ?? getter, Set = existing.Set ?? setter };
                }
            }
        }

        return privateMethods;
    }

    /// <summary>
    /// The instance fields and accessors' backing fields, in source order, then one entry running every instance
    /// extra initializer: the methods', getters' and setters' first, then each field's and accessor's in source order.
    /// </summary>
    private static List<ClassFieldDefinition> InstanceFields(Engine engine, List<DecoratedElement> elements, List<ICallable>? methodExtraInitializers)
    {
        var fields = new List<ClassFieldDefinition>();
        var extraInitializers = methodExtraInitializers;
        foreach (var element in elements)
        {
            if (element.IsStatic || element.Kind is not (DecoratedElementKind.Field or DecoratedElementKind.Accessor))
            {
                continue;
            }

            fields.Add(FieldDefinition(engine, element));
            if (element.ExtraInitializers is { Count: > 0 } own)
            {
                (extraInitializers ??= []).AddRange(own);
            }
        }

        if (extraInitializers is { Count: > 0 })
        {
            var initializers = extraInitializers.ToArray();
            var runner = new ClrFunction(engine, "", (thisObj, _) =>
            {
                RunInitializers(engine, thisObj, initializers);
                return JsValue.Undefined;
            }, 0, PropertyFlag.Configurable);

            fields.Add(new ClassFieldDefinition { Name = JsValue.Undefined, Initializer = runner });
        }

        return fields;
    }

    /// <summary>
    /// The definition a field or an accessor's backing field is initialized from: InitializeFieldOrAccessor's
    /// [[Initializers]], the element's own first, each passing its value on to the next.
    /// </summary>
    /// <remarks>
    /// The element's own initializer runs first, which is what the spec text intends rather than what it says.
    /// ClassFieldDefinitionEvaluation appends the element's own initializer to [[Initializers]], and
    /// ApplyDecoratorsToElementDefinition then prepends the decorators' initializers, so read literally the element's
    /// own runs last and its value always wins: <c>@add1 @times2 x = 3</c> would give 3.
    /// https://github.com/tc39/proposal-decorators/issues/569 reports that as a bug in the text, and Babel and the
    /// test262 tests proposed for decorators give the intended 8.
    /// </remarks>
    private static ClassFieldDefinition FieldDefinition(Engine engine, DecoratedElement element)
    {
        var field = element.Field!;
        if (element.Initializers is { Count: > 0 } decoratorInitializers)
        {
            var own = field.Initializer;
            var initializers = decoratorInitializers.ToArray();
            field.Initializer = new ClrFunction(engine, "", (thisObj, _) =>
            {
                var value = own is not null ? engine.Call(own, thisObj, Arguments.Empty) : JsValue.Undefined;
                for (var i = 0; i < initializers.Length; i++)
                {
                    value = engine.Call((JsValue) initializers[i], thisObj, [value]);
                }

                return value;
            }, 0, PropertyFlag.Configurable);

            element.Initializers = null;
        }

        return field;
    }

    /// <summary>
    /// ApplyDecoratorsToElementDefinition (https://github.com/tc39/ecma262/pull/2417): calls the element's decorators,
    /// last to first, each with the current value and a context of its own, and keeps what each answers. A field's
    /// and an accessor's initializers are prepended, so the outermost decorator's runs first.
    /// </summary>
    private static void ApplyDecoratorsToElementDefinition(Engine engine, DecoratedElement element, List<ICallable> extraInitializers)
    {
        var decorators = element.Decorators;
        var kind = element.Kind;
        for (var i = decorators.Length - 1; i >= 0; i--)
        {
            var decorator = decorators[i];
            if (decorator.Decorator is not ICallable)
            {
                Throw.TypeError(engine.Realm, "A decorator must be a function");
            }

            var decorationState = new DecorationState();
            var context = CreateDecoratorContextObject(engine, kind, element.Key, extraInitializers, decorationState, element.IsStatic);

            JsValue value;
            switch (kind)
            {
                case DecoratedElementKind.Method:
                    value = element.Value!;
                    break;
                case DecoratedElementKind.Getter:
                    value = element.Get!;
                    break;
                case DecoratedElementKind.Setter:
                    value = element.Set!;
                    break;
                case DecoratedElementKind.Accessor:
                    var accessor = new JsObject(engine);
                    accessor.CreateDataPropertyOrThrow(CommonProperties.Get, element.Get ?? JsValue.Undefined);
                    accessor.CreateDataPropertyOrThrow(CommonProperties.Set, element.Set ?? JsValue.Undefined);
                    value = accessor;
                    break;
                default:
                    value = JsValue.Undefined;
                    break;
            }

            var newValue = engine.Call(decorator.Decorator, decorator.Receiver, [value, context]);
            decorationState.Finished = true;

            if (kind == DecoratedElementKind.Field)
            {
                if (newValue is ICallable initializer)
                {
                    (element.Initializers ??= []).Insert(0, initializer);
                }
                else if (!newValue.IsUndefined())
                {
                    Throw.TypeError(engine.Realm, "A field decorator must return either undefined or a function");
                }
            }
            else if (kind == DecoratedElementKind.Accessor)
            {
                if (newValue is ObjectInstance result)
                {
                    element.Get = CallableOrCurrent(engine, result.Get(CommonProperties.Get), element.Get, "get");
                    element.Set = CallableOrCurrent(engine, result.Get(CommonProperties.Set), element.Set, "set");
                    var initializer = result.Get("init");
                    if (initializer is ICallable callable)
                    {
                        (element.Initializers ??= []).Insert(0, callable);
                    }
                    else if (!initializer.IsUndefined())
                    {
                        Throw.TypeError(engine.Realm, "An accessor decorator's init must be a function");
                    }
                }
                else if (!newValue.IsUndefined())
                {
                    Throw.TypeError(engine.Realm, "An accessor decorator must return either undefined or an object");
                }
            }
            else if (newValue is ICallable)
            {
                switch (kind)
                {
                    case DecoratedElementKind.Getter:
                        element.Get = newValue;
                        break;
                    case DecoratedElementKind.Setter:
                        element.Set = newValue;
                        break;
                    default:
                        element.Value = newValue;
                        break;
                }
            }
            else if (!newValue.IsUndefined())
            {
                Throw.TypeError(engine.Realm, "A decorator must return either undefined or a function");
            }
        }

        static JsValue? CallableOrCurrent(Engine engine, JsValue replacement, JsValue? current, string member)
        {
            if (replacement is ICallable)
            {
                return replacement;
            }

            if (!replacement.IsUndefined())
            {
                Throw.TypeError(engine.Realm, "An accessor decorator's " + member + " must be a function");
            }

            return current;
        }
    }

    /// <summary>
    /// ApplyDecoratorsToClassDefinition (https://github.com/tc39/ecma262/pull/2417): calls the class's own decorators,
    /// last to first, each with the class the one before it answered, and answers the last class.
    /// </summary>
    private static JsValue ApplyDecoratorsToClassDefinition(Engine engine, ScriptFunction constructor, DecoratorDefinition[] decorators, JsString className, ref List<ICallable>? extraInitializers)
    {
        JsValue classDefinition = constructor;
        for (var i = decorators.Length - 1; i >= 0; i--)
        {
            var decorator = decorators[i];
            if (decorator.Decorator is not ICallable)
            {
                Throw.TypeError(engine.Realm, "A decorator must be a function");
            }

            var decorationState = new DecorationState();
            var context = CreateDecoratorContextObject(engine, kind: null, className, extraInitializers ??= [], decorationState, isStatic: false);
            var newDefinition = engine.Call(decorator.Decorator, decorator.Receiver, [classDefinition, context]);
            decorationState.Finished = true;

            if (newDefinition is ICallable)
            {
                classDefinition = newDefinition;
            }
            else if (!newDefinition.IsUndefined())
            {
                Throw.TypeError(engine.Realm, "A class decorator must return either undefined or a function");
            }
        }

        return classDefinition;
    }

    /// <summary>
    /// The [[Finished]] Record a decorator's <c>addInitializer</c> captures: set once the decorator returns.
    /// </summary>
    private sealed class DecorationState
    {
        public bool Finished { get; set; }
    }

    /// <summary>
    /// CreateDecoratorContextObject, https://tc39.es/proposal-decorators/#sec-createdecoratorcontextobject as the
    /// decorators spec text has it (https://github.com/tc39/ecma262/pull/2417): <c>kind</c>, then for an element
    /// <c>access</c>, <c>static</c>, <c>private</c> and <c>name</c>, for a class only <c>name</c>, then
    /// <c>addInitializer</c>. <paramref name="kind"/> is null for a class. A private element's name is its private
    /// name's description, the <c>#</c> included.
    /// </summary>
    private static JsObject CreateDecoratorContextObject(
        Engine engine,
        DecoratedElementKind? kind,
        JsValue name,
        List<ICallable> extraInitializers,
        DecorationState decorationState,
        bool isStatic)
    {
        var context = new JsObject(engine);
        var kindName = kind switch
        {
            DecoratedElementKind.Method => "method",
            DecoratedElementKind.Getter => "getter",
            DecoratedElementKind.Setter => "setter",
            DecoratedElementKind.Accessor => "accessor",
            DecoratedElementKind.Field => "field",
            _ => "class",
        };

        context.CreateDataPropertyOrThrow("kind", kindName);
        if (kind is { } elementKind)
        {
            context.CreateDataPropertyOrThrow("access", CreateDecoratorAccessObject(engine, elementKind, name));
            context.CreateDataPropertyOrThrow("static", isStatic ? JsBoolean.True : JsBoolean.False);
            if (name is PrivateName privateName)
            {
                context.CreateDataPropertyOrThrow("private", JsBoolean.True);
                context.CreateDataPropertyOrThrow("name", "#" + privateName.Description);
            }
            else
            {
                context.CreateDataPropertyOrThrow("private", JsBoolean.False);
                context.CreateDataPropertyOrThrow("name", name);
            }
        }
        else
        {
            context.CreateDataPropertyOrThrow("name", name);
        }

        context.CreateDataPropertyOrThrow("addInitializer", CreateAddInitializerFunction(engine, extraInitializers, decorationState));
        return context;
    }

    /// <summary>
    /// CreateAddInitializerFunction (https://github.com/tc39/ecma262/pull/2417): a function that appends to
    /// <paramref name="extraInitializers"/> until the decorator it was made for returns.
    /// </summary>
    private static ClrFunction CreateAddInitializerFunction(Engine engine, List<ICallable> extraInitializers, DecorationState decorationState)
    {
        return new ClrFunction(engine, "addInitializer", (_, arguments) =>
        {
            if (decorationState.Finished)
            {
                Throw.TypeError(engine.Realm, "addInitializer cannot be called once its decorator has returned");
            }

            if (arguments.At(0) is not ICallable initializer)
            {
                Throw.TypeError(engine.Realm, "An initializer must be a function");
                return JsValue.Undefined;
            }

            extraInitializers.Add(initializer);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);
    }

    /// <summary>
    /// CreateDecoratorAccessObject (https://github.com/tc39/ecma262/pull/2417): <c>get</c> for a field, method,
    /// accessor or getter, <c>set</c> for a field, accessor or setter, and <c>has</c> for every element, each reaching
    /// the element on the object it is given: through the property key, or the private name for a private element.
    /// </summary>
    private static JsObject CreateDecoratorAccessObject(Engine engine, DecoratedElementKind kind, JsValue name)
    {
        var access = new JsObject(engine);
        var privateName = name as PrivateName;

        if (kind is DecoratedElementKind.Field or DecoratedElementKind.Method or DecoratedElementKind.Accessor or DecoratedElementKind.Getter)
        {
            access.CreateDataPropertyOrThrow(CommonProperties.Get, new ClrFunction(engine, "", (_, arguments) =>
            {
                var target = AccessTarget(engine, arguments.At(0));
                return privateName is not null ? target.PrivateGet(privateName) : target.Get(name);
            }, 1, PropertyFlag.Configurable));
        }

        if (kind is DecoratedElementKind.Field or DecoratedElementKind.Accessor or DecoratedElementKind.Setter)
        {
            access.CreateDataPropertyOrThrow(CommonProperties.Set, new ClrFunction(engine, "", (_, arguments) =>
            {
                var target = AccessTarget(engine, arguments.At(0));
                var value = arguments.At(1);
                if (privateName is not null)
                {
                    target.PrivateSet(privateName, value);
                }
                else
                {
                    target.Set(name, value, throwOnError: true);
                }

                return JsValue.Undefined;
            }, 2, PropertyFlag.Configurable));
        }

        access.CreateDataPropertyOrThrow("has", new ClrFunction(engine, "has", (_, arguments) =>
        {
            var target = AccessTarget(engine, arguments.At(0));
            var found = privateName is not null ? target.PrivateElementFind(privateName) is not null : target.HasProperty(name);
            return found ? JsBoolean.True : JsBoolean.False;
        }, 1, PropertyFlag.Configurable));

        return access;

        static ObjectInstance AccessTarget(Engine engine, JsValue value)
        {
            if (value is not ObjectInstance target)
            {
                Throw.TypeError(engine.Realm, "A decorator's access functions take an object");
                return null!;
            }

            return target;
        }
    }

    /// <summary>
    /// Calls each of <paramref name="initializers"/> with <paramref name="receiver"/> as its this, in order.
    /// </summary>
    private static void RunInitializers(Engine engine, JsValue receiver, IReadOnlyList<ICallable>? initializers)
    {
        if (initializers is null)
        {
            return;
        }

        for (var i = 0; i < initializers.Count; i++)
        {
            engine.Call((JsValue) initializers[i], receiver, Arguments.Empty);
        }
    }
}

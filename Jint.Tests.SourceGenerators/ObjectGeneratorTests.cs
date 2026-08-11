using static Jint.Tests.SourceGenerators.VerifyHelper;

namespace Jint.Tests.SourceGenerators;

#pragma warning disable NUnit1032 // Verify is used as a static helper, not async-disposable infra

[TestFixture]
public class ObjectGeneratorTests
{
    [Test]
    public Task MinimalClass()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1)]
                private static JsValue Bar(JsValue x) => x;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task PropertyConstants()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime.Descriptors;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsProperty(Name = "PI", Flags = PropertyFlag.AllForbidden)]
                private static readonly JsNumber PiValue = new(3.14);

                [JsProperty(Name = "answer")]
                private static readonly JsNumber Answer = new(42);

                [JsProperty(Flags = PropertyFlag.NonEnumerable)]
                private static readonly JsString MutableTag = new("hi");

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task SymbolMember()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime.Descriptors;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
                private static readonly JsString Tag = new("Foo");

                protected override void Initialize() => CreateSymbols_Generated();
            }
            """);
    }

    [Test]
    public Task RestParameter()
    {
        return VerifyGenerator("""
            using System;
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 0)]
                private static JsValue Concat(JsValue thisObject, JsValue first, [Rest] ReadOnlySpan<JsValue> rest)
                    => first;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task PassthroughArguments()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 2, Name = "max")]
                private static JsValue Max(JsValue thisObject, JsCallArguments arguments) => arguments.At(0);

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task InstanceMethod()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                private int _state;

                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 0)]
                private JsValue Tick(JsValue thisObject) { _state++; return JsNumber.Create(_state); }

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task NotPartial_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }
                [JsFunction] private static JsValue Bar(JsValue x) => x;
            }
            """);
    }

    [Test]
    public Task OverloadCollision_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }
                [JsFunction] private static JsValue Bar(JsValue x) => x;
                [JsFunction] private static JsValue Bar(JsValue x, JsValue y) => x;
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task UnsupportedReturnType_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }
                [JsFunction] private static int Bar(JsValue x) => 0;
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ToNumberConversion()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1)]
                private static JsValue Abs(JsValue thisObject, [ToNumber] double x)
                    => JsNumber.Create(System.Math.Abs(x));

                [JsFunction(Length = 2)]
                private static JsValue Atan2(JsValue thisObject, [ToNumber] double y, [ToNumber] double x)
                    => JsNumber.Create(System.Math.Atan2(y, x));

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task IntegerConversions()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 2)]
                private static JsValue Imul(JsValue thisObject, [ToInt32] int a, [ToInt32] int b)
                    => JsNumber.Create(a * b);

                [JsFunction(Length = 1)]
                private static JsValue PopCount(JsValue thisObject, [ToUint32] uint x)
                    => JsNumber.Create(System.Numerics.BitOperations.PopCount(x));

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ConversionTypeMismatch_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }
                [JsFunction] private static JsValue Bar(JsValue thisObject, [ToNumber] int x) => JsNumber.Create(x);
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ConflictingConversionAttrs_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }
                [JsFunction] private static JsValue Bar(JsValue thisObject, [ToNumber][ToInt32] double x) => JsNumber.Create(x);
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task RichConversions()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1)]
                private static JsValue Trunc(JsValue thisObject, [ToInteger] double n) => JsNumber.Create((long) n);

                [JsFunction(Length = 1)]
                private static JsValue ByteAt(JsValue thisObject, [ToLength] ulong i) => JsNumber.Create((double) i);

                [JsFunction(Length = 1)]
                private static JsValue Echo(JsValue thisObject, [ToString] string s) => new JsString(s);

                [JsFunction(Length = 1)]
                private static JsValue EchoJs(JsValue thisObject, [ToJsString] JsString s) => s;

                [JsFunction(Length = 1)]
                private static JsValue Wrap(JsValue thisObject, [ToObject] ObjectInstance o) => o;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task AccessorGetterAndSetter()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsAccessor("__proto__")]
                private static JsValue ProtoGet(JsValue thisObject) => JsValue.Null;

                [JsAccessor("__proto__", AccessorKind.Set)]
                private static JsValue ProtoSet(JsValue thisObject, JsValue value) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task AccessorGetterOnly()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsAccessor("size")]
                private JsValue SizeGet(JsValue thisObject) => JsNumber.Create(0);

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ThrowerAccessor()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            [JsThrowerAccessor("arguments")]
            [JsThrowerAccessor("caller")]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task SymbolFunctionMethod()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsSymbolFunction("HasInstance", Length = 1)]
                private static JsValue HasInstance(JsValue thisObject, JsValue v) => JsBoolean.False;

                protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
            }
            """);
    }

    [Test]
    public Task SymbolAccessorGetterOnly()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsSymbolAccessor("Species")]
                private static JsValue Species(JsValue thisObject) => thisObject;

                protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
            }
            """);
    }

    [Test]
    public Task ThisObjectCastToCallable()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                private readonly Realm _realm;
                internal Foo(Engine engine, Realm realm) : base(engine) { _realm = realm; }

                // ICallable thisObject — generator emits a cast + TypeError if not callable.
                [JsFunction(Length = 0, Name = "invoke")]
                private static JsValue Invoke(ICallable thisObject) => thisObject.Call(JsValue.Undefined, Arguments.Empty);

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ThisObjectCastToObjectInstance()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                private readonly Realm _realm;
                internal Foo(Engine engine, Realm realm) : base(engine) { _realm = realm; }

                [JsFunction(Length = 0, Name = "tag")]
                private static JsValue Tag(ObjectInstance thisObject) => new JsString(thisObject.Class.ToString());

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ConflictingAccessorFlags_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime.Descriptors;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsAccessor("size", Flags = PropertyFlag.Configurable)]
                private static JsValue SizeGet(JsValue thisObject) => JsNumber.PositiveZero;

                [JsAccessor("size", AccessorKind.Set, Flags = PropertyFlag.Writable)]
                private static JsValue SizeSet(JsValue thisObject, JsValue value) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task MissingRealmField_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            // No _realm field declared and ObjectInstance doesn't have one — the cast precondition
            // would emit _host._realm which wouldn't compile.
            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 0, Name = "invoke")]
                private static JsValue Invoke(ICallable thisObject) => thisObject.Call(JsValue.Undefined, Arguments.Empty);

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task AccessorWrongArity_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                // Setter with no value parameter — should be 1.
                [JsAccessor("x", AccessorKind.Set)]
                private static JsValue BadSetter(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task DuplicateThrower_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            [JsThrowerAccessor("arguments")]
            [JsThrowerAccessor("arguments")]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task IntrinsicReference()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime;

            namespace Sample;

            // Mix of plain names and IntrinsicMember overrides — covers the four casing/name patterns
            // GlobalObject uses: identical (Array), case-mismatch (JSON→Json), expansion (Generator→
            // GeneratorFunction), and lowercase JsName (eval→Eval).
            [JsObject]
            [JsIntrinsicReference("Array")]
            [JsIntrinsicReference("JSON", IntrinsicMember = "Json")]
            [JsIntrinsicReference("Generator", IntrinsicMember = "GeneratorFunction")]
            [JsIntrinsicReference("eval", IntrinsicMember = "Eval")]
            internal sealed partial class Foo : ObjectInstance
            {
                private readonly Realm _realm;
                internal Foo(Engine engine, Realm realm) : base(engine) { _realm = realm; }
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ShapeIntrinsicReference()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime;

            namespace Sample;

            // Shape-path intrinsic references become Factory slots producing the same lazy
            // per-realm descriptor the dictionary path uses.
            [JsObject(UseShape = true)]
            [JsIntrinsicReference("Array")]
            [JsIntrinsicReference("JSON", IntrinsicMember = "Json")]
            internal sealed partial class Foo : ObjectInstance
            {
                private readonly Realm _realm;
                internal Foo(Engine engine, Realm realm) : base(engine) { _realm = realm; }

                [JsFunction(Length = 1)]
                private static JsValue Bar(JsValue x) => x;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ShapeThrowerAccessor()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime;

            namespace Sample;

            // Shape-path throwers become Factory slots producing realm-pinned %ThrowTypeError%
            // accessor descriptors.
            [JsObject(UseShape = true)]
            [JsThrowerAccessor("arguments")]
            [JsThrowerAccessor("caller")]
            internal sealed partial class Foo : ObjectInstance
            {
                private readonly Realm _realm;
                internal Foo(Engine engine, Realm realm) : base(engine) { _realm = realm; }

                [JsFunction(Length = 1)]
                private static JsValue Bar(JsValue x) => x;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task DuplicateIntrinsicReference_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime;

            namespace Sample;

            [JsObject]
            [JsIntrinsicReference("Array")]
            [JsIntrinsicReference("Array")]
            internal sealed partial class Foo : ObjectInstance
            {
                private readonly Realm _realm;
                internal Foo(Engine engine, Realm realm) : base(engine) { _realm = realm; }
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task IntrinsicReferenceWithoutRealmField_ProducesDiagnostic()
    {
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            // [JsIntrinsicReference]'s emitted lambda body is `host => host._realm.Intrinsics.X`;
            // without an accessible _realm field on the host, the generated code wouldn't compile —
            // catch it via JINT018 at generator time rather than letting the C# compiler complain.
            [JsObject]
            [JsIntrinsicReference("Array")]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }
                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task CoercedRestSpan()
    {
        // [Rest, ToNumber] ReadOnlySpan<double>: dispatcher emits a stackalloc-or-heap span +
        // coerce-loop preamble that runs before the host method (per spec, every element's
        // ToNumber must complete before any scanning logic — observable via valueOf side effects).
        // This shape replaces the hand-rolled pattern in MathInstance.Max/Min/Hypot.
        return VerifyGenerator("""
            using System;
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 2)]
                private static JsValue Max(JsValue thisObject, [Rest, ToNumber] ReadOnlySpan<double> values)
                {
                    var highest = double.NegativeInfinity;
                    for (var i = 0; i < values.Length; i++) if (values[i] > highest) highest = values[i];
                    return JsNumber.Create(highest);
                }

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task SymbolAlias()
    {
        // [JsSymbolAlias] registers the SAME function object under a well-known symbol as a generated
        // string-keyed member — Array.prototype[@@iterator] === values — with an optional capture
        // field for host identity fast paths.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            [JsSymbolAlias("Iterator", "values", CaptureField = nameof(_originalIteratorFunction))]
            internal sealed partial class Foo : ObjectInstance
            {
                internal JsValue _originalIteratorFunction;

                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 0)]
                private static JsValue Values(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
            }
            """);
    }

    [Test]
    public Task ShapeSymbolAlias()
    {
        // Shape host variant: symbols live beside the shape in the symbol dictionary, and the alias
        // materializes the target's shape slot before wrapping it.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject(UseShape = true)]
            [JsSymbolAlias("Iterator", "values")]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 0)]
                private static JsValue Values(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
            }
            """);
    }

    [Test]
    public Task FunctionCaptureField()
    {
        // [JsFunction(CaptureField = ...)]: after SetProperties the generated code materializes the
        // member's descriptor value (GetOwnProperty(...).Value — idempotent, LazyPropertyDescriptor
        // caches on first read) and assigns the SAME instance to the named host field — the
        // ReferenceEquals fast-path snapshot pattern (WeakSetPrototype.OriginalAddFunction).
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal JsValue? _originalNextFunction;

                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Name = "next", CaptureField = nameof(_originalNextFunction))]
                private static JsValue NextHandler(JsValue thisObject) => JsValue.Undefined;

                [JsFunction(Length = 0)]
                private static JsValue Values(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ShapeFunctionCaptureField()
    {
        // Shape-host variant: the capture materializes the member's shape slot via GetOwnProperty
        // (cached in the per-realm descriptor array, so the captured instance is identity-stable) —
        // mirrors ArrayIteratorPrototype._originalNextFunction.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject(UseShape = true)]
            internal sealed partial class Foo : ObjectInstance
            {
                internal JsValue? _originalNextFunction;

                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Name = "next", CaptureField = nameof(_originalNextFunction))]
                private static JsValue NextHandler(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task SymbolFunctionCaptureField()
    {
        // [JsSymbolFunction(CaptureField = ...)] forces the member EAGER: the dispatcher Function is
        // constructed inside CreateSymbols_Generated, assigned to the capture field, and that same
        // instance registered under the symbol — mirrors StringPrototype._originalIteratorFunction.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime.Descriptors;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal JsValue? _originalIteratorFunction;

                internal Foo(Engine engine) : base(engine) { }

                [JsSymbolFunction("Iterator", Flags = PropertyFlag.Configurable | PropertyFlag.Writable, CaptureField = nameof(_originalIteratorFunction))]
                private static JsValue Iterator(JsValue thisObject) => JsValue.Undefined;

                [JsFunction(Length = 0)]
                private static JsValue Values(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() { CreateProperties_Generated(); CreateSymbols_Generated(); }
            }
            """);
    }

    [Test]
    public Task StringAliasSharesDescriptor()
    {
        // [JsAlias] on both storage paths: the alias entry shares the target member's descriptor
        // (function-identity aliases like Set.prototype.keys === values).
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            [JsAlias("keys", "values")]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 0)]
                private static JsValue Values(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ShapeAliasAndInstanceSlot()
    {
        // Shape-path [JsAlias] (builder.Alias shares the target slot) and [JsInstanceSlot] (a
        // reserved host-filled slot appended last, populated via SetBuiltinSlotByName in Initialize).
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject(UseShape = true)]
            [JsAlias("keys", "values")]
            [JsInstanceSlot("toString")]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 0)]
                private static JsValue Values(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task CoercedRestSpanTypeMismatch_ProducesDiagnostic()
    {
        // [Rest, ToNumber] requires ReadOnlySpan<double>. Span<int> here is wrong — JINT013 fires.
        return VerifyGenerator("""
            using System;
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 2)]
                private static JsValue Bad(JsValue thisObject, [Rest, ToNumber] ReadOnlySpan<int> values)
                    => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallLanes()
    {
        // The two lanes side by side, plus an opted-out slot so the switch keeps a default arm.
        // Plain (FastCall) reports Supported with no guards — the frame is still pushed, so passing
        // arguments in registers is safe for any value. Leaf additionally reports the guards under
        // which frame elision is legal: [ToNumber] is a no-op for an actual number, [ToJsString] for
        // an actual string, [ToInteger] for a number again. Every Leaf value parameter must declare
        // its conversion; an undeclared one is rejected (see LeafOnUnguardableHazards).
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1, FastCall = true)]
                private static JsValue Passthrough(JsValue thisObject, JsValue value) => value;

                [JsFunction(Length = 1, Leaf = true)]
                private static JsValue Abs(JsValue thisObject, [ToNumber] double x) => JsNumber.Create(x);

                [JsFunction(Length = 2, Leaf = true)]
                private static JsValue CharAt(JsValue thisObject, [ToJsString] JsString s, [ToInteger] double index) => s;

                [JsFunction(Length = 0)]
                private static JsValue Untouched(JsValue thisObject) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallTypedReceiver()
    {
        // The receiver guard comes from the declared cast type, not from a hand-written check:
        // JsDate is sealed and FastCallGuard.Date tests `is JsDate`, so a passing guard proves the
        // emitted cast succeeds and its TypeError is unreachable. ArrayInstance maps to
        // FastCallGuard.Array the same way (InternalTypes.Array is set by exactly that hierarchy).
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Array;
            using Jint.Native.Object;
            using Jint.Runtime;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                private readonly Realm _realm;
                internal Foo(Engine engine, Realm realm) : base(engine) { _realm = realm; }

                [JsFunction(Length = 0, Leaf = true)]
                private static JsValue GetTime(JsDate thisObject) => JsNumber.Create(0);

                [JsFunction(Length = 0, Leaf = true)]
                private static JsValue ArrayLength(ArrayInstance thisObject) => JsNumber.Create(thisObject.Length);

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallSingleSlot()
    {
        // A host with exactly one [JsFunction] has no Slot enum and no _slot field, so both fast-call
        // overrides collapse to expression/flat bodies with no switch.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1, Leaf = true)]
                private static JsValue Trunc(JsValue thisObject, [ToNumber] double x) => JsNumber.Create(x);

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task ArgCountParameter()
    {
        // [ArgCount] lets a body that must tell an ABSENT argument from an explicit `undefined` keep
        // declaring its real parameters positionally instead of taking the raw JsCallArguments array
        // — the Date-setter and reduce shapes. It is not JS-visible: `length` still reports 2.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction]
                private static JsValue Reduce(JsValue thisObject, JsValue callback, JsValue initialValue, [ArgCount] int argc)
                    => argc < 2 ? JsValue.Undefined : initialValue;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallOnRawArguments_ProducesDiagnostic()
    {
        // The raw array has no positional model, so there is nothing to put in the registers — JINT023.
        // Spelled JsValue[] rather than the JsCallArguments alias, which is a global using in the Jint
        // project and so is not bound in this standalone test compilation (see PassthroughArguments,
        // where it degrades to JINT011 before the array shape is ever recognized).
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1, FastCall = true)]
                private static JsValue Bad(JsValue thisObject, JsValue[] arguments) => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallOnOverlongArity_ProducesDiagnostic()
    {
        // Three declared value parameters overflow the two argument registers, and unlike a variadic
        // tail there is no arity at which they fit — JINT023 rather than a silent no-op.
        return VerifyGenerator("""
            using System;
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 3, Leaf = true)]
                private static JsValue TooWide(JsValue thisObject, JsValue a, JsValue b, JsValue c) => a;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallVariadicLanes()
    {
        // A [Rest] tail takes the lane through CallFastVariadic, which carries a span sized to the
        // call site's arity instead of two registers padded with undefined — the distinction a
        // variadic body would otherwise see as an extra element. GetFastCallShape becomes
        // arity-keyed for those slots: it declines the arities that overflow the registers, and
        // publishes the tail's guard only for the registers the tail actually receives, so
        // `Max(a)` is still leaf even though register 1 holds padding. The fixed-arity slot
        // alongside keeps its own CallFast entry point.
        return VerifyGenerator("""
            using System;
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 2, Leaf = true)]
                private static JsValue Max(JsValue thisObject, [Rest, ToNumber] ReadOnlySpan<double> values)
                    => JsNumber.Create(values.Length);

                [JsFunction(Length = 1, FastCall = true)]
                private static JsValue Push(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> values)
                    => JsNumber.Create(values.Length);

                [JsFunction(Length = 1, FastCall = true)]
                private static JsValue Concat(JsValue thisObject, JsValue first, [Rest] ReadOnlySpan<JsValue> rest)
                    => first;

                [JsFunction(Length = 1, Leaf = true)]
                private static JsValue Abs(JsValue thisObject, [ToNumber] double x) => JsNumber.Create(x);

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallVariadicSingleSlot()
    {
        // A host with one [JsFunction] has no Slot enum, so the arity switch is the whole body of
        // GetFastCallShape and CallFastVariadic collapses to a flat one.
        return VerifyGenerator("""
            using System;
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 2, Leaf = true)]
                private static JsValue Min(JsValue thisObject, [Rest, ToNumber] ReadOnlySpan<double> values)
                    => JsNumber.Create(values.Length);

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallDeclaredArgumentGuards()
    {
        // LeafArg0/LeafArg1 are how a body that takes a raw JsValue and coerces it itself earns the
        // frameless lane back: the declaration names the values it is leaf-safe for, and the runtime
        // checks them per call. Undefined is composed in because the call site pads absent arguments
        // with it, so a Number-only guard would cost the lane every one-argument call of a
        // two-parameter method. A parameter that already derives a guard from its conversion ignores
        // a declared one — an explicit value must never be able to weaken a derived guard.
        //
        // Unguarded is the declaration for a parameter that needs no precondition at all, because the
        // body inspects the value instead of coercing it. It is the only guard that is not emitted:
        // the runtime spells "every value satisfies this" and "there is nothing to test" the same
        // way, as Any, so Unguarded lowers to Any and the difference survives only where it matters,
        // in whether the generator accepts Leaf for a raw JsValue at all.
        //
        // FastCallGuard is internal to Jint and this compilation is not on its InternalsVisibleTo
        // list, so the test declares its own — source wins over an inaccessible imported type, and
        // the values are what the generator reads back. They have to be the real ones: the guard
        // travels as a number, so a stub with different values would exercise a different rendering
        // than the one that ships. That the real enum has these values is pinned by
        // FastCallGuardValuesMatchInternalTypes; what is being verified here is the rendering.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Native.Function;

            namespace Jint.Native.Function
            {
                [System.Flags]
                internal enum FastCallGuard
                {
                    Any = 0,
                    Undefined = 1,
                    String = 8,
                    Number = 16 | 32,
                    Array = 16384,
                    Unguarded = 1 << 29,
                    Date = 1 << 30,
                }
            }

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 2, Leaf = true, LeafReceiver = FastCallGuard.String,
                    LeafArg0 = FastCallGuard.Number | FastCallGuard.Undefined,
                    LeafArg1 = FastCallGuard.Number | FastCallGuard.Undefined)]
                private static JsValue Substring(JsValue thisObject, JsValue startArg, JsValue endArg) => thisObject;

                [JsFunction(Length = 1, Leaf = true, LeafReceiver = FastCallGuard.String, LeafArg0 = FastCallGuard.Number)]
                private static JsValue CharCodeAt(JsValue thisObject, JsValue pos) => pos;

                [JsFunction(Length = 1, Leaf = true, LeafArg0 = FastCallGuard.String)]
                private static JsValue Derived(JsValue thisObject, [ToNumber] double x) => JsNumber.Create(x);

                [JsFunction(Length = 1, Leaf = true, LeafArg0 = FastCallGuard.Unguarded)]
                private static JsValue Inspected(JsValue thisObject, JsValue value) => value is JsNumber ? JsNumber.Create(1) : JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallKeyedCollectionGuards()
    {
        // The keyed-collection shape: a receiver guard naming the brand the body checks, and arguments
        // declared AnyValue. AnyValue is the one declaration-only kind — a Map key is hashed and
        // compared and never converted, so the author's claim is that there is no hazard at all, and a
        // claim of no hazard publishes no constraint. It must therefore come back out as
        // FastCallGuard.Any, not as a member the runtime enum would treat as unmatchable, and it must
        // subsume anything composed with it (Impostor below). What is verified here is that rendering;
        // that the numbers are the real ones is pinned by FastCallGuardValuesMatchInternalTypes.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Native.Function;

            namespace Jint.Native.Function
            {
                [System.Flags]
                internal enum FastCallGuard
                {
                    Any = 0,
                    Undefined = 1,
                    String = 8,
                    Number = 16 | 32,
                    Array = 16384,
                    Map = 134217728,
                    Set = 268435456,
                    AnyValue = 1 << 29,
                    Date = 1 << 30,
                }
            }

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1, Leaf = true, LeafReceiver = FastCallGuard.Map, LeafArg0 = FastCallGuard.AnyValue)]
                private static JsValue MapGet(JsValue thisObject, JsValue key) => key;

                [JsFunction(Length = 2, Leaf = true, LeafReceiver = FastCallGuard.Map,
                    LeafArg0 = FastCallGuard.AnyValue, LeafArg1 = FastCallGuard.AnyValue)]
                private static JsValue MapSet(JsValue thisObject, JsValue key, JsValue value) => value;

                [JsFunction(Length = 1, Leaf = true, LeafReceiver = FastCallGuard.Set, LeafArg0 = FastCallGuard.AnyValue)]
                private static JsValue SetAdd(JsValue thisObject, JsValue value) => value;

                // Rendering only: a composed receiver has to come back out member by member, and a
                // declaration that composes AnyValue with a narrower kind is still no constraint.
                [JsFunction(Length = 1, Leaf = true, LeafReceiver = FastCallGuard.Map | FastCallGuard.Set,
                    LeafArg0 = FastCallGuard.AnyValue | FastCallGuard.String)]
                private static JsValue Impostor(JsValue thisObject, JsValue key) => key;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task LeafOnUncoercedRest_ProducesDiagnostic()
    {
        // A [Rest] tail with no declared conversion hands the body raw JsValues, which is the same
        // unguardable hazard a plain JsValue parameter is — and unlike one, no per-position
        // declaration could speak for a list of unbounded length. FastCall alone is fine; Leaf is
        // JINT023.
        return VerifyGenerator("""
            using System;
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1, Leaf = true)]
                private static JsValue Bad(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> values)
                    => JsValue.Undefined;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task FastCallOnArgCount_ProducesDiagnostic()
    {
        // CallFast carries a receiver and two argument registers but no arity, so a body reading the
        // argument count cannot be invoked through it — the two features are mutually exclusive.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                internal Foo(Engine engine) : base(engine) { }

                [JsFunction(Length = 1, FastCall = true)]
                private static JsValue Bad(JsValue thisObject, JsValue value, [ArgCount] int argc)
                    => argc == 0 ? JsValue.Undefined : value;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }

    [Test]
    public Task LeafOnUnguardableHazards_ProducesDiagnostic()
    {
        // Frame elision needs every route to user code or a JS error closed off by a guard the
        // runtime can check. None of these four can be: ObjectInstance has no matching
        // FastCallGuard, [ToObject] throws for null/undefined and "not nullish" is not expressible,
        // [RequireObjectCoercible] on an unguarded 'this' is the same problem, and a JsValue
        // parameter with neither a conversion nor a LeafArgN declaration hides whatever the body
        // does with it — the shape that let four String.prototype methods run a user valueOf
        // frameless. Each is an error at the declaration rather than a silent downgrade to the
        // non-leaf lane.
        return VerifyGenerator("""
            using Jint;
            using Jint.Native;
            using Jint.Native.Object;
            using Jint.Runtime;

            namespace Sample;

            [JsObject]
            internal sealed partial class Foo : ObjectInstance
            {
                private readonly Realm _realm;
                internal Foo(Engine engine, Realm realm) : base(engine) { _realm = realm; }

                [JsFunction(Length = 0, Leaf = true)]
                private static JsValue UnguardedCast(ObjectInstance thisObject) => JsValue.Undefined;

                [JsFunction(Length = 1, Leaf = true)]
                private static JsValue Coerced(JsValue thisObject, [ToObject] ObjectInstance value) => value;

                [RequireObjectCoercible]
                [JsFunction(Length = 0, Leaf = true)]
                private static JsValue NeedsCoercible(JsValue thisObject) => JsValue.Undefined;

                [JsFunction(Length = 1, Leaf = true)]
                private static JsValue Undeclared(JsValue thisObject, JsValue pos) => pos;

                protected override void Initialize() => CreateProperties_Generated();
            }
            """);
    }
}

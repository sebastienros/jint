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
}

using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace Jint;

/// <summary>
/// Provides the standard $262 test harness object for ECMAScript Test262 conformance testing.
/// See https://github.com/tc39/test262/blob/main/INTERPRETING.md
/// </summary>
internal static class Test262Object
{
    /// <summary>
    /// Installs the $262 object into the engine's global scope and returns it.
    /// </summary>
    public static ObjectInstance Install(Engine engine) => Install(engine, engine.Realm);

    /// <summary>
    /// Installs a $262 object onto the global object of <paramref name="realm"/> and returns it.
    /// <para>
    /// Everything it carries is built from <paramref name="realm"/>'s own intrinsics and pinned to that
    /// realm, because <c>$262.createRealm()</c> installs an API into a realm other than the one running.
    /// A function left pinned to the caller's realm would report its errors — and hand out its
    /// prototypes — from the wrong one.
    /// </para>
    /// </summary>
    public static ObjectInstance Install(Engine engine, Realm realm)
    {
        // Not Intrinsics.Object.Construct: for a plain construction that lands on `new JsObject(engine)`,
        // which takes the *active* realm's Object.prototype. Name the prototype explicitly instead.
        var o = ObjectInstance.OrdinaryObjectCreate(engine, realm.Intrinsics.Object.PrototypeObject);

        // "A reference to the global object on which the $262 object lives."
        o.FastSetProperty("global", new PropertyDescriptor(realm.GlobalObject, true, true, true));

        // %AbstractModuleSource% intrinsic - exposed via $262 for source-phase-imports tests
        o.FastSetProperty("AbstractModuleSource", new PropertyDescriptor(CreateAbstractModuleSource(engine, realm), true, true, true));

        o.FastSetProperty("evalScript", new PropertyDescriptor(new ClrFunction(engine, realm, "evalScript",
            (_, args) =>
            {
                if (args.Length > 1)
                {
                    throw new ArgumentException("only script parsing supported", nameof(args));
                }

                return EvaluateInRealm(engine, realm, args.At(0).AsString());
            }, 0), true, true, true));

        // Per test262 INTERPRETING.md, createRealm "creates a new ECMAScript Realm, defines this API on
        // the new realm's global object, and returns the $262 property of the new realm's global object".
        // So the whole API - createRealm included, so realms can nest - goes onto the new global, and the
        // new realm's own $262 is what comes back.
        o.FastSetProperty("createRealm", new PropertyDescriptor(new ClrFunction(engine, realm, "createRealm",
            (_, _) => Install(engine, engine._host.CreateRealm()), 0), true, true, true));

        o.FastSetProperty("detachArrayBuffer", new PropertyDescriptor(new ClrFunction(engine, realm, "detachArrayBuffer",
            (_, args) =>
            {
                var buffer = (JsArrayBuffer) args.At(0);
                buffer.DetachArrayBuffer();
                return JsValue.Undefined;
            }, 0), true, true, true));

        o.FastSetProperty("gc", new PropertyDescriptor(new ClrFunction(engine, realm, "gc",
            (_, _) =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return JsValue.Undefined;
            }, 0), true, true, true));

        o.FastSetProperty("IsHTMLDDA", new PropertyDescriptor(new IsHTMLDDA(engine, realm), true, true, true));

        realm.GlobalObject.Set("$262", o);
        return o;
    }

    /// <summary>
    /// Evaluates a script in the global scope of <paramref name="realm"/> (rather than the engine's
    /// currently active realm), mirroring InitializeHostDefinedRealm: a global execution context for the
    /// target realm is pushed for the duration of the evaluation.
    /// </summary>
    private static JsValue EvaluateInRealm(Engine engine, Realm realm, string source)
    {
        var script = Engine.PrepareScript(source, options: new ScriptPreparationOptions
        {
            ParsingOptions = ScriptParsingOptions.Default with { Tolerant = false, RetainFunctionSourceText = true },
        });

        var context = new ExecutionContext(
            scriptOrModule: null,
            lexicalEnvironment: realm.GlobalEnv,
            variableEnvironment: realm.GlobalEnv,
            privateEnvironment: null,
            realm: realm,
            function: null,
            // The harness scripts pushed via engine.Evaluate below carry their own strictness.
            strict: false);

        engine.EnterExecutionContext(in context);
        try
        {
            return engine.Evaluate(in script);
        }
        finally
        {
            engine.LeaveExecutionContext();
        }
    }

    /// <summary>
    /// Creates the %AbstractModuleSource% intrinsic constructor and prototype.
    /// https://tc39.es/proposal-source-phase-imports/#sec-%abstractmodulesource%
    /// </summary>
    private static ClrFunction CreateAbstractModuleSource(Engine engine, Realm realm)
    {
        // Create the prototype object (see the note in Install about naming the prototype explicitly)
        var proto = ObjectInstance.OrdinaryObjectCreate(engine, realm.Intrinsics.Object.PrototypeObject);

        // @@toStringTag getter on prototype
        var toStringTagGetter = new ClrFunction(engine, realm, "get [Symbol.toStringTag]", (thisObj, _) =>
        {
            if (thisObj is not ObjectInstance)
            {
                return JsValue.Undefined;
            }

            // Check for [[ModuleSourceClassName]] internal slot - not implemented for SourceTextModules
            return JsValue.Undefined;
        }, 0, PropertyFlag.Configurable);

        proto.DefineOwnProperty(GlobalSymbolRegistry.ToStringTag, new GetSetPropertyDescriptor(
            get: toStringTagGetter,
            set: JsValue.Undefined,
            PropertyFlag.Configurable));

        // The constructor function that always throws TypeError
        var ctor = new ClrFunction(engine, realm, "AbstractModuleSource", (_, _) =>
        {
            Throw.TypeError(realm, "Abstract class constructor %AbstractModuleSource% cannot be invoked");
            return JsValue.Undefined;
        }, 0, PropertyFlag.Configurable);

        // Set up constructor <-> prototype relationship
        ctor.DefineOwnProperty(CommonProperties.Prototype, new PropertyDescriptor(proto, PropertyFlag.AllForbidden));
        proto.DefineOwnProperty(CommonProperties.Constructor, new PropertyDescriptor(ctor, PropertyFlag.NonEnumerable));

        return ctor;
    }
}

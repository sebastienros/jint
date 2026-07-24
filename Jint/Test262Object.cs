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
    public static ObjectInstance Install(Engine engine)
    {
        var o = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);

        // %AbstractModuleSource% intrinsic - exposed via $262 for source-phase-imports tests
        o.FastSetProperty("AbstractModuleSource", new PropertyDescriptor(CreateAbstractModuleSource(engine), true, true, true));

        o.FastSetProperty("evalScript", new PropertyDescriptor(new ClrFunction(engine, "evalScript",
            (_, args) =>
            {
                if (args.Length > 1)
                {
                    throw new ArgumentException("only script parsing supported", nameof(args));
                }

                var script = Engine.PrepareScript(args.At(0).AsString(), options: new ScriptPreparationOptions
                {
                    ParsingOptions = ScriptParsingOptions.Default with { Tolerant = false, RetainFunctionSourceText = true },
                });

                return engine.Evaluate(script);
            }), true, true, true));

        o.FastSetProperty("createRealm", new PropertyDescriptor(new ClrFunction(engine, "createRealm",
            (_, _) =>
            {
                var realm = engine._host.CreateRealm();
                var newGlobal = realm.GlobalObject;
                newGlobal.Set("global", newGlobal);
                // Per test262 INTERPRETING.md, the object returned by createRealm exposes an evalScript
                // that evaluates in the new realm. Provide it so cross-realm tests (e.g. FallbackSymbol
                // per-realm) can run code against the freshly created realm's intrinsics.
                newGlobal.Set("evalScript", new ClrFunction(engine, "evalScript",
                    (_, args) => EvaluateInRealm(engine, realm, args.At(0).AsString())));
                return newGlobal;
            }), true, true, true));

        o.FastSetProperty("detachArrayBuffer", new PropertyDescriptor(new ClrFunction(engine, "detachArrayBuffer",
            (_, args) =>
            {
                var buffer = (JsArrayBuffer) args.At(0);
                buffer.DetachArrayBuffer();
                return JsValue.Undefined;
            }), true, true, true));

        o.FastSetProperty("gc", new PropertyDescriptor(new ClrFunction(engine, "gc",
            (_, _) =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return JsValue.Undefined;
            }), true, true, true));

        o.FastSetProperty("IsHTMLDDA", new PropertyDescriptor(new IsHTMLDDA(engine), true, true, true));

        engine.SetValue("$262", o);
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
            ParsingOptions = ScriptParsingOptions.Default with { Tolerant = false },
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

        engine.EnterExecutionContext(context);
        try
        {
            return engine.Evaluate(script);
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
    private static ClrFunction CreateAbstractModuleSource(Engine engine)
    {
        var realm = engine.Realm;

        // Create the prototype object
        var proto = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);

        // @@toStringTag getter on prototype
        var toStringTagGetter = new ClrFunction(engine, "get [Symbol.toStringTag]", (thisObj, _) =>
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
        var ctor = new ClrFunction(engine, "AbstractModuleSource", (_, _) =>
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

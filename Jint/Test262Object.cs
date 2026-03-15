using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
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

        o.FastSetProperty("evalScript", new PropertyDescriptor(new ClrFunction(engine, "evalScript",
            (_, args) =>
            {
                if (args.Length > 1)
                {
                    throw new ArgumentException("only script parsing supported", nameof(args));
                }

                var script = Engine.PrepareScript(args.At(0).AsString(), options: new ScriptPreparationOptions
                {
                    ParsingOptions = ScriptParsingOptions.Default with { Tolerant = false },
                });

                return engine.Evaluate(script);
            }), true, true, true));

        o.FastSetProperty("createRealm", new PropertyDescriptor(new ClrFunction(engine, "createRealm",
            (_, _) =>
            {
                var realm = engine._host.CreateRealm();
                realm.GlobalObject.Set("global", realm.GlobalObject);
                return realm.GlobalObject;
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
}

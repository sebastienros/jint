using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Test262Harness;

namespace Jint.Tests.Test262;

public abstract partial class Test262Test
{
    private static Engine BuildTestExecutor(Test262File file)
    {
        var engine = new Engine(cfg =>
        {
            var relativePath = Path.GetDirectoryName(file.FileName);
            cfg.EnableModules(new Test262ModuleLoader(State.Test262Stream.Options.FileSystem, relativePath));
            cfg.ExperimentalFeatures = ExperimentalFeature.All;
        });

        if (file.Flags.Contains("raw"))
        {
            // nothing should be loaded
            return engine;
        }

        engine.Execute(State.Sources["assert.js"]);
        engine.Execute(State.Sources["sta.js"]);

        engine.SetValue("print", new ClrFunction(engine, "print", (_, args) => TypeConverter.ToString(args.At(0))));

        var o = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
        o.FastSetProperty("evalScript", new PropertyDescriptor(new ClrFunction(engine, "evalScript",
            (_, args) =>
            {
                if (args.Length > 1)
                {
                    throw new Exception("only script parsing supported");
                }

                var script = Engine.PrepareScript(args.At(0).AsString(), options: new ScriptPreparationOptions
                {
                    ParsingOptions = ScriptParsingOptions.Default with { Tolerant = false },
                });

                return engine.Evaluate(script);
            }), true, true, true));

        o.FastSetProperty("createRealm", new PropertyDescriptor(new ClrFunction(engine, "createRealm",
            (_, args) =>
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

        engine.SetValue("$262", o);

        foreach (var include in file.Includes)
        {
            engine.Execute(State.Sources[include]);
        }

        if (file.Flags.Contains("async"))
        {
            engine.Execute(State.Sources["doneprintHandle.js"]);
        }

        return engine;
    }

    private static void ExecuteTest(Engine engine, Test262File file)
    {
        if (file.Type == ProgramType.Module)
        {
            var specifier = "./" + Path.GetFileName(file.FileName);
            engine.Modules.Add(specifier, builder => builder.AddSource(file.Program));
            engine.Modules.Import(specifier);
        }
        else
        {
            var script = Engine.PrepareScript(file.Program, source: file.FileName, options: new ScriptPreparationOptions
            {
                ParsingOptions = ScriptParsingOptions.Default with { Tolerant = false },
            });

            engine.Execute(script);
        }
    }

    private partial bool ShouldThrow(Test262File testCase, bool strict)
    {
        return testCase.Negative;
    }
}

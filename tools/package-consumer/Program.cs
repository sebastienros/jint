using System;
using Jint;
using Jint.Native;

namespace PackageConsumer
{
    /// <summary>
    /// A host type of the shape the feature exists for. Two of its members are deliberately outside what
    /// the generator can express, so that the run also says what happens to the ones it declines.
    /// </summary>
    [JsAccessible]
    public sealed class Player
    {
        public int Score { get; set; }

        public string Name { get; set; } = "";

        public JsValue Describe(JsValue prefix)
        {
            return prefix.ToString() + Name;
        }

        // JINT033: a `string` parameter's reflected binding is a conversion chain steered by engine
        // options, so the generator declines it and this stays on the reflection path.
        public string Shout(string text)
        {
            return text.ToUpperInvariant();
        }
    }

    public static class Program
    {
        public static int Main()
        {
            var failures = 0;

            // This line is the whole point of the exercise. `JsAccessibleRegistration` does not exist in
            // Jint; it is emitted into THIS assembly by the analyzer inside the Jint package, so if the
            // generator did not travel, the project does not compile and the run never happens.
            JsAccessibleRegistration.RegisterAll();

            var engine = new Engine(options => options.Interop.AllowWrite = true);
            engine.SetValue("player", new Player { Score = 41, Name = "Ada" });

            Check(ref failures, "read", "41", engine.Evaluate("String(player.Score)").AsString());
            Check(ref failures, "write", "42", engine.Evaluate("player.Score = 42; String(player.Score)").AsString());
            Check(ref failures, "invoke", "hi Ada", engine.Evaluate("player.Describe('hi ')").AsString());
            Check(ref failures, "a declined member still resolves", "LOUD", engine.Evaluate("player.Shout('loud')").AsString());

            // The one observable that says the GENERATED lane answered rather than the reflected one: a
            // generated method is a function object with its own `length`, where a reflected one reports the
            // arity it inherits from Function.prototype. Without this the assertions above would also pass
            // against a package carrying no generator at all.
            Check(ref failures, "the generated lane is engaged", "1", engine.Evaluate("String(player.Describe.length)").AsString());
            Check(ref failures, "the declined member is not", "0", engine.Evaluate("String(player.Shout.length)").AsString());

            // Jint.SourceGenerators must NOT be in the package, and this is the observable that says so.
            // Its post-initialization output declares Jint.JsObjectAttribute and friends into whichever
            // compilation references it, unconditionally — and that source names
            // Jint.Native.Function.FastCallGuard, which is internal to Jint, so a consumer referencing that
            // analyzer would fail to compile before reaching a line of their own code. The build succeeding
            // is half the evidence; this is the other half, because it would also catch the attribute set
            // arriving by some route that happened to compile.
            var leaked = typeof(Program).Assembly.GetType("Jint.JsObjectAttribute", false);
            Check(ref failures, "the [JsObject] attribute source stayed out", "absent", leaked is null ? "absent" : leaked.FullName ?? "present");

            // ... while [JsAccessible] itself is a public type shipped in Jint.dll, which is exactly why the
            // generator a third party references needs no post-initialization output of its own.
            Check(ref failures, "[JsAccessible] comes from Jint.dll", "Jint", typeof(JsAccessibleAttribute).Assembly.GetName().Name ?? "unknown");

            Console.WriteLine(failures == 0 ? "ALL PROBES PASSED" : failures + " PROBE(S) FAILED");
            return failures == 0 ? 0 : 1;
        }

        private static void Check(ref int failures, string what, string expected, string actual)
        {
            var ok = string.Equals(expected, actual, StringComparison.Ordinal);
            if (!ok)
            {
                failures++;
            }

            Console.WriteLine((ok ? "ok   " : "FAIL ") + what + ": expected '" + expected + "', got '" + actual + "'");
        }
    }
}

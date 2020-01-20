using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.References;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a Lexical Environment (a.k.a Scope)
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.2
    /// </summary>
    public sealed class LexicalEnvironment
    {
        private readonly Engine _engine;
        internal EnvironmentRecord _record;
        internal LexicalEnvironment _outer;

        public LexicalEnvironment(Engine engine, EnvironmentRecord record, LexicalEnvironment outer)
        {
            _engine = engine;
            _record = record;
            _outer = outer;
        }

        public EnvironmentRecord Record => _record;

        public LexicalEnvironment Outer => _outer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Reference GetIdentifierReference(LexicalEnvironment lex, JsValue name, bool strict)
        {
            var identifierEnvironment = TryGetIdentifierEnvironmentWithBindingValue(lex, name, strict, out var temp, out _)
                ? temp
                : JsValue.Undefined;

            return lex._engine._referencePool.Rent(identifierEnvironment, name, strict);
        }

        internal static bool TryGetIdentifierEnvironmentWithBindingValue(
            LexicalEnvironment lex,
            JsValue name,
            bool strict,
            out EnvironmentRecord record,
            out JsValue value)
        {
            var keyValue = new KeyValue(name.ToString(), name);
            while (true)
            {
                if (lex._record.TryGetBinding(
                    keyValue,
                    strict,
                    out _,
                    out value))
                {
                    record = lex._record;
                    return true;
                }

                if (lex._outer == null)
                {
                    record = default;
                    value = default;
                    return false;
                }

                lex = lex._outer;
            }
        }

        public static LexicalEnvironment NewDeclarativeEnvironment(Engine engine, LexicalEnvironment outer = null)
        {
            var environment = new LexicalEnvironment(engine, null, null);
            environment._record = new DeclarativeEnvironmentRecord(engine);
            environment._outer = outer;

            return environment;
        }

        public static LexicalEnvironment NewObjectEnvironment(Engine engine, ObjectInstance objectInstance, LexicalEnvironment outer, bool provideThis)
        {
            return new LexicalEnvironment(engine, new ObjectEnvironmentRecord(engine, objectInstance, provideThis), outer);
        }
    }
}

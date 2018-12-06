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
        internal readonly EnvironmentRecord _record;
        internal readonly LexicalEnvironment _outer;

        public LexicalEnvironment(Engine engine, EnvironmentRecord record, LexicalEnvironment outer)
        {
            _engine = engine;
            _record = record;
            _outer = outer;
        }

        public EnvironmentRecord Record => _record;

        public LexicalEnvironment Outer => _outer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Reference GetIdentifierReference(LexicalEnvironment lex, string name, bool strict)
        {
            var identifierEnvironment = TryGetIdentifierEnvironmentWithBindingValue(lex, name, strict, out var temp, out _)
                ? temp
                : JsValue.Undefined;

            return lex._engine._referencePool.Rent(identifierEnvironment, name, strict);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetIdentifierEnvironmentWithBindingValue(
            LexicalEnvironment lex,
            string name,
            bool strict,
            out EnvironmentRecord record,
            out JsValue value)
        {
            // optimize for common case where result is in one of the nearest scopes
            if (lex._record.TryGetBinding(name, strict, out var binding))
            {
                record = lex._record;
                value = lex._record.UnwrapBindingValue(name, strict, binding);
                return true;
            }

            if (lex._outer == null)
            {
                record = default;
                value = default;
                return false;
            }

            return TryGetIdentifierReferenceLooping(lex._outer, name, strict, out record, out value);
        }

        private static bool TryGetIdentifierReferenceLooping(
            LexicalEnvironment lex,
            string name,
            bool strict,
            out EnvironmentRecord record,
            out JsValue value)
        {
            while (true)
            {
                if (lex._record.TryGetBinding(name, strict, out var binding))
                {
                    record = lex._record;
                    value = lex._record.UnwrapBindingValue(name, strict, binding);
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
            return new LexicalEnvironment(engine, new DeclarativeEnvironmentRecord(engine), outer);
        }

        public static LexicalEnvironment NewObjectEnvironment(Engine engine, ObjectInstance objectInstance, LexicalEnvironment outer, bool provideThis)
        {
            return new LexicalEnvironment(engine, new ObjectEnvironmentRecord(engine, objectInstance, provideThis), outer);
        }
    }
}

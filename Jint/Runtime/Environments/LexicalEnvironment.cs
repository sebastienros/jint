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
            // optimize for common case where result is in one of the nearest scopes
            if (lex._record.HasBinding(name))
            {
                return lex._engine._referencePool.Rent(lex._record, name, strict);
            }

            if (lex._outer == null)
            {
                return new Reference(Undefined.Instance, name, strict);
            }
            
            return GetIdentifierReferenceLooping(lex._outer, name, strict);
        }

        private static Reference GetIdentifierReferenceLooping(LexicalEnvironment lex, string name, bool strict)
        {
            while (true)
            {
                if (lex._record.HasBinding(name))
                {
                    return lex._engine._referencePool.Rent(lex._record, name, strict);
                }

                if (lex._outer == null)
                {
                    return lex._engine._referencePool.Rent(Undefined.Instance, name, strict);
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

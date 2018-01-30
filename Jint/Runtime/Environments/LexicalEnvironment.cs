using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.References;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a Liexical Environment (a.k.a Scope)
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.2
    /// </summary>
    public sealed class LexicalEnvironment
    {
        private readonly Engine _engine;
        private readonly EnvironmentRecord _record;
        private readonly LexicalEnvironment _outer;

        public LexicalEnvironment(Engine engine, EnvironmentRecord record, LexicalEnvironment outer)
        {
            _engine = engine;
            _record = record;
            _outer = outer;
        }

        public EnvironmentRecord Record => _record;

        public LexicalEnvironment Outer => _outer;

        public static Reference GetIdentifierReference(LexicalEnvironment lex, string name, bool strict)
        {
            while (true)
            {
                if (lex == null)
                {
                    return new Reference(Undefined.Instance, name, strict);
                }

                if (lex.Record.HasBinding(name))
                {
                    return lex._engine.ReferencePool.Rent(lex.Record, name, strict);
                }

                if (lex.Outer == null)
                {
                    return lex._engine.ReferencePool.Rent(Undefined.Instance, name, strict);
                }

                lex = lex.Outer;
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

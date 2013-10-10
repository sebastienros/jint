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
        private readonly EnvironmentRecord _record;
        private readonly LexicalEnvironment _outer;

        public LexicalEnvironment(EnvironmentRecord record, LexicalEnvironment outer)
        {
            _record = record;
            _outer = outer;
        }

        public EnvironmentRecord Record
        {
            get { return _record; }
        }

        public LexicalEnvironment Outer
        {
            get { return _outer; }
        }

        public static Reference GetIdentifierReference(LexicalEnvironment lex, string name, bool strict)
        {
            if (lex == null)
            {
                return new Reference(Undefined.Instance, name, strict);
            }

            if (lex.Record.HasBinding(name))
            {
                return new Reference(lex.Record, name, strict);
            }

            if (lex.Outer == null)
            {
                return new Reference(Undefined.Instance, name, strict);    
            }

            return GetIdentifierReference(lex.Outer, name, strict);
        }

        public static LexicalEnvironment NewDeclarativeEnvironment(Engine engine, LexicalEnvironment outer = null)
        {
            return new LexicalEnvironment(new DeclarativeEnvironmentRecord(engine), outer);
        }

        public static LexicalEnvironment NewObjectEnvironment(Engine engine, ObjectInstance objectInstance, LexicalEnvironment outer, bool provideThis)
        {
            return new LexicalEnvironment(new ObjectEnvironmentRecord(engine, objectInstance, provideThis), outer);
        }
    }

    
}

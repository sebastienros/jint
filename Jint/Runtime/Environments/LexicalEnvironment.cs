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

        public Reference GetIdentifierReference(string name, bool strict)
        {
            if (Record.HasBinding(name))
            {
                return new Reference(Record, name, strict);
            }
            
            if (Outer == null)
            {
                return new Reference(Undefined.Instance, name, strict);    
            }
            
            return Outer.GetIdentifierReference(name, strict);
        }

        public static LexicalEnvironment NewDeclarativeEnvironment(Engine engine, LexicalEnvironment outer = null)
        {
            return new LexicalEnvironment(new DeclarativeEnvironmentRecord(), outer);
        }

        public static LexicalEnvironment NewObjectEnvironment(Engine engine, ObjectInstance objectInstance, LexicalEnvironment outer, bool provideThis)
        {
            return new LexicalEnvironment(new ObjectEnvironmentRecord(engine, objectInstance, provideThis), outer);
        }
    }

    
}

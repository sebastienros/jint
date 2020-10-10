#nullable enable

using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Environments
{
    internal static class JintEnvironment
    {
        internal static bool TryGetIdentifierEnvironmentWithBindingValue(
            Engine engine,
            EnvironmentRecord? lex,
            in EnvironmentRecord.BindingName name,
            bool strict,
            out EnvironmentRecord? record,
            out JsValue? value)
        {
            record = default;
            value = default;

            if (ReferenceEquals(lex, engine.Realm.GlobalEnv)
                && lex.TryGetBinding(name, strict, out _, out value))
            {
                record = lex;
                return true;
            }

            while (!ReferenceEquals(lex, null))
            {
                if (lex.TryGetBinding(
                    name,
                    strict,
                    out _,
                    out value))
                {
                    record = lex;
                    return true;
                }

                lex = lex._outerEnv;
            }

            return false;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-newdeclarativeenvironment
        /// </summary>
        internal static DeclarativeEnvironmentRecord NewDeclarativeEnvironment(Engine engine, EnvironmentRecord outer, bool catchEnvironment = false)
        {
            return new DeclarativeEnvironmentRecord(engine, catchEnvironment)
            {
                _outerEnv = outer
            };
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-newfunctionenvironment
        /// </summary>
        internal static FunctionEnvironmentRecord NewFunctionEnvironment(Engine engine, FunctionInstance f, JsValue newTarget)
        {
            return new FunctionEnvironmentRecord(engine, f, newTarget)
            {
                _outerEnv = f._environment
            };
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-newglobalenvironment
        /// </summary>
        internal static GlobalEnvironmentRecord NewGlobalEnvironment(Engine engine, ObjectInstance objectInstance, JsValue thisValue)
        {
            return new GlobalEnvironmentRecord(engine, objectInstance)
            {
                _outerEnv = null
            };
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-newobjectenvironment
        /// </summary>
        internal static ObjectEnvironmentRecord NewObjectEnvironment(Engine engine, ObjectInstance objectInstance, EnvironmentRecord outer, bool provideThis, bool withEnvironment = false)
        {
            return new ObjectEnvironmentRecord(engine, objectInstance, provideThis, withEnvironment)
            {
                _outerEnv = outer
            };
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-newprivateenvironment
        /// </summary>
        internal static PrivateEnvironmentRecord NewPrivateEnvironment(Engine engine, PrivateEnvironmentRecord? outerPriv)
        {
            return new PrivateEnvironmentRecord(outerPriv);
        }
    }
}

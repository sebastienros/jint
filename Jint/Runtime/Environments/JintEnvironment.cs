using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Environments
{
    internal static class JintEnvironment
    {
        internal static bool TryGetIdentifierEnvironmentWithBinding(
            EnvironmentRecord env,
            in EnvironmentRecord.BindingName name,
            [NotNullWhen(true)] out EnvironmentRecord? record)
        {
            record = env;

            if (env._outerEnv is null)
            {
                return env.HasBinding(name);
            }

            while (!ReferenceEquals(record, null))
            {
                if (record.HasBinding(name))
                {
                    return true;
                }

                record = record._outerEnv;
            }

            return false;
        }

        internal static bool TryGetIdentifierEnvironmentWithBindingValue(
            EnvironmentRecord env,
            in EnvironmentRecord.BindingName name,
            bool strict,
            [NotNullWhen(true)] out EnvironmentRecord? record,
            [NotNullWhen(true)] out JsValue? value)
        {
            record = env;
            value = default;

            if (env._outerEnv is null)
            {
                return env.TryGetBinding(name, strict, out _, out value);
            }

            while (!ReferenceEquals(record, null))
            {
                if (record.TryGetBinding(
                    name,
                    strict,
                    out _,
                    out value))
                {
                    return true;
                }

                record = record._outerEnv;
            }

            return false;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-newdeclarativeenvironment
        /// </summary>
        internal static DeclarativeEnvironmentRecord NewDeclarativeEnvironment(Engine engine, EnvironmentRecord? outer, bool catchEnvironment = false)
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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-newmoduleenvironment
        /// </summary>
        internal static ModuleEnvironmentRecord NewModuleEnvironment(Engine engine, EnvironmentRecord outer)
        {
            return new ModuleEnvironmentRecord(engine)
            {
                _outerEnv = outer
            };
        }
    }
}

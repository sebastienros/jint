using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Environments;

internal static class JintEnvironment
{
    internal static bool TryGetIdentifierEnvironmentWithBinding(
        Environment env,
        Environment.BindingName name,
        [NotNullWhen(true)] out Environment? record)
    {
        record = env;

        if (env._outerEnv is null)
        {
            return env.HasBinding(name);
        }

        while (record is not null)
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
        Environment env,
        Environment.BindingName name,
        bool strict,
        [NotNullWhen(true)] out Environment? record,
        [NotNullWhen(true)] out JsValue? value)
    {
        record = env;
        value = default;

        if (env._outerEnv is null)
        {
            return ((GlobalEnvironment) env).TryGetBinding(name, strict, out value);
        }

        while (record is not null)
        {
            if (record.TryGetBinding(name, strict, out value))
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
    internal static DeclarativeEnvironment NewDeclarativeEnvironment(Engine engine, Environment? outer, bool catchEnvironment = false)
    {
        return new DeclarativeEnvironment(engine, catchEnvironment)
        {
            _outerEnv = outer
        };
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-newfunctionenvironment
    /// </summary>
    internal static FunctionEnvironment NewFunctionEnvironment(Engine engine, Function f, JsValue newTarget)
    {
        var state = f._functionDefinition?.Initialize();
        FunctionEnvironment env;

        // Reuse a pooled FunctionEnvironment when the function's bindings cannot escape the call
        // (no closure capture, not a generator/async). Re-bind to the new function/target/outer env
        // and reset transient state. Slot storage is handled below.
        if (state is { EnvironmentMayEscape: false }
            && Interlocked.Exchange(ref state._cachedEnv, null) is { } cachedEnv
            && ReferenceEquals(cachedEnv._engine, engine))
        {
            cachedEnv.Reset(f, newTarget, f._environment);
            env = cachedEnv;
        }
        else
        {
            env = new FunctionEnvironment(engine, f, newTarget)
            {
                _outerEnv = f._environment,
            };
        }

        if (state is { UseFixedSlots: true })
        {
            env._slotNames = state.SlotNames;
            // Try to reuse cached slots from previous call to same function (thread-safe)
            var cached = Interlocked.Exchange(ref state._cachedSlots, null);
            env._slots = cached ?? new Binding[state.SlotNames!.Length];
        }

        return env;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-newglobalenvironment
    /// </summary>
    internal static GlobalEnvironment NewGlobalEnvironment(Engine engine, ObjectInstance objectInstance, JsValue thisValue)
    {
        return new GlobalEnvironment(engine, objectInstance)
        {
            _outerEnv = null
        };
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-newobjectenvironment
    /// </summary>
    internal static ObjectEnvironment NewObjectEnvironment(Engine engine, ObjectInstance objectInstance, Environment outer, bool provideThis, bool withEnvironment = false)
    {
        return new ObjectEnvironment(engine, objectInstance, provideThis, withEnvironment)
        {
            _outerEnv = outer
        };
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-newprivateenvironment
    /// </summary>
    internal static PrivateEnvironment NewPrivateEnvironment(Engine engine, PrivateEnvironment? outerPriv)
    {
        return new PrivateEnvironment(outerPriv);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-newmoduleenvironment
    /// </summary>
    internal static ModuleEnvironment NewModuleEnvironment(Engine engine, Environment outer)
    {
        return new ModuleEnvironment(engine)
        {
            _outerEnv = outer
        };
    }
}

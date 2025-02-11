using System.Threading;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-arguments-exotic-objects
/// </summary>
public sealed class JsArguments : ObjectInstance
{
    // cache property container for array iteration for less allocations
    private static readonly ThreadLocal<HashSet<Key>> _mappedNamed = new(() => []);

    private Function.Function _func = null!;
    private Key[] _names = null!;
    private JsValue[] _args = null!;
    private DeclarativeEnvironment _env = null!;
    private bool _canReturnToPool;
    private bool _hasRestParameter;
    private bool _materialized;

    internal JsArguments(Engine engine)
        : base(engine, ObjectClass.Arguments)
    {
    }

    internal void Prepare(
        Function.Function func,
        Key[] names,
        JsValue[] args,
        DeclarativeEnvironment env,
        bool hasRestParameter)
    {
        _func = func;
        _names = names;
        _args = args;
        _env = env;
        _hasRestParameter = hasRestParameter;
        _canReturnToPool = true;

        ClearProperties();
    }

    protected override void Initialize()
    {
        _canReturnToPool = false;
        var args = _args;

        DefinePropertyOrThrow(CommonProperties.Length, new PropertyDescriptor(_args.Length, PropertyFlag.NonEnumerable));

        if (_func is null)
        {
            // unmapped
            ParameterMap = null;

            for (uint i = 0; i < (uint) args.Length; i++)
            {
                var val = args[i];
                CreateDataProperty(JsString.Create(i), val);
            }

            DefinePropertyOrThrow(CommonProperties.Callee, new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(_engine, PropertyFlag.None));
        }
        else
        {
            ObjectInstance? map = null;
            if (args.Length > 0)
            {
                var mappedNamed = _mappedNamed.Value!;
                mappedNamed.Clear();

                map = Engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);

                for (uint i = 0; i < (uint) args.Length; i++)
                {
                    SetOwnProperty(JsString.Create(i), new PropertyDescriptor(args[i], PropertyFlag.ConfigurableEnumerableWritable));
                    if (i < _names.Length)
                    {
                        var name = _names[i];
                        if (mappedNamed.Add(name))
                        {
                            map.SetOwnProperty(JsString.Create(i), new ClrAccessDescriptor(_env, Engine, name));
                        }
                    }
                }
            }

            ParameterMap = map;

            // step 13
            DefinePropertyOrThrow(CommonProperties.Callee, new PropertyDescriptor(_func, PropertyFlag.NonEnumerable));
        }

        var iteratorFunction = new ClrFunction(Engine, "iterator", _engine.Realm.Intrinsics.Array.PrototypeObject.Values, 0, PropertyFlag.Configurable);
        DefinePropertyOrThrow(GlobalSymbolRegistry.Iterator, new PropertyDescriptor(iteratorFunction, PropertyFlag.Writable | PropertyFlag.Configurable));
    }

    internal ObjectInstance? ParameterMap { get; set; }

    internal override bool IsArrayLike => true;

    internal override bool IsIntegerIndexedArray => true;

    public uint Length => (uint) _args.Length;

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        EnsureInitialized();

        if (ParameterMap is not null)
        {
            var desc = base.GetOwnProperty(property);
            if (desc == PropertyDescriptor.Undefined)
            {
                return desc;
            }

            if (ParameterMap.TryGetValue(property, out var jsValue) && !jsValue.IsUndefined())
            {
                desc.Value = jsValue;
            }

            return desc;
        }

        return base.GetOwnProperty(property);
    }

    /// Implementation from ObjectInstance official specs as the one
    /// in ObjectInstance is optimized for the general case and wouldn't work
    /// for arrays
    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        EnsureInitialized();

        if (!CanPut(property))
        {
            return false;
        }

        var ownDesc = GetOwnProperty(property);

        if (ownDesc.IsDataDescriptor())
        {
            var valueDesc = new PropertyDescriptor(value, PropertyFlag.None);
            return DefineOwnProperty(property, valueDesc);
        }

        // property is an accessor or inherited
        var desc = GetOwnProperty(property);

        if (desc.IsAccessorDescriptor())
        {
            if (desc.Set is not ICallable setter)
            {
                return false;
            }
            setter.Call(receiver, value);
        }
        else
        {
            var newDesc = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
            return DefineOwnProperty(property, newDesc);
        }

        return true;
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (_hasRestParameter)
        {
            // immutable
            return true;
        }

        EnsureInitialized();

        if (ParameterMap is not null)
        {
            var map = ParameterMap;
            var isMapped = map.GetOwnProperty(property);
            var allowed = base.DefineOwnProperty(property, desc);
            if (!allowed)
            {
                return false;
            }

            if (isMapped != PropertyDescriptor.Undefined)
            {
                if (desc.IsAccessorDescriptor())
                {
                    map.Delete(property);
                }
                else
                {
                    var descValue = desc.Value;
                    if (descValue is not null && !descValue.IsUndefined())
                    {
                        map.Set(property, descValue, false);
                    }

                    if (desc.WritableSet && !desc.Writable)
                    {
                        map.Delete(property);
                    }
                }
            }

            return true;
        }

        return base.DefineOwnProperty(property, desc);
    }

    public override bool Delete(JsValue property)
    {
        EnsureInitialized();

        if (ParameterMap is not null)
        {
            var map = ParameterMap;
            var isMapped = map.GetOwnProperty(property);
            var result = base.Delete(property);
            if (result && isMapped != PropertyDescriptor.Undefined)
            {
                map.Delete(property);
            }

            return result;
        }

        return base.Delete(property);
    }

    internal void Materialize()
    {
        if (_materialized)
        {
            // already done
            return;
        }

        _materialized = true;

        EnsureInitialized();

        var args = _args;
        var copiedArgs = new JsValue[args.Length];
        System.Array.Copy(args, copiedArgs, args.Length);
        _args = copiedArgs;

        _canReturnToPool = false;
    }

    internal void FunctionWasCalled()
    {
        // should no longer expose arguments which is special name
        ParameterMap = null;

        if (_canReturnToPool)
        {
            _engine._argumentsInstancePool.Return(this);
            // prevent double-return
            _canReturnToPool = false;
        }
    }
}

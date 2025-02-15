using System.Threading;
using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Atomics;

/// <summary>
/// https://tc39.es/ecma262/#sec-atomics-object
/// </summary>
internal sealed class AtomicsInstance : ObjectInstance
{
    private readonly Realm _realm;

    public AtomicsInstance(
        Engine engine,
        Realm realm,
        ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["pause"] = new(new ClrFunction(Engine, "pause", Pause, 0, PropertyFlag.Configurable), true, false, true),
        };
        SetProperties(properties);
    }

    private JsValue Pause(JsValue thisObject, JsCallArguments arguments)
    {
        var iterationNumber = arguments.At(0);
        if (!iterationNumber.IsUndefined())
        {
            if (!iterationNumber.IsNumber())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Invalid iteration count");
            }

            var n = TypeConverter.ToNumber(iterationNumber);
            if (!TypeConverter.IsIntegralNumber(n))
            {
                ExceptionHelper.ThrowTypeError(_realm, "Invalid iteration count");
            }

            if (n < 0)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid iteration count");
            }

            n = System.Math.Min(n, _engine.Options.Constraints.MaxAtomicsPauseIterations);
            Thread.SpinWait((int) n);
        }
        else
        {
            Thread.SpinWait(1);
        }

        return Undefined;
    }
}

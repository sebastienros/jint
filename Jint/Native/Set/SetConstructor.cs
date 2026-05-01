using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Set;

[JsObject]
public sealed partial class SetConstructor : Constructor
{
    private static readonly JsString _functionName = new("Set");

    internal SetConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new SetPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal SetPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateSymbols_Generated();

    public JsSet Construct() => ConstructSet(this);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-iterable
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var set = ConstructSet(newTarget);

        if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
        {
            var adderValue = set.Get("add");
            var adder = adderValue as ICallable;
            if (adder is null)
            {
                Throw.TypeError(_engine.Realm, "add must be callable");
            }

            var iterable = arguments.At(0).GetIterator(_realm);

            try
            {
                var args = new JsValue[1];
                do
                {
                    if (!iterable.TryIteratorStep(out var next))
                    {
                        return set;
                    }

                    var nextValue = next.Get(CommonProperties.Value);
                    args[0] = nextValue;
                    adder.Call(set, args);
                } while (true);
            }
            catch
            {
                iterable.Close(CompletionType.Throw);
                throw;
            }
        }

        return set;
    }

    private JsSet ConstructSet(JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_engine.Realm, $"Constructor {_nameDescriptor?.Value} requires 'new'");
        }

        if (ReferenceEquals(newTarget, this))
        {
            return new JsSet(_engine)
            {
                _prototype = PrototypeObject
            };
        }

        var set = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Set.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsSet(engine));
        return set;
    }

    [JsSymbolAccessor("Species")]
    private static JsValue Species(JsValue thisObject)
    {
        return thisObject;
    }
}

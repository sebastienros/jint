using System.Globalization;
using Jint.Native.Generator;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Runtime;

namespace Jint.Native.Iterator;

internal abstract class IteratorInstance : ObjectInstance
{
    protected IteratorInstance(Engine engine) : base(engine)
    {
        _prototype = engine.Realm.Intrinsics.ArrayIteratorPrototype;
    }

    public override object ToObject()
    {
        ExceptionHelper.ThrowNotImplementedException();
        return null;
    }

    public abstract bool TryIteratorStep(out ObjectInstance nextItem);

    public virtual void Close(CompletionType completion)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createiterresultobject
    /// </summary>
    private IteratorResult CreateIterResultObject(JsValue value, bool done)
    {
        return new IteratorResult(_engine, value, JsBoolean.Create(done));
    }

    internal sealed class ObjectIterator : IteratorInstance
    {
        private readonly ObjectInstance _target;
        private readonly ICallable _nextMethod;

        public ObjectIterator(ObjectInstance target) : base(target.Engine)
        {
            _target = target;
            if (target.Get(CommonProperties.Next) is not ICallable callable)
            {
                ExceptionHelper.ThrowTypeError(target.Engine.Realm);
                return;
            }

            _nextMethod = callable;
        }

        public override bool TryIteratorStep(out ObjectInstance result)
        {
            result = IteratorNext();

            var done = result.Get(CommonProperties.Done);
            if (!done.IsUndefined() && TypeConverter.ToBoolean(done))
            {
                return false;
            }

            return true;
        }

        private ObjectInstance IteratorNext()
        {
            var jsValue = _nextMethod.Call(_target, Arguments.Empty);
            var instance = jsValue as ObjectInstance;
            if (instance is null)
            {
                ExceptionHelper.ThrowTypeError(_target.Engine.Realm, $"Iterator result {jsValue} is not an object");
            }

            return instance;
        }

        public override void Close(CompletionType completion)
        {
            if (!_target.TryGetValue(CommonProperties.Return, out var func)
                || func.IsNullOrUndefined())
            {
                return;
            }

            var callable = func as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeError(_target.Engine.Realm, func + " is not a function");
            }

            var innerResult = Undefined;
            try
            {
                innerResult = callable.Call(_target, Arguments.Empty);
            }
            catch
            {
                if (completion != CompletionType.Throw)
                {
                    throw;
                }
            }

            if (completion != CompletionType.Throw && !innerResult.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_target.Engine.Realm, "Iterator returned non-object");
            }
        }
    }

    internal sealed class StringIterator : IteratorInstance
    {
        private readonly TextElementEnumerator _iterator;

        public StringIterator(Engine engine, string str) : base(engine)
        {
            _iterator = StringInfo.GetTextElementEnumerator(str);
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_iterator.MoveNext())
            {
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, (string) _iterator.Current);
                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }

    internal sealed class RegExpStringIterator : IteratorInstance
    {
        private readonly JsRegExp _iteratingRegExp;
        private readonly string _s;
        private readonly bool _global;
        private readonly bool _unicode;

        private bool _done;

        public RegExpStringIterator(Engine engine, ObjectInstance iteratingRegExp, string iteratedString, bool global, bool unicode) : base(engine)
        {
            var r = iteratingRegExp as JsRegExp;
            if (r is null)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm);
            }

            _iteratingRegExp = r;
            _s = iteratedString;
            _global = global;
            _unicode = unicode;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_done)
            {
                nextItem = CreateIterResultObject(Undefined, true);
                return false;
            }

            var match = RegExpPrototype.RegExpExec(_iteratingRegExp, _s);
            if (match.IsNull())
            {
                _done = true;
                nextItem = CreateIterResultObject(Undefined, true);
                return false;
            }

            if (_global)
            {
                var macthStr = TypeConverter.ToString(match.Get(JsString.NumberZeroString));
                if (macthStr == "")
                {
                    var thisIndex = TypeConverter.ToLength(_iteratingRegExp.Get(JsRegExp.PropertyLastIndex));
                    var nextIndex = thisIndex + 1;
                    _iteratingRegExp.Set(JsRegExp.PropertyLastIndex, nextIndex, true);
                }
            }
            else
            {
                _done = true;
            }

            nextItem = CreateIterResultObject(match, false);
            return true;
        }
    }

    internal sealed class EnumerableIterator : IteratorInstance
    {
        private readonly IEnumerator<JsValue> _enumerable;

        public EnumerableIterator(Engine engine, IEnumerable<JsValue> obj) : base(engine)
        {
            _enumerable = obj.GetEnumerator();
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_enumerable.MoveNext())
            {
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, _enumerable.Current);
                return true;
            }

            nextItem = IteratorResult.CreateValueIteratorPosition(_engine, done: JsBoolean.True);
            return false;
        }
    }

    internal sealed class GeneratorIterator : IteratorInstance
    {
        private readonly GeneratorInstance _generator;

        public GeneratorIterator(Engine engine, GeneratorInstance generator) : base(engine)
        {
            _generator = generator;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            nextItem = IteratorResult.CreateValueIteratorPosition(_engine, done: JsBoolean.True);
            return false;
        }
    }

}

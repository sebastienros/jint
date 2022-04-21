using System;
using Jint.Native.Object;
using Jint.Pooling;
using Jint.Runtime;

namespace Jint.Native.Function
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-bound-function-exotic-objects
    /// </summary>
    internal sealed class BindFunctionInstance : ObjectInstance, IConstructor, ICallable
    {
        private readonly Realm _realm;

        public BindFunctionInstance(
            Engine engine,
            Realm realm,
            ObjectInstance proto,
            ObjectInstance targetFunction,
            JsValue boundThis,
            in Arguments boundArgs)
            : base(engine, ObjectClass.Function)
        {
            _realm = realm;
            _prototype = proto;
            BoundTargetFunction = targetFunction;
            BoundThis = boundThis;
            BoundArguments = new JsValue[boundArgs.Length];
            boundArgs.CopyTo(BoundArguments);
        }

        /// <summary>
        /// The wrapped function object.
        /// </summary>
        public JsValue BoundTargetFunction { get; }

        /// <summary>
        /// The value that is always passed as the this value when calling the wrapped function.
        /// </summary>
        public JsValue BoundThis { get; }

        /// <summary>
        /// A list of values whose elements are used as the first arguments to any call to the wrapped function.
        /// </summary>
        public JsValue[] BoundArguments { get; }

        JsValue ICallable.Call(JsValue thisObject, in Arguments arguments)
        {
            var f = BoundTargetFunction as FunctionInstance;
            if (f is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            using var args = CreateArguments(arguments);
            var value = f.Call(BoundThis, new Arguments(args._array, args.Span.Length));
            return value;
        }

        ObjectInstance IConstructor.Construct(in Arguments arguments, JsValue newTarget)
        {
            var target = BoundTargetFunction as IConstructor;
            if (target is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            using var args = CreateArguments(arguments);

            if (ReferenceEquals(this, newTarget))
            {
                newTarget = BoundTargetFunction;
            }

            var value = target.Construct(new Arguments(args._array, args.Span.Length), newTarget);
            return value;
        }

        internal override bool OrdinaryHasInstance(JsValue v)
        {
            var f = BoundTargetFunction as FunctionInstance;
            if (f is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return f.OrdinaryHasInstance(v);
        }

        private JsValueArrayPool.ArrayRentHolder CreateArguments(in Arguments arguments)
        {
            var combined = _engine._jsValueArrayPool.RentArray(BoundArguments.Length + arguments.Length);
            System.Array.Copy(BoundArguments, combined._array, BoundArguments.Length);
            arguments.CopyTo(combined._array.AsSpan(BoundArguments.Length));
            return combined;
        }

        internal override bool IsConstructor => BoundTargetFunction.IsConstructor;

        public override string ToString() => "function () { [native code] }";
    }
}

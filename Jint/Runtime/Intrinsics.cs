using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Error;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Json;
using Jint.Native.Map;
using Jint.Native.Math;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Proxy;
using Jint.Native.Reflect;
using Jint.Native.RegExp;
using Jint.Native.Set;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Native.WeakMap;
using Jint.Native.WeakSet;

namespace Jint.Runtime
{
    public sealed class Intrinsics
    {
        private static readonly JsString _errorFunctionName = new("Error");
        private static readonly JsString _evalErrorFunctionName = new("EvalError");
        private static readonly JsString _rangeErrorFunctionName = new("RangeError");
        private static readonly JsString _referenceErrorFunctionName = new("ReferenceError");
        private static readonly JsString _syntaxErrorFunctionName = new("SyntaxError");
        private static readonly JsString _typeErrorFunctionName = new("TypeError");
        private static readonly JsString _uriErrorFunctionName = new("URIError");

        private readonly Engine _engine;
        private readonly Realm _realm;

        // lazy properties
        private ErrorConstructor _error;
        private ErrorConstructor _evalError;
        private ErrorConstructor _rangeError;
        private ErrorConstructor _referenceError;
        private ErrorConstructor _syntaxError;
        private ErrorConstructor _typeError;
        private ErrorConstructor _uriError;

        internal Intrinsics(Engine engine, Realm realm)
        {
            _engine = engine;
            _realm = realm;

            // we need to transfer state currently to some initialization, would otherwise require quite the
            // ClrFunctionInstance constructor refactoring
            _engine._originalIntrinsics = this;

            Object = new ObjectConstructor(engine, realm);
            Function = new FunctionConstructor(engine, realm, Object.PrototypeObject);

            // this is implementation dependent, and only to pass some unit tests
            Object._prototype = Function.PrototypeObject;

            Symbol = new SymbolConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            Array = new ArrayConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            Map = new MapConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            Set = new SetConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            WeakMap = new WeakMapConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            WeakSet = new WeakSetConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            Promise = new PromiseConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            Iterator = new IteratorConstructor(engine, realm, Object.PrototypeObject);
            String = new StringConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            RegExp = new RegExpConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            Number = new NumberConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            Boolean = new BooleanConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);
            Date = new DateConstructor(engine, realm, Function.PrototypeObject, Object.PrototypeObject);

            Math = new MathInstance(engine, Object.PrototypeObject);
            Json = new JsonInstance(engine, realm, Object.PrototypeObject);
            Proxy = new ProxyConstructor(engine, realm);
            Reflect = new ReflectInstance(engine, realm, Object.PrototypeObject);
            Eval = new EvalFunctionInstance(engine, realm, Function.PrototypeObject);
        }

        public ObjectConstructor Object { get; }
        public FunctionConstructor Function { get; }
        public ArrayConstructor Array { get; }
        public MapConstructor Map { get; }
        public SetConstructor Set { get; }
        public WeakMapConstructor WeakMap { get; }
        public WeakSetConstructor WeakSet { get; }
        public PromiseConstructor Promise { get; }
        public IteratorConstructor Iterator { get; }
        public StringConstructor String { get; }
        public RegExpConstructor RegExp { get; }
        public BooleanConstructor Boolean { get; }
        public NumberConstructor Number { get; }
        public DateConstructor Date { get; }
        public MathInstance Math { get; }
        public JsonInstance Json { get; }
        public ProxyConstructor Proxy { get; }
        public ReflectInstance Reflect { get; }
        public SymbolConstructor Symbol { get; }
        public EvalFunctionInstance Eval { get; }

        public ErrorConstructor Error =>
            _error ??= new ErrorConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject, _errorFunctionName);

        public ErrorConstructor EvalError =>
            _evalError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _evalErrorFunctionName);

        public ErrorConstructor SyntaxError =>
            _syntaxError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _syntaxErrorFunctionName);

        public ErrorConstructor TypeError =>
            _typeError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _typeErrorFunctionName);

        public ErrorConstructor RangeError =>
            _rangeError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _rangeErrorFunctionName);

        public ErrorConstructor ReferenceError
            => _referenceError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _referenceErrorFunctionName);

        public ErrorConstructor UriError =>
            _uriError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _uriErrorFunctionName);
    }
}
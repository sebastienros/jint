using Jint.Native;
using Jint.Native.Array;
using Jint.Native.ArrayBuffer;
using Jint.Native.Boolean;
using Jint.Native.DataView;
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
        private WeakMapConstructor _weakMap;
        private WeakSetConstructor _weakSet;
        private PromiseConstructor _promise;
        private ProxyConstructor _proxy;
        private ReflectInstance _reflect;
        private EvalFunctionInstance _eval;
        private DateConstructor _date;
        private IteratorConstructor _iterator;
        private MathInstance _math;
        private JsonInstance _json;
        private SymbolConstructor _symbol;
        private RegExpConstructor _regExp;
        private NumberConstructor _number;
        private StringConstructor _string;
        private MapConstructor _map;
        private SetConstructor _set;
        private ArrayConstructor _array;
        private BooleanConstructor _boolean;
        private ArrayBufferConstructor _arrayBufferConstructor;
        private DataViewConstructor _dataView;

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
        }

        public ObjectConstructor Object { get; }
        public FunctionConstructor Function { get; }

        public ArrayConstructor Array =>
            _array ??= new ArrayConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public DataViewConstructor DataView =>
            _dataView ??= new DataViewConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public ArrayBufferConstructor ArrayBuffer =>
            _arrayBufferConstructor ??= new ArrayBufferConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public MapConstructor Map =>
            _map ??= new MapConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public SetConstructor Set =>
            _set ??= new SetConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public WeakMapConstructor WeakMap =>
            _weakMap ??= new WeakMapConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public WeakSetConstructor WeakSet =>
            _weakSet ??= new WeakSetConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public PromiseConstructor Promise =>
            _promise ??= new PromiseConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public IteratorConstructor Iterator =>
            _iterator ??= new IteratorConstructor(_engine, _realm, Object.PrototypeObject);

        public StringConstructor String =>
            _string ??= new StringConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public RegExpConstructor RegExp =>
            _regExp ??= new RegExpConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public BooleanConstructor Boolean =>
            _boolean ??= new BooleanConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public NumberConstructor Number =>
            _number ??= new NumberConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public DateConstructor Date =>
            _date ??= new DateConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public MathInstance Math =>
            _math ??= new MathInstance(_engine, Object.PrototypeObject);

        public JsonInstance Json =>
            _json ??= new JsonInstance(_engine, _realm, Object.PrototypeObject);

        public ProxyConstructor Proxy =>
            _proxy ??= new ProxyConstructor(_engine, _realm);

        public ReflectInstance Reflect =>
            _reflect ??= new ReflectInstance(_engine, _realm, Object.PrototypeObject);

        public SymbolConstructor Symbol =>
            _symbol ??= new SymbolConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

        public EvalFunctionInstance Eval =>
            _eval ??= new EvalFunctionInstance(_engine, _realm, Function.PrototypeObject);

        public ErrorConstructor Error =>
            _error ??= new ErrorConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject, _errorFunctionName);

        public ErrorConstructor EvalError =>
            _evalError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _evalErrorFunctionName);

        public ErrorConstructor SyntaxError =>
            _syntaxError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _syntaxErrorFunctionName);

        public ErrorConstructor TypeError =>
            _typeError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _typeErrorFunctionName);

        public ErrorConstructor RangeError =>
            _rangeError ??=
                new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _rangeErrorFunctionName);

        public ErrorConstructor ReferenceError
            => _referenceError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _referenceErrorFunctionName);

        public ErrorConstructor UriError =>
            _uriError ??= new ErrorConstructor(_engine, _realm, Error, Error.PrototypeObject, _uriErrorFunctionName);
    }
}
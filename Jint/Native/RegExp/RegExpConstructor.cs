using System.Text.RegularExpressions;
using Esprima;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.RegExp
{
    public sealed class RegExpConstructor : Constructor
    {
        private static readonly JsString _functionName = new JsString("RegExp");

        internal RegExpConstructor(
            Engine engine,
            Realm realm,
            FunctionPrototype functionPrototype,
            ObjectPrototype objectPrototype)
            : base(engine, realm, _functionName)
        {
            _prototype = functionPrototype;
            PrototypeObject = new RegExpPrototype(engine, realm, this, objectPrototype);
            _length = new PropertyDescriptor(2, PropertyFlag.Configurable);
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        }

        internal RegExpPrototype PrototypeObject { get; }

        protected override void Initialize()
        {
            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(_engine, "get [Symbol.species]", (thisObj, _) => thisObj, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            return Construct(arguments, this);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-regexp-pattern-flags
        /// </summary>
        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var pattern = arguments.At(0);
            var flags = arguments.At(1);

            var patternIsRegExp = pattern.IsRegExp();
            if (newTarget.IsUndefined())
            {
                newTarget = this;
                if (patternIsRegExp && flags.IsUndefined())
                {
                    var patternConstructor = pattern.Get(CommonProperties.Constructor);
                    if (ReferenceEquals(newTarget, patternConstructor))
                    {
                        return (ObjectInstance) pattern;
                    }
                }
            }

            JsValue p;
            JsValue f;
            if (pattern is JsRegExp regExpInstance)
            {
                p = regExpInstance.Source;
                f = flags.IsUndefined() ? regExpInstance.Flags : flags;
            }
            else if (patternIsRegExp)
            {
                p = pattern.Get(RegExpPrototype.PropertySource);
                f = flags.IsUndefined() ? pattern.Get(RegExpPrototype.PropertyFlags) : flags;
            }
            else
            {
                p = pattern;
                f = flags;
            }

            var r = RegExpAlloc(newTarget);
            return RegExpInitialize(r, p, f);
        }

        private ObjectInstance RegExpInitialize(JsRegExp r, JsValue pattern, JsValue flags)
        {
            var p = pattern.IsUndefined() ? "" : TypeConverter.ToString(pattern);
            if (string.IsNullOrEmpty(p))
            {
                p = "(?:)";
            }

            var f = flags.IsUndefined() ? "" : TypeConverter.ToString(flags);

            try
            {
                var regExp = Scanner.AdaptRegExp(p, f, compiled: false, _engine.Options.Constraints.RegexTimeout);

                if (regExp is null)
                {
                    ExceptionHelper.ThrowSyntaxError(_realm, $"Could not parse regex '/{p}/{flags}'");
                }

                r.Value = regExp;
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowSyntaxError(_realm, ex.Message);
            }

            r.Flags = f;
            r.Source = p;

            RegExpInitialize(r);

            return r;
        }

        private JsRegExp RegExpAlloc(JsValue newTarget)
        {
            var r = OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.RegExp.PrototypeObject,
                static (Engine engine, Realm _, object? _) => new JsRegExp(engine));
            return r;
        }

        public JsRegExp Construct(Regex regExp, string source, string flags)
        {
            var r = new JsRegExp(Engine);
            r._prototype = PrototypeObject;

            r.Flags = flags;
            r.Source = source;

            var timeout = _engine.Options.Constraints.RegexTimeout;
            if (timeout.Ticks > 0)
            {
                r.Value = new Regex(regExp.ToString(), regExp.Options, timeout);
            }
            else
            {
                r.Value = regExp;
            }

            RegExpInitialize(r);

            return r;
        }

        private static void RegExpInitialize(JsRegExp r)
        {
            r.SetOwnProperty(JsRegExp.PropertyLastIndex, new PropertyDescriptor(0, PropertyFlag.OnlyWritable));
        }
    }
}
